// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Stores Application Caching metadata, publication authority, quotas, operations and immutable payload chunks in one owned native database.</summary>
/// <remarks>Coordination is confined to this process. Native calls execute synchronously on the named database's worker.</remarks>
/// <example><code>services.AddApplicationCaching().WithLiteDbProvider("catalog");</code></example>
public sealed partial class LiteDbCacheStoreProvider : ICacheStoreProvider
{
    private readonly IServiceScopeFactory scopes;
    private readonly LiteDbFeatureOptions native;
    private readonly CacheProviderOptions options;
    private readonly CacheDiagnosticsOptions diagnostics;
    private readonly TimeProvider timeProvider;
    private readonly ResourceBudget transient;
    private readonly ResourceBudget resident;
    private readonly CacheRuntimeResources resources;
    private readonly CacheAdmission readers;
    private readonly CacheAdmission writes;
    private readonly CacheAdmission queries;
    private readonly string instance = Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim initialization = new(1);
    private Dictionary<string, CacheProviderCollection> definitions;
    private string catalogHash;
    private int disposed;

    /// <summary>Creates a lazy complete cache provider backed by a named database and an exclusively registered feature namespace.</summary>
    /// <param name="scopes">Host scope factory; every command owns a fresh session.</param>
    /// <param name="native">Frozen database selection and collection namespace.</param>
    /// <param name="name">Stable cache provider alias.</param>
    /// <param name="options">Finite provider storage settings.</param>
    /// <param name="timeProvider">Authoritative validity clock.</param>
    /// <param name="resources">Optional shared process resource budgets.</param>
    /// <param name="diagnostics">Tracked operation limits and retention.</param>
    public LiteDbCacheStoreProvider(IServiceScopeFactory scopes, LiteDbFeatureOptions native, string name,
        CacheProviderOptions options = null, TimeProvider timeProvider = null, CacheRuntimeResources resources = null,
        CacheDiagnosticsOptions diagnostics = null)
    {
        this.scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        ArgumentNullException.ThrowIfNull(native);
        this.native = new() { DatabaseName = native.DatabaseName, CollectionPrefix = native.CollectionPrefix, MaxMaterializedRows = native.MaxMaterializedRows };
        this.Name = CacheIdentityNormalizer.Name(name);
        this.options = OptionsPatch.Clone(options ?? new());
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.diagnostics = diagnostics ?? new();
        this.resources = resources;
        this.resident = resources?.ForProvider(this.Name) ?? new(this.Name, this.options.MaxResidentBytes);
        this.transient = resources?.Transient ?? new("litedb-cache-transient", 64 * CacheDefaults.MiB);
        if (this.options.ChunkSize is <= 0 or > 1024 * 1024 || this.options.MaxStorageBytes <= 0 || this.options.MaxEntries <= 0 || !double.IsFinite(this.options.MetadataReserveRatio) ||
            this.options.MetadataReserveRatio is <= 0 or >= 1 || this.native.MaxMaterializedRows <= 0)
        {
            throw new ArgumentException("Native cache chunk, entry and storage limits must be finite and positive.", nameof(options));
        }

        this.readers = new("litedb-cache-readers", 8, 0, timeProvider: this.timeProvider);
        this.writes = new("litedb-cache-staging", 16, 0, timeProvider: this.timeProvider);
        this.queries = new("litedb-cache-queries", 4, 0, timeProvider: this.timeProvider);
    }

    /// <summary>Gets durable, streaming, seekable, local-process capabilities; shared-node ownership and native asynchronous I/O are unavailable.</summary>
    public static CacheProviderCapabilities DefaultCapabilities { get; } = new(true, false, true, true, false, true);

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public CacheProviderCapabilities Capabilities => DefaultCapabilities;

    /// <inheritdoc />
    public async ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default)
    {
        await this.initialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var normalized = CacheBoundedSnapshot.Copy(collections, 1024, nameof(collections)).Select(Normalize).ToArray();
            var map = normalized.Where(d => d.Policies.Enabled).ToDictionary(d => new CacheEntryIdentity(d.Namespace, d.Collection, d.ContractVersion, "catalog").Canonical);
            var fingerprint = Hash(JsonSerializer.Serialize(normalized.OrderBy(d => d.Namespace).ThenBy(d => d.Collection).ThenBy(d => d.ContractVersion)));
            if (this.definitions is not null)
            {
                return this.catalogHash == fingerprint ? Result.Success() : Result.Failure(Error(CacheErrorCode.Compatibility, "The initialized cache catalog is immutable."));
            }

            var result = await this.Run(db =>
            {
                foreach (var suffix in new[] { "entries", "payloads", "fences", "operations", "receipts" })
                {
                    this.Rows(db, suffix).EnsureIndex("scope", "$.Scope");
                    this.Rows(db, suffix).EnsureIndex("sequence", "$.Sequence");
                }

                foreach (var suffix in new[] { "fences", "payloads", "receipts" }) { this.Rows(db, suffix).EnsureIndex("owner", "$.Owner"); }

                this.Rows(db, "entries").EnsureIndex("key", "$.LogicalKey");
                this.Rows(db, "entries").EnsureIndex("expires", "$.Expires");
                this.Rows(db, "entries").EnsureIndex("published", "$.Published");
                this.Rows(db, "entries").EnsureIndex("size", "$.Size");
                this.Rows(db, "entries").EnsureIndex("keyHash", "$.KeyHash");
                this.Rows(db, "payloads").EnsureIndex("state", "$.State");
                this.Rows(db, "operations").EnsureIndex("runnable", "$.Runnable");
                var store = this.Get<StoreState>(db, "state", "store") ?? new StoreState { Secret = RandomNumberGenerator.GetBytes(32) };
                this.Put(db, "state", "store", store);
                foreach (var definition in map.Values)
                {
                    var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                    var previous = this.Get<CacheProviderCollection>(db, "catalog", identity.Digest);
                    if (previous is not null && !Compatible(previous, definition))
                    {
                        throw Failure(CacheErrorCode.Compatibility, "A retained contract alias or dimension schema changed without a new contract version.");
                    }

                    this.Put(db, "catalog", identity.Digest, definition);
                    if (this.Get<CollectionState>(db, "state", Scope(identity)) is null)
                    {
                        this.Put(db, "state", Scope(identity), new CollectionState());
                    }
                }

                return true;
            }, cancellationToken, requireReady: false).ConfigureAwait(false);
            if (result.IsFailure) { return Result.Failure().WithErrors(result.Errors); }

            this.catalogHash = fingerprint;
            this.definitions = map;
            return Result.Success();
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return Result.Failure(Error(CacheErrorCode.Validation, "Invalid cache collection catalog."));
        }
        finally { this.initialization.Release(); }
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.ReadAsync(identity, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion expectedVersion, CacheReadRequest request,
        CancellationToken cancellationToken = default) => this.ReadAsync(identity, request with { ExpectedVersion = expectedVersion }, cancellationToken);

    private async ValueTask<Result<CacheReadValidation>> ReadAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        CacheAdmissionLease admission = null;
        ResourceCharge memory = null;
        try
        {
            if (request.OpenPayload)
            {
                var acquired = await this.readers.AcquireAsync(this.options.AttemptTimeout, token).ConfigureAwait(false);
                if (acquired.IsFailure) { return Result<CacheReadValidation>.Failure(acquired); }

                admission = acquired.Value;
                if (!ResourceCharge.TryReserve(2L * CacheDefaults.MaximumChunkBytes, out memory, this.transient, this.ResidentFor(this.Definition(identity)))) { throw Failure(CacheErrorCode.Capacity, "Reader buffer capacity is exhausted."); }
            }

            var result = await this.Run(db =>
            {
                _ = this.Definition(identity);
                var entry = this.Entry(db, identity);
                var now = this.timeProvider.GetUtcNow();
                if (entry is null || !Valid(entry.Metadata, now)) { return new CacheReadValidation(CacheReadValidationState.Missing, null, default); }

                var metadata = entry.Metadata;
                if (request.ExpectedVersion is { } expected && metadata.Version != expected)
                {
                    return new CacheReadValidation(CacheReadValidationState.Changed, metadata, default);
                }

                if (request.ObserveAccess)
                {
                    metadata = metadata with { LastAccessAt = now, HasForegroundAccess = true,
                        ExpiresAt = metadata.SlidingIdleTimeout is { } sliding ? Min(metadata.AbsoluteExpiresAt, now.Add(sliding)) : metadata.ExpiresAt };
                    entry.Metadata = metadata;
                    this.SaveEntry(db, entry);
                }

                ICacheProviderReadHandle handle = null;
                if (request.OpenPayload && !metadata.IsNotFound)
                {
                    var payload = this.Required<PayloadState>(db, "payloads", entry.Payload);
                    if (payload.PinOwner != this.instance) { payload.Pins = 0; payload.PinOwner = this.instance; }

                    payload.Pins++;
                    this.SavePayload(db, payload);
                    handle = new ReadHandle(this, payload, metadata, admission, memory);
                }

                return new CacheReadValidation(CacheReadValidationState.Valid, metadata, default, handle);
            }, token).ConfigureAwait(false);
            if (result.IsSuccess && result.Value.Handle is not null) { admission = null; memory = null; }

            return result;
        }
        catch (LiteDbProviderException ex) { return Result<CacheReadValidation>.Failure().WithErrors(ex.Errors); }
        finally { admission?.Dispose(); memory?.Dispose(); }
    }

    /// <inheritdoc />
    public ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        var definition = this.Definition(identity);
        var scope = Scope(identity);
        var key = this.Get<KeyState>(db, "keys", LogicalKey(identity)) ?? new();
        var state = this.Required<CollectionState>(db, "state", scope);
        var store = this.Required<StoreState>(db, "state", "store");
        this.ChargeMetadata(store, state, definition, FenceCharge);
        key.Captures++;
        if (!key.Sequences.TryGetValue(identity.ContractVersion, out var sequence))
        {
            sequence = this.Entry(db, identity)?.Sequence ?? 0;
            key.Sequences[identity.ContractVersion] = sequence;
        }

        var id = Guid.NewGuid().ToString("N");
        this.Put(db, "keys", LogicalKey(identity), key);
        this.Put(db, "fences", id, new FenceState(identity, state.Epoch, key.Epoch, sequence, this.instance), scope);
        var fenceRow = this.Rows(db, "fences").FindById(id);
        fenceRow["Owner"] = this.instance;
        this.Rows(db, "fences").Update(fenceRow);
        this.SaveAccounting(db, store, scope, state);
        return new CachePublicationFence(id);
    }, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default)
    {
        var result = await this.Run(db =>
        {
            var capture = this.Get<FenceState>(db, "fences", fence.Value);
            if (capture is null || capture.Identity != identity) { return true; }

            var key = this.Required<KeyState>(db, "keys", LogicalKey(identity));
            if (--key.Captures == 0) { this.Rows(db, "keys").Delete(LogicalKey(identity)); }
            else { this.Put(db, "keys", LogicalKey(identity), key); }

            this.Rows(db, "fences").Delete(fence.Value);
            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", Scope(identity));
            store.Metadata -= FenceCharge; state.Metadata -= FenceCharge;
            this.SaveAccounting(db, store, Scope(identity), state);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? expectedVersion = null, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        _ = this.Definition(identity);
        var entry = this.Entry(db, identity);
        var now = this.timeProvider.GetUtcNow();
        if (entry is null || !Valid(entry.Metadata, now)) { throw Failure(CacheErrorCode.NotFound, "The cache entry is missing or expired."); }

        if (expectedVersion.HasValue && expectedVersion != entry.Metadata.Version) { throw Conflict(); }

        entry.Metadata = entry.Metadata with { LastAccessAt = now, HasForegroundAccess = true,
            ExpiresAt = entry.Metadata.SlidingIdleTimeout is { } sliding ? Min(entry.Metadata.AbsoluteExpiresAt, now.Add(sliding)) : entry.Metadata.ExpiresAt };
        this.SaveEntry(db, entry);
        return entry.Metadata;
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) => this.Run(db =>
    {
        var state = this.Required<StoreState>(db, "state", "store");
        return new CacheProviderQuota(state.Current, state.Retired, state.Staging, state.Metadata, state.Count, this.options.MaxStorageBytes, false);
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) => UnsupportedOwnership(cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default) => UnsupportedOwnership(cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default) => UnsupportedOwnership(cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion, CancellationToken cancellationToken = default) => UnsupportedCompletion(cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default) => UnsupportedCompletion(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() { Interlocked.Exchange(ref this.disposed, 1); return ValueTask.CompletedTask; }

    private async ValueTask<Result<T>> Run<T>(Func<LiteDatabase, T> action, CancellationToken token, bool requireReady = true)
    {
        token.ThrowIfCancellationRequested();
        if (Volatile.Read(ref this.disposed) != 0 || (requireReady && this.definitions is null))
        {
            return Result<T>.Failure(Error(CacheErrorCode.ProviderUnavailable, "The native cache provider is not ready or has stopped."));
        }

        try
        {
            await using var scope = this.scopes.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>(this.native.DatabaseName);
            return await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(db =>
            {
                var result = action(db);
                token.ThrowIfCancellationRequested();
                return result;
            }, token).ConfigureAwait(false)), true, token).ConfigureAwait(false);
        }
        catch (LiteDbProviderException ex) { return Result<T>.Failure().WithErrors(ex.Errors); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or InvalidDataException)
        {
            return Result<T>.Failure(Error(CacheErrorCode.Validation, "Invalid native cache metadata or continuation."));
        }
        catch (LiteException) { return Result<T>.Failure(Error(CacheErrorCode.ProviderUnavailable, "The native cache database could not complete its command.")); }
    }

    private CacheProviderCollection Definition(CacheEntryIdentity identity)
    {
        if (this.definitions is null || Volatile.Read(ref this.disposed) != 0) { throw Failure(CacheErrorCode.ProviderUnavailable, "The native cache provider is not ready or has stopped."); }

        if (!identity.IsValid) { throw Failure(CacheErrorCode.Validation, "A canonical entry identity is required."); }

        return this.definitions.GetValueOrDefault(new CacheEntryIdentity(identity.Namespace, identity.Collection, identity.ContractVersion, "catalog").Canonical)
            ?? throw Failure(CacheErrorCode.Contract, "This collection contract was not bound during initialization.");
    }

    private ILiteCollection<BsonDocument> Rows(LiteDatabase db, string suffix) => db.GetCollection<BsonDocument>(this.native.CollectionPrefix + "_" + suffix);

    private T Get<T>(LiteDatabase db, string suffix, string id) where T : class => id is null ? null : Deserialize<T>(this.Rows(db, suffix).FindById(id));

    private T Required<T>(LiteDatabase db, string suffix, string id) where T : class => this.Get<T>(db, suffix, id) ?? throw Failure(CacheErrorCode.Corruption, "Required native cache state is missing.");

    private static T Deserialize<T>(BsonDocument row) => row is null ? default : JsonSerializer.Deserialize<T>(row["Data"].AsString);

    private void Put<T>(LiteDatabase db, string suffix, string id, T value, string scope = null, long sequence = 0)
    {
        this.Rows(db, suffix).Upsert(new BsonDocument { ["_id"] = id, ["Data"] = JsonSerializer.Serialize(value), ["Scope"] = scope, ["Sequence"] = sequence });
    }

    private EntryState Entry(LiteDatabase db, CacheEntryIdentity identity) => this.Get<EntryState>(db, "entries", identity.Digest);

    private void SaveEntry(LiteDatabase db, EntryState entry)
    {
        var identity = entry.Metadata.Identity;
        entry.SerializedMetadataBytes = JsonSerializer.SerializeToUtf8Bytes(entry.Metadata).LongLength;
        this.Put(db, "entries", identity.Digest, entry, Scope(identity), entry.Sequence);
        var row = this.Rows(db, "entries").FindById(identity.Digest);
        row["LogicalKey"] = LogicalKey(identity);
        row["Expires"] = entry.Metadata.ExpiresAt.UtcTicks;
        row["Published"] = entry.Metadata.PublishedAt.UtcTicks;
        row["Size"] = entry.Metadata.StoredSizeBytes;
        row["KeyHash"] = Hash(identity.Key);
        foreach (var dimension in entry.Metadata.Dimensions)
        {
            var field = Hash(dimension.Name)[..24];
            row["P" + field] = true;
            if (dimension.Form == CacheDimensionForm.Scalar) { row["E" + field] = DimensionToken(dimension.Values[0]); }
            else if (dimension.Form == CacheDimensionForm.Set) { row["D" + field] = new BsonArray(dimension.Values.Select(v => new BsonValue(DimensionToken(v)))); }
            else { row["L" + field] = Number(dimension.Values[0]); row["U" + field] = Number(dimension.Values[1]); }
        }

        this.Rows(db, "entries").Update(row);
    }

    private void SavePayload(LiteDatabase db, PayloadState payload)
    {
        this.Put(db, "payloads", payload.Id, payload, payload.Scope);
        var row = this.Rows(db, "payloads").FindById(payload.Id);
        row["State"] = payload.State;
        row["Owner"] = payload.Owner;
        row["Pins"] = payload.Pins;
        row["PinOwner"] = payload.PinOwner;
        this.Rows(db, "payloads").Update(row);
    }

    private void VerifyFence(LiteDatabase db, CacheEntryIdentity identity, CachePublicationFence fence)
    {
        var capture = this.Get<FenceState>(db, "fences", fence.Value);
        var key = this.Get<KeyState>(db, "keys", LogicalKey(identity));
        if (capture is null || capture.Owner != this.instance || key is null || capture.Identity != identity || capture.CollectionEpoch != this.Required<CollectionState>(db, "state", Scope(identity)).Epoch ||
            capture.KeyEpoch != key.Epoch || capture.Sequence != key.Sequences.GetValueOrDefault(identity.ContractVersion)) { throw Conflict(); }
    }

    private void SaveAccounting(LiteDatabase db, StoreState store, string scope, CollectionState state)
    {
        this.Put(db, "state", "store", store);
        this.Put(db, "state", scope, state);
    }

    private void ChargeMetadata(StoreState store, CollectionState state, CacheProviderCollection definition, long bytes)
    {
        if (bytes < 0 || store.Metadata + bytes > this.options.MaxStorageBytes * this.options.MetadataReserveRatio ||
            store.Total + bytes > this.options.MaxStorageBytes || state.Total + bytes > definition.Policies.Limits.MaxStorageBytes)
        { throw Failure(CacheErrorCode.Capacity, "Native cache metadata capacity is exhausted."); }

        store.Metadata += bytes; state.Metadata += bytes;
    }

    private ResourceBudget ResidentFor(CacheProviderCollection definition) => this.resources?.ForCollection(definition.Collection).Resident ?? this.resident;

    private const long FenceCharge = 2048;
    private const long PayloadCharge = 2048;
    private static string Scope(CacheEntryIdentity identity) => Hash(identity.CollectionIdentity);
    private static string LogicalKey(CacheEntryIdentity identity) => Hash(new CacheEntryIdentity(identity.Namespace, identity.Collection, "key", identity.Key).Canonical);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static CacheError Error(CacheErrorCode code, string message) => new(code, message);
    private static LiteDbProviderException Failure(CacheErrorCode code, string message) => new(Error(code, message));
    private static LiteDbProviderException Conflict() => Failure(CacheErrorCode.Conflict, "The expected version or captured publication authority changed.");
    private static bool Valid(CacheEntryMetadata metadata, DateTimeOffset now) => metadata.ExpiresAt > now && metadata.AbsoluteExpiresAt > now;
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
    private static ValueTask<Result<CacheOwnershipObservation>> UnsupportedOwnership(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<CacheOwnershipObservation>.Failure(Error(CacheErrorCode.Unsupported, "Shared-node source ownership is unavailable for the local native database.")));
    }

    private static ValueTask<Result> UnsupportedCompletion(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Failure(Error(CacheErrorCode.Unsupported, "Shared-node source ownership is unavailable for the local native database.")));
    }

    private class Accounting
    {
        public long Current { get; set; }

        public long Retired { get; set; }

        public long Staging { get; set; }

        public long Metadata { get; set; }

        public long Count { get; set; }

        public long Total => this.Current + this.Retired + this.Staging + this.Metadata;
    }

    private sealed class StoreState : Accounting
    {
        public long Sequence { get; set; }

        public byte[] Secret { get; set; }
    }

    private sealed class CollectionState : Accounting
    {
        public long Epoch { get; set; }

        public long Revision { get; set; }
    }

    private sealed class KeyState
    {
        public long Epoch { get; set; }

        public int Captures { get; set; }

        public Dictionary<string, long> Sequences { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed record FenceState(CacheEntryIdentity Identity, long CollectionEpoch, long KeyEpoch, long Sequence, string Owner);
    private sealed class EntryState
    {
        public CacheEntryMetadata Metadata { get; set; }

        public string Payload { get; set; }

        public long Sequence { get; set; }

        public long MetadataChargeBytes { get; set; }

        public long SerializedMetadataBytes { get; set; }
    }

    private sealed class PayloadState
    {
        public string Id { get; set; }

        public string Scope { get; set; }

        public string State { get; set; } = "staging";
        public string Owner { get; set; }

        public long Length { get; set; }

        public int Chunks { get; set; }

        public int ReclaimedChunks { get; set; }

        public long ReclaimedBytes { get; set; }

        public int ChunkSize { get; set; }

        public int Pins { get; set; }

        public string PinOwner { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    private static CacheProviderCollection Normalize(CacheProviderCollection definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Dimensions);
        var dimensions = CacheBoundedSnapshot.Copy(definition.Dimensions, 16, nameof(definition.Dimensions))
            .Select(d => d with { Name = CacheIdentityNormalizer.Name(d.Name) }).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        if (dimensions.Length > 16 || dimensions.Select(d => d.Name).Distinct(StringComparer.Ordinal).Count() != dimensions.Length ||
            dimensions.Any(d => !Enum.IsDefined(d.Kind) || !Enum.IsDefined(d.Form) ||
                (d.Form == CacheDimensionForm.Interval && (d.Kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp) || d.AllowNull))))
        {
            throw new ArgumentException("Invalid native dimension schema.");
        }

        return new(CacheIdentityNormalizer.Namespace(definition.Namespace), CacheIdentityNormalizer.Name(definition.Collection),
            CacheIdentityNormalizer.ContractVersion(definition.ContractVersion), CacheIdentityNormalizer.Name(definition.ContractId),
            Array.AsReadOnly(dimensions), OptionsPatch.Clone(definition.Policies ?? new()));
    }

    private static bool Compatible(CacheProviderCollection left, CacheProviderCollection right) =>
        left.ContractId == right.ContractId && JsonSerializer.Serialize(left.Dimensions) == JsonSerializer.Serialize(right.Dimensions);

}
