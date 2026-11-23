// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.Collections.Frozen;
using System.Data.Common;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>Owns the complete advanced-cache relational store using short bounded transactions and immutable payload generations.</summary>
/// <remarks>Hosts deploy matching migrations and SQLite WAL/FULL settings before readiness. Native SQLite I/O is synchronous and host-local.</remarks>
/// <example><code>var provider = new EntityFrameworkCacheStoreProvider("database", options, () => new CacheDbContext(contextOptions));</code></example>
public class EntityFrameworkCacheStoreProvider : ICacheStoreProvider
{
    // Startup and shared persistence helpers.

    private readonly CacheProviderOptions options;
    private readonly Func<DbContext> contexts;
    private readonly TimeProvider time;
    private readonly IDatabaseReadyService ready;
    private readonly CacheRuntimeResources resources;
    private readonly ResourceBudget resident;
    private readonly ResourceBudget transient;
    private readonly CacheAdmission calls;
    private readonly CacheAdmission queries;
    private readonly SemaphoreSlim initialization = new(1);
    private readonly string node = Guid.NewGuid().ToString("N");
    private readonly ILogger<EntityFrameworkCacheStoreProvider> logger;
    private readonly CacheDiagnosticsOptions diagnostics;
    private FrozenDictionary<string, CacheProviderCollection> definitions;
    private byte[] continuationKey;
    private int disposed;

    /// <summary>Creates lazy native bindings without database access. Every context returned by the host factory is independently owned and disposed.</summary>
    public EntityFrameworkCacheStoreProvider(string name, CacheProviderOptions options, Func<DbContext> contexts,
        TimeProvider timeProvider = null, CacheRuntimeResources resources = null, IDatabaseReadyService ready = null, ILogger<EntityFrameworkCacheStoreProvider> logger = null,
        CacheDiagnosticsOptions diagnostics = null)
    {
        this.Name = CacheIdentityNormalizer.Name(name);
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        this.time = timeProvider ?? TimeProvider.System;
        this.resources = resources;
        this.ready = ready;
        this.logger = logger;
        this.diagnostics = diagnostics ?? new();
        this.resident = resources?.ForProvider(this.Name) ?? new(this.Name, options.MaxResidentBytes);
        this.transient = resources?.Transient ?? new("native-transient", 64 * CacheDefaults.MiB);
        this.calls = new("relational-native", 64, 256, timeProvider: this.time);
        this.queries = new("relational-metadata", 4, 0, timeProvider: this.time);
        this.Capabilities = CapabilitiesFor(options);
        if (options.ChunkSize is <= 0 or > 1048576 || options.InlinePayloadThreshold is <= 0 or > 65536 || options.InlinePayloadThreshold > options.ChunkSize)
        {
            throw new ArgumentException("Relational payload thresholds must remain within 64 KiB inline and 1 MiB chunks.", nameof(options));
        }
    }

    /// <summary>Gets this complete provider's normalized registration alias.</summary>
    public string Name { get; }

    /// <summary>Gets the actual engine's persistence, coordination, native-I/O and access scope.</summary>
    public CacheProviderCapabilities Capabilities { get; }

    /// <summary>Resolves truthful engine capabilities without opening a database or promising arbitrary seeking.</summary>
    public static CacheProviderCapabilities CapabilitiesFor(CacheProviderOptions options) => options.Engine?.ToUpperInvariant() switch
    {
        "SQLITE" => new(true, true, true, false, false, true),
        "SQLSERVER" or "POSTGRESQL" or "POSTGRES" => new(true, true, true, false, true, false),
        _ => throw new ArgumentException("Choose the SqlServer, PostgreSql or local-host Sqlite cache profile.", nameof(options))
    };

    /// <summary>Checks deployed metadata/schema and canonical contracts. It never creates tables, applies migrations or changes SQLite journal mode.</summary>
    public async ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default)
    {
        if (collections is null || collections.Count > 1024)
        {
            return Result.Failure(Error(CacheErrorCode.Validation, "The native startup catalog must be finite."));
        }

        await this.initialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.CheckAvailable(false);
            var declared = collections.Select(Normalize).ToArray();
            var map = declared.ToFrozenDictionary(d => ContractId(new(d.Namespace, d.Collection, d.ContractVersion, "catalog")), StringComparer.Ordinal);
            if (this.definitions is not null)
            {
                return this.definitions.Count == map.Count && map.All(p => this.definitions.TryGetValue(p.Key, out var value) && Serialize(value) == Serialize(p.Value)) ?
                    Result.Success() : Result.Failure(Error(CacheErrorCode.Compatibility, "This provider's startup catalog is immutable for its lifetime."));
            }

            if (this.ready is not null)
            {
                await this.ready.WaitForReadyAsync(this.options.DatabaseReadyAlias, timeout: this.options.AttemptTimeout,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await using var context = this.CreateContext(false);
            var native = context.Database.ProviderName;
            if (!EngineMatches(this.options.Engine, native) || context.Database.CreateExecutionStrategy().RetriesOnFailure)
            {
                return Result.Failure(Error(CacheErrorCode.Validation, "The configured native engine must match and must not automatically replay mutations."));
            }

            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (this.Capabilities.LocalHostOnly && !await this.SqliteProfileValidAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(Error(CacheErrorCode.Compatibility, "SQLite caching requires a local file, WAL, FULL commits and the configured busy budget."));
            }

            await using var transaction = await LockStoreAsync(context, cancellationToken).ConfigureAwait(false);
            var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, cancellationToken).ConfigureAwait(false);
            if (store.MetadataFormat != 1 || store.IdentityFormat != 1 || this.options.MetadataFormatVersion != 1)
            {
                return Result.Failure(Error(CacheErrorCode.Compatibility, "The deployed cache metadata/identity format is incompatible."));
            }

            if (store.MaximumBytes == 0)
            {
                store.MaximumBytes = this.options.MaxStorageBytes;
                store.MaximumEntries = this.options.MaxEntries;
                store.CursorKey = RandomNumberGenerator.GetBytes(32);
                store.MetadataBytes = 16384;
            }
            else if (store.MaximumBytes != this.options.MaxStorageBytes || store.MaximumEntries != this.options.MaxEntries || store.CursorKey?.Length != 32)
            {
                return Result.Failure(Error(CacheErrorCode.Compatibility, "Participating providers must agree on the shared quota profile and store identity."));
            }

            foreach (var definition in declared)
            {
                var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                var collectionId = CollectionId(identity);
                var collection = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheCollectionEntity>().Where(entity => entity.Id == collectionId), true, cancellationToken).ConfigureAwait(false);
                if (collection is null)
                {
                    this.ReserveMetadata(store, 32768);
                    collection = new CacheCollectionEntity { Id = collectionId, CanonicalIdentity = identity.CollectionIdentity, MetadataBytes = 32768,
                        MaximumBytes = definition.Policies.Limits.MaxStorageBytes, MaximumEntries = definition.Policies.Limits.MaxEntries };
                    context.Add(collection);
                    // Save this bounded definition before another contract version resolves the same collection.
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (collection.CanonicalIdentity != identity.CollectionIdentity)
                {
                    return Result.Failure(Error(CacheErrorCode.Corruption, "A persisted collection digest has a different canonical identity."));
                }

                if (collection.MaximumBytes != definition.Policies.Limits.MaxStorageBytes || collection.MaximumEntries != definition.Policies.Limits.MaxEntries ||
                    collection.MetadataBytes > collection.MaximumBytes)
                {
                    return Result.Failure(Error(CacheErrorCode.Compatibility, "Participating nodes must agree on the persisted logical collection quota profile."));
                }

                var contractId = ContractId(identity);
                var previous = await context.Set<CacheContractEntity>().AsNoTracking().Where(e => e.Id == contractId)
                    .Select(e => new { CollectionId = e.CollectionId.Substring(0, 65), ContractVersion = e.ContractVersion.Substring(0, 129), ContractAlias = e.ContractAlias.Substring(0, 129),
                        SchemaJson = e.SchemaJson.Substring(0, 16385), e.MetadataFormat }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                var schema = Serialize(definition.Dimensions);
                if (previous is not null && (previous.CollectionId != collectionId || previous.ContractVersion != identity.ContractVersion ||
                    previous.ContractAlias != definition.ContractId || previous.SchemaJson != schema || previous.MetadataFormat != 1))
                {
                    return Result.Failure(Error(CacheErrorCode.Compatibility, "A retained cache contract has an incompatible schema or value alias."));
                }

                if (previous is null)
                {
                    this.ReserveMetadata(store, collection, 16384);
                    context.Add(new CacheContractEntity { Id = contractId, CollectionId = collectionId, ContractVersion = identity.ContractVersion,
                        ContractAlias = definition.ContractId, SchemaJson = schema, PolicyJson = Serialize(definition.Policies), MetadataFormat = 1 });
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            this.continuationKey = store.CursorKey.ToArray();
            this.definitions = map;
            return Result.Success();
        }
        catch (CacheRelationalException exception) { return Result.Failure(Error(exception.Code, exception.Message)); }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return Result.Failure(Error(CacheErrorCode.ProviderUnavailable, "The deployed cache database is unavailable or not ready."));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return Result.Failure(Error(CacheErrorCode.Validation, "The cache startup catalog is invalid."));
        }
        finally { this.initialization.Release(); }
    }

    /// <summary>Stops new native work. Existing independently owned contexts/streams retain their resources until actual completion.</summary>
    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref this.disposed, 1);
        this.DisposeActivity();
        return ValueTask.CompletedTask;
    }

    private DbContext CreateContext(bool requireReady = true)
    {
        this.CheckAvailable(requireReady);
        var context = this.contexts() ?? throw new InvalidOperationException("The approved cache context factory returned null.");
        if (context is not IApplicationCachingContext)
        {
            context.Dispose();
            throw new ArgumentException("The native cache context must implement IApplicationCachingContext.");
        }

        context.Database.SetCommandTimeout(this.options.AttemptTimeout);
        return context;
    }

    private void CheckAvailable(bool requireReady = true)
    {
        if (Volatile.Read(ref this.disposed) != 0 || (requireReady && this.definitions is null))
        {
            throw new CacheRelationalException(CacheErrorCode.ProviderUnavailable, "The cache provider is not ready.");
        }
    }

    private CacheProviderCollection Definition(CacheEntryIdentity identity)
    {
        this.CheckAvailable();
        if (!identity.IsValid || !this.definitions.TryGetValue(ContractId(identity), out var definition) || !definition.Policies.Enabled)
        {
            throw new CacheRelationalException(CacheErrorCode.Contract, "The exact cache collection contract is unavailable.");
        }

        return definition;
    }

    private async ValueTask<CacheTransaction> LockStoreAsync(DbContext context, CancellationToken cancellationToken)
    {
        // The first root UPDATE is the store's mutation authority. Server engines use read-committed
        // snapshots after that lock is acquired; PostgreSQL serializable snapshots would reject a normal competing owner.
        var isolation = this.Capabilities.LocalHostOnly ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted;
        var transaction = await context.Database.BeginTransactionAsync(isolation, cancellationToken).ConfigureAwait(false);
        try
        {
            // A conditional native UPDATE locks the predeployed quota row on all three engines.
            // Assigning the format to itself preserves that row-write authority without a redundant revision token.
            // It is held only for bounded metadata/chunk transactions, never source work or a consumer stream.
            var locked = await context.Set<CacheStoreEntity>().Where(e => e.Id == "root")
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.MetadataFormat, e => e.MetadataFormat), cancellationToken).ConfigureAwait(false);
            if (locked != 1) { throw new CacheRelationalException(CacheErrorCode.Compatibility, "The deployed cache store identity is missing."); }

            return new(transaction, this.logger, this.Name);
        }
        catch
        {
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            catch { /* Preserve the original acquisition failure; no commit was requested. */ }

            throw;
        }
    }

    private async ValueTask<bool> SqliteProfileValidAsync(DbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var source = connection.DataSource;
        if (string.IsNullOrEmpty(source) || source == ":memory:" || source.StartsWith("\\\\", StringComparison.Ordinal) || source.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        var mode = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        command.CommandText = "PRAGMA synchronous";
        var synchronous = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        command.CommandText = "PRAGMA busy_timeout";
        var busy = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase) && synchronous >= 2 && busy == (int)this.options.SqliteBusyTimeout.TotalMilliseconds;
    }

    private void ReserveMetadata(CacheStoreEntity store, long bytes)
    {
        if (bytes < 0 || bytes > store.MaximumBytes - store.CurrentBytes - store.RetiredBytes - store.StagingBytes - store.MetadataBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "Shared cache metadata quota is exhausted.");
        }

        store.MetadataBytes += bytes;
    }

    private void ReserveMetadata(CacheStoreEntity store, CacheCollectionEntity collection, long bytes)
    {
        if (bytes < 0 || bytes > collection.MaximumBytes - collection.CurrentBytes - collection.RetiredBytes - collection.StagingBytes - collection.MetadataBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "Shared collection metadata quota is exhausted.");
        }

        this.ReserveMetadata(store, bytes);
        collection.MetadataBytes += bytes;
    }

    private static CacheProviderCollection Normalize(CacheProviderCollection definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
        if (definition.Dimensions is null || definition.Dimensions.Count > 16 ||
            definition.Dimensions.Select(d => CacheIdentityNormalizer.Name(d.Name)).Distinct(StringComparer.Ordinal).Count() != definition.Dimensions.Count)
        {
            throw new ArgumentException("A cache contract requires a bounded unique dimension schema.");
        }

        return definition with { Namespace = identity.Namespace, Collection = identity.Collection, ContractVersion = identity.ContractVersion,
            ContractId = CacheIdentityNormalizer.Name(definition.ContractId), Dimensions = definition.Dimensions.Select(d => d with { Name = CacheIdentityNormalizer.Name(d.Name) })
                .OrderBy(d => d.Name, StringComparer.Ordinal).ToArray(),
            Policies = definition.Policies ?? new() };
    }

    private static string CollectionId(CacheEntryIdentity identity) => Hash(identity.CollectionIdentity);

    private static string ContractId(CacheEntryIdentity identity) => Hash(new CacheEntryIdentity(identity.Namespace, identity.Collection, identity.ContractVersion, "catalog").Canonical);

    private static string KeyId(CacheEntryIdentity identity) => Hash(new CacheEntryIdentity(identity.Namespace, identity.Collection, "logical-key", identity.Key).Canonical);

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static CacheError Error(CacheErrorCode code, string message) => new(code, message);

    private static bool EngineMatches(string engine, string provider) => engine?.ToUpperInvariant() switch
    {
        "SQLITE" => provider == "Microsoft.EntityFrameworkCore.Sqlite",
        "SQLSERVER" => provider == "Microsoft.EntityFrameworkCore.SqlServer",
        "POSTGRESQL" or "POSTGRES" => provider == "Npgsql.EntityFrameworkCore.PostgreSQL",
        _ => false
    };

    private static string Serialize<T>(T value)
    {
        using var stream = new CacheRelationalMetadataStream();
        JsonSerializer.Serialize(stream, value);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    // Read snapshots and reader leases.

    /// <summary>Acquires an indexed metadata/inline-payload snapshot in one query by default, or a bounded protected chunk stream.</summary>
    public ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.Definition(identity);
            if (request.RequireSeek) { throw new CacheRelationalException(CacheErrorCode.Unsupported, "Relational payload streams support indexed ranges and forward reads, without arbitrary seeking."); }

            if (!ResourceCharge.TryReserve(576 * CacheDefaults.KiB, out var memory,
                this.transient, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "Native read-buffer admission was rejected.");
            }

            CacheTransaction transaction = null;
            var handedOff = false;
            try
            {
                if (definition.Policies.Expiration.SlidingEnabled && request.ObserveAccess)
                {
                    transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
                }

                var query = context.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.Id == identity.Digest);
                var inlineLimit = 64 * (int)CacheDefaults.KiB; // Read every format-one inline representation, independently of current write tuning.
                var entries = BoundedEntries(query);
                var acquired = request.OpenPayload ? await (from entry in entries
                    join payload in context.Set<CachePayloadEntity>() on entry.PayloadId equals payload.Id into payloads
                    from payload in payloads.DefaultIfEmpty()
                    select new ReadRow(entry, payload.InlineBytes.Length <= inlineLimit ? payload.InlineBytes : null,
                        (int?)payload.InlineBytes.Length, (long?)payload.SizeBytes, payload.StoredHash.Substring(0, 65)))
                    .SingleOrDefaultAsync(token).ConfigureAwait(false) :
                    await entries.Select(e => new ReadRow(e, null, null, null, null)).SingleOrDefaultAsync(token).ConfigureAwait(false);
                if (acquired is null || acquired.Entry.ExpiryTicks <= this.time.GetUtcNow().UtcTicks || acquired.Entry.AbsoluteExpiryTicks <= this.time.GetUtcNow().UtcTicks)
                {
                    return new CacheReadValidation(CacheReadValidationState.Missing, null, default);
                }

                var metadata = Metadata(acquired.Entry, identity);
                if (metadata.ContractId != definition.ContractId) { throw new CacheRelationalException(CacheErrorCode.Corruption, "The acquired value alias differs from its declared contract."); }

                if (request.ExpectedVersion is { } expected && metadata.Version != expected)
                {
                    return new CacheReadValidation(CacheReadValidationState.Changed, metadata, default);
                }

                if (request.ObserveAccess) { this.ObserveForeground(definition, metadata); }

                if (transaction is not null && !metadata.IsNotFound && metadata.SlidingIdleTimeout.HasValue)
                {
                    metadata = this.RenewMetadata(metadata);
                    var entry = acquired.Entry;
                    context.Attach(entry);
                    ApplyTouch(entry, metadata);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }

                if (!request.OpenPayload || metadata.IsNotFound)
                {
                    if (transaction is not null) { await transaction.CommitAsync(token).ConfigureAwait(false); }

                    return new CacheReadValidation(CacheReadValidationState.Valid, metadata, default);
                }

                if (acquired.PayloadBytes != metadata.StoredSizeBytes || acquired.PayloadHash != metadata.StoredHash || acquired.InlineLength > inlineLimit)
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "The acquired immutable payload does not match its metadata.");
                }

                string readerId = null;
                if (acquired.Inline is null)
                {
                    transaction ??= await LockStoreAsync(context, token).ConfigureAwait(false);
                    var payloadId = acquired.Entry.PayloadId;
                    if (!await context.Set<CachePayloadEntity>().AnyAsync(e => e.Id == payloadId && e.State != 0, token).ConfigureAwait(false))
                    {
                        return new CacheReadValidation(CacheReadValidationState.Missing, null, default);
                    }

                    var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                    var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == acquired.Entry.CollectionId), true, token).ConfigureAwait(false);
                    this.ReserveMetadata(store, collection, 256);
                    readerId = Guid.NewGuid().ToString("N");
                    context.Add(new CacheReaderEntity { Id = readerId, PayloadId = payloadId,
                        ExpiryTicks = this.time.GetUtcNow().Add(definition.Policies.Streaming.Lifetime).Add(this.options.AttemptTimeout).UtcTicks });
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }
                else if (acquired.Inline.LongLength != metadata.StoredSizeBytes)
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "The acquired inline payload has an invalid length.");
                }

                if (transaction is not null) { await transaction.CommitAsync(token).ConfigureAwait(false); }

                var protectedReader = readerId;
                var handle = new CacheRelationalReadHandle(metadata, acquired.Inline, memory,
                    (offset, readToken) => this.ReadChunkAsync(definition, acquired.Entry.PayloadId, protectedReader, offset, readToken),
                    () => this.ReleaseReaderAsync(protectedReader));
                handedOff = true;
                return new CacheReadValidation(CacheReadValidationState.Valid, metadata, default, handle);
            }
            finally
            {
                if (transaction is not null) { await transaction.DisposeAsync().ConfigureAwait(false); }

                if (!handedOff) { memory.Dispose(); }
            }
        }, cancellationToken);

    /// <summary>Establishes authoritative validity for the expected hot version without selecting payload columns unless requested explicitly.</summary>
    public ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion expectedVersion,
        CacheReadRequest request, CancellationToken cancellationToken = default) => this.LookupAsync(identity, request with { ExpectedVersion = expectedVersion }, cancellationToken);

    /// <summary>Atomically renews an unexpired sliding version without replacing its immutable payload or version token.</summary>
    public ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? expectedVersion = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            _ = this.Definition(identity);
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var entry = await ReadEntryAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == identity.Digest), true, token).ConfigureAwait(false);
            if (entry is null || entry.ExpiryTicks <= this.time.GetUtcNow().UtcTicks || entry.AbsoluteExpiryTicks <= this.time.GetUtcNow().UtcTicks)
            {
                throw new CacheRelationalException(CacheErrorCode.NotFound, "There is no valid cache version to touch.");
            }

            var metadata = Metadata(entry, identity);
            if (expectedVersion.HasValue && metadata.Version != expectedVersion.Value)
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "The expected cache version changed.");
            }

            metadata = this.RenewMetadata(metadata);
            ApplyTouch(entry, metadata);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return metadata;
        }, cancellationToken);

    private CacheEntryMetadata RenewMetadata(CacheEntryMetadata metadata)
    {
        if (metadata.IsNotFound || !metadata.SlidingIdleTimeout.HasValue) { return metadata; }

        var now = this.time.GetUtcNow();
        var expiry = now.Add(metadata.SlidingIdleTimeout.Value);
        return metadata with { LastAccessAt = now, HasForegroundAccess = true, ExpiresAt = expiry < metadata.AbsoluteExpiresAt ? expiry : metadata.AbsoluteExpiresAt };
    }

    private static void ApplyTouch(CacheEntryEntity entry, CacheEntryMetadata metadata)
    {
        entry.ExpiryTicks = metadata.ExpiresAt.UtcTicks;
        entry.PurgeAfterTicks = checked(metadata.ExpiresAt.UtcTicks + entry.RetentionGraceTicks);
        entry.LastAccessTicks = metadata.LastAccessAt.UtcTicks;
        entry.HasForegroundAccess = metadata.HasForegroundAccess;
        entry.MetadataJson = Serialize(metadata);
    }

    private async ValueTask<Result<CacheRelationalChunk>> ReadChunkAsync(CacheProviderCollection definition, string payloadId, string readerId,
        long offset, CancellationToken cancellationToken)
    {
        if (!ResourceCharge.TryReserve(CacheDefaults.MaximumChunkBytes + 8192L, out var memory, this.transient, this.ResidentFor(definition)))
        {
            return Result<CacheRelationalChunk>.Failure(Error(CacheErrorCode.Capacity, "Native chunk-buffer admission was rejected."));
        }

        var handedOff = false;
        try
        {
            var result = await this.ExecuteAsync(async (context, token) =>
            {
                var now = this.time.GetUtcNow().UtcTicks;
                var maximum = CacheDefaults.MaximumChunkBytes;
                var chunk = await context.Set<CacheChunkEntity>().AsNoTracking().Where(e => e.PayloadId == payloadId && e.Offset <= offset &&
                    context.Set<CacheReaderEntity>().Any(r => r.Id == readerId && r.PayloadId == payloadId && r.ExpiryTicks > now))
                    .OrderByDescending(e => e.Offset).Select(e => new { e.Offset, Hash = e.Hash.Substring(0, 65), Length = e.Data.Length, Data = e.Data.Length <= maximum ? e.Data : null })
                    .FirstOrDefaultAsync(token).ConfigureAwait(false);
                if (chunk is null || chunk.Data is null || chunk.Length > maximum || offset >= chunk.Offset + chunk.Length ||
                    !string.Equals(Convert.ToHexStringLower(SHA256.HashData(chunk.Data)), chunk.Hash, StringComparison.Ordinal))
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "The protected immutable payload chunk is missing or invalid.");
                }

                return new CacheRelationalChunk(chunk.Offset, chunk.Data, memory);
            }, cancellationToken).ConfigureAwait(false);
            handedOff = result.IsSuccess;
            return result;
        }
        finally { if (!handedOff) { memory.Dispose(); } }
    }

    private async ValueTask ReleaseReaderAsync(string readerId)
    {
        if (readerId is null) { return; }

        var released = await this.ExecuteAsync(async (context, token) =>
        {
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var collectionId = await context.Set<CachePayloadEntity>().Where(e => context.Set<CacheReaderEntity>()
                .Any(r => r.Id == readerId && r.PayloadId == e.Id)).Select(e => e.CollectionId.Substring(0, 65)).SingleOrDefaultAsync(token).ConfigureAwait(false);
            CacheRelationalProjection.ValidateScalar(collectionId, 64, true);
            var removed = await context.Set<CacheReaderEntity>().Where(e => e.Id == readerId).ExecuteDeleteAsync(token).ConfigureAwait(false);
            if (removed != 0)
            {
                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                store.MetadataBytes -= 256;
                if (collectionId is not null)
                {
                    var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == collectionId), true, token).ConfigureAwait(false);
                    collection.MetadataBytes -= 256;
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, CancellationToken.None).ConfigureAwait(false);
        if (released.IsFailure)
        {
            CacheRelationalLog.Deferred(this.logger, this.Name, "reader-release");
        }
    }

    private sealed record ReadRow(CacheEntryEntity Entry, byte[] Inline, int? InlineLength, long? PayloadBytes, string PayloadHash);

    // Foreground access and sliding expiry.

    private readonly Lock activityGate = new();
    private readonly Dictionary<string, ForegroundActivity> activity = new(StringComparer.Ordinal);

    private void ObserveForeground(CacheProviderCollection definition, CacheEntryMetadata metadata)
    {
        if (metadata.IsNotFound) { return; }

        lock (this.activityGate)
        {
            if (Volatile.Read(ref this.disposed) != 0) { return; }

            var now = this.time.GetUtcNow().UtcTicks;
            if (this.activity.TryGetValue(metadata.Identity.Digest, out var observed))
            {
                observed.Version = metadata.Version.Value;
                observed.Ticks = now;
                return;
            }

            if (this.activity.Count >= 1024 || !ResourceCharge.TryReserve(1024, out var memory, this.ResidentFor(definition))) { return; }

            this.activity.Add(metadata.Identity.Digest, new(metadata.Identity.Digest, metadata.Version.Value, now, memory));
        }
    }

    private async ValueTask FlushActivityAsync(DbContext context, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return; }

        // Approximate eligibility observations are expendable. Every actual I/O call and sample charge remains owned until it finishes.
        ForegroundActivity[] observations;
        lock (this.activityGate)
        {
            observations = this.activity.Values.Take(Math.Min(16, budget.Remaining / 4)).ToArray();
            foreach (var observation in observations) { this.activity.Remove(observation.Id); }
        }

        try
        {
            foreach (var observation in observations)
            {
                if (!budget.CanStart) { break; }

                await context.Set<CacheEntryEntity>().Where(e => e.Id == observation.Id && e.PayloadId == observation.Version && e.LastAccessTicks <= observation.Ticks)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastAccessTicks, observation.Ticks).SetProperty(e => e.HasForegroundAccess, true), token).ConfigureAwait(false);
                budget.Use();
            }
        }
        finally
        {
            foreach (var observation in observations) { observation.Memory.Dispose(); }
        }
    }

    private void DisposeActivity()
    {
        lock (this.activityGate)
        {
            foreach (var observation in this.activity.Values) { observation.Memory.Dispose(); }

            this.activity.Clear();
        }
    }

    private sealed class ForegroundActivity(string id, string version, long ticks, ResourceCharge memory)
    {
        /// <summary>Gets the bounded canonical entry identifier whose foreground activity is retained.</summary>
        public string Id { get; } = id;

        /// <summary>Gets or sets the exact immutable payload generation associated with this foreground observation.</summary>
        public string Version { get; set; } = version;

        /// <summary>Gets or sets the latest authoritative foreground observation in UTC ticks.</summary>
        public long Ticks { get; set; } = ticks;

        /// <summary>Gets the reservation retained until this foreground observation is discarded.</summary>
        public ResourceCharge Memory { get; } = memory;
    }

    // Publication fences.

    /// <summary>Captures a persisted key/collection epoch and exact observed version before any source or staging access.</summary>
    public ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.Definition(identity);
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var fence = await this.CaptureFenceAsync(context, identity, definition, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new CachePublicationFence(fence.Id);
        }, cancellationToken);

    /// <summary>Releases one exact captured authority record. Repeated release is harmless and never resets a still-captured key epoch.</summary>
    public async ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default)
    {
        var released = await this.ExecuteAsync(async (context, token) =>
        {
            _ = this.Definition(identity);
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var stored = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheFenceEntity>().Where(e => e.Id == fence.Value && e.EntryId == identity.Digest), true, token).ConfigureAwait(false);
            if (stored is not null)
            {
                context.Remove(stored);
                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                store.MetadataBytes -= 1536;
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == stored.CollectionId), true, token).ConfigureAwait(false);
                collection.MetadataBytes -= 1536;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return released.IsSuccess ? Result.Success() : Result.Failure().WithErrors(released.Errors);
    }

    private async ValueTask<CacheFenceEntity> CaptureFenceAsync(DbContext context, CacheEntryIdentity identity,
        CacheProviderCollection definition, CancellationToken cancellationToken)
    {
        var collectionId = CollectionId(identity);
        var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == collectionId), true, cancellationToken).ConfigureAwait(false);
        var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, cancellationToken).ConfigureAwait(false);
        await this.ReclaimPressureAsync(context, store, collection, definition, identity.Digest, cancellationToken).ConfigureAwait(false);
        var keyId = KeyId(identity);
        var key = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheKeyEntity>().Where(e => e.Id == keyId), true, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            this.ReserveMetadata(store, collection, 512 + System.Text.Encoding.UTF8.GetByteCount(identity.Key));
            key = new() { Id = keyId, CollectionId = collectionId, CanonicalKey = identity.Key };
            context.Add(key);
        }
        else if (key.CollectionId != collectionId || key.CanonicalKey != identity.Key)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "A persisted key digest has a different canonical identity.");
        }

        var entry = await context.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.Id == identity.Digest)
            .Select(e => new { PayloadId = e.PayloadId.Substring(0, 33), e.PublishedTicks }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entry is not null) { CacheRelationalProjection.ValidateScalar(entry.PayloadId, 32); }

        var maximum = definition.Policies.Streaming.Lifetime > definition.Policies.Loading.SourceTimeout ?
            definition.Policies.Streaming.Lifetime : definition.Policies.Loading.SourceTimeout;
        var fence = new CacheFenceEntity { Id = Guid.NewGuid().ToString("N"), EntryId = identity.Digest, KeyId = keyId,
            CollectionId = collectionId, KeyEpoch = key.Epoch, CollectionEpoch = collection.Epoch,
            ObservedVersion = entry?.PayloadId, ObservedPublicationTicks = entry?.PublishedTicks ?? 0,
            DeadlineTicks = this.time.GetUtcNow().Add(maximum).Add(definition.Policies.Expiration.RetentionGrace).UtcTicks };
        this.ReserveMetadata(store, collection, 1536);
        context.Add(fence);
        return fence;
    }

    private async ValueTask<CacheFenceEntity> VerifyFenceAsync(DbContext context, CacheEntryIdentity identity, CachePublicationFence fence,
        CacheOwnerToken? owner, DateTimeOffset? deadline, CancellationToken cancellationToken)
    {
        var now = this.time.GetUtcNow().UtcTicks;
        if (deadline.HasValue && deadline.Value.UtcTicks <= now)
        {
            throw new CacheRelationalException(CacheErrorCode.SourceTimeout, "The fixed publication deadline ended.");
        }

        var stored = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheFenceEntity>().Where(e => e.Id == fence.Value && e.EntryId == identity.Digest), true, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.DeadlineTicks <= now)
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "The captured publication authority ended.");
        }

        var keyEpoch = await context.Set<CacheKeyEntity>().Where(e => e.Id == stored.KeyId).Select(e => (long?)e.Epoch).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var epoch = await context.Set<CacheCollectionEntity>().Where(e => e.Id == stored.CollectionId).Select(e => (long?)e.Epoch).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (keyEpoch != stored.KeyEpoch || epoch != stored.CollectionEpoch)
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "An invalidation fenced this publication.");
        }

        if (owner.HasValue && !await context.Set<CacheOwnershipEntity>().AnyAsync(e => e.EntryId == identity.Digest && e.FenceId == stored.Id &&
            e.OwnerToken == owner.Value.Value && e.State == (int)CacheOwnershipState.Owned && e.LeaseExpiryTicks > now && e.DeadlineTicks > now, cancellationToken).ConfigureAwait(false))
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "Shared source ownership is no longer authoritative.");
        }

        return stored;
    }

    // Bounded staging and immutable chunks.

    /// <summary>Reserves finite shared staging credits and creates one same-database immutable candidate, without changing the current entry.</summary>
    public ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync<ICacheProviderWriteSession>(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.Definition(request.Identity);
            if (request.MaximumStoredBytes <= 0 || request.MaximumStoredBytes > CacheSizePolicy.MaximumStoredBytes(definition.Policies) ||
                (request.Condition.CreateOnly && request.Condition.ExpectedVersion.HasValue) ||
                (request.Condition.ExpectedVersion.HasValue && !Guid.TryParseExact(request.Condition.ExpectedVersion.Value.Value, "N", out _)) ||
                (request.OperationId is not null && !Guid.TryParseExact(request.OperationId, "N", out _)))
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "The native staging request violates its finite publication contract.");
            }

            if (!ResourceCharge.TryReserve(16384, out var memory, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "Native staging metadata admission was rejected.");
            }

            var handedOff = false;
            try
            {
                await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
                var fence = await this.VerifyFenceAsync(context, request.Identity, request.Fence, request.Owner, request.Deadline, token).ConfigureAwait(false);
                if ((request.Condition.CreateOnly || request.Condition.ExpectedVersion.HasValue) && fence.PublishedSinceCapture)
                {
                    throw new CacheRelationalException(CacheErrorCode.Conflict, "A newer publication changed this captured version.");
                }

                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                var collectionId = CollectionId(request.Identity);
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == collectionId), true, token).ConfigureAwait(false);
                var credit = Math.Min(this.options.InlinePayloadThreshold, request.MaximumStoredBytes);
                this.ReserveStaging(store, collection, definition, credit);
                this.ReserveMetadata(store, collection, 8192);
                var version = CacheEntryVersion.New();
                var operationId = request.OperationId ?? Guid.NewGuid().ToString("N");
                var deadline = request.Deadline?.UtcTicks ?? this.time.GetUtcNow().Add(definition.Policies.Streaming.Lifetime).UtcTicks;
                context.Add(new CachePayloadEntity { Id = version.Value, EntryId = request.Identity.Digest, CollectionId = collectionId, OperationId = operationId,
                    State = 0, ReservedBytes = credit, MetadataBytes = 8192, CreatedTicks = this.time.GetUtcNow().UtcTicks,
                    DeadlineTicks = Math.Min(deadline, fence.DeadlineTicks), StagingOwner = this.node });
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                var session = new CacheWriteSession(this, request, definition, version, operationId, credit, memory);
                handedOff = true;
                return session;
            }
            finally { if (!handedOff) { memory.Dispose(); } }
        }, cancellationToken);

    private void ReserveStaging(CacheStoreEntity store, CacheCollectionEntity collection, CacheProviderCollection definition, long bytes)
    {
        var payloadMaximum = (long)(store.MaximumBytes * (1 - this.options.MetadataReserveRatio));
        if (bytes < 0 || bytes > payloadMaximum - store.CurrentBytes - store.RetiredBytes - store.StagingBytes ||
            bytes > store.MaximumBytes - store.CurrentBytes - store.RetiredBytes - store.StagingBytes - store.MetadataBytes ||
            bytes > collection.MaximumBytes - collection.CurrentBytes - collection.RetiredBytes - collection.StagingBytes - collection.MetadataBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "Shared staging credits exceed the provider or collection quota.");
        }

        store.StagingBytes += bytes;
        collection.StagingBytes += bytes;
    }

    private ValueTask<Result<long>> ReserveStageCreditAsync(CacheWriteSession session, long wanted, long required, CancellationToken cancellationToken) =>
        this.ExecuteAsync(async (context, token) =>
        {
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var payload = await this.StagedPayloadAsync(context, session, token).ConfigureAwait(false);
            if (wanted > payload.ReservedBytes)
            {
                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == payload.CollectionId), true, token).ConfigureAwait(false);
                var available = Math.Min((long)(store.MaximumBytes * (1 - this.options.MetadataReserveRatio)) - store.CurrentBytes - store.RetiredBytes - store.StagingBytes,
                    Math.Min(store.MaximumBytes - store.CurrentBytes - store.RetiredBytes - store.StagingBytes - store.MetadataBytes,
                        collection.MaximumBytes - collection.CurrentBytes - collection.RetiredBytes - collection.StagingBytes - collection.MetadataBytes));
                if (required - payload.ReservedBytes > available) { throw new CacheRelationalException(CacheErrorCode.Capacity, "Shared staging credits are exhausted."); }

                var delta = Math.Min(wanted - payload.ReservedBytes, available);
                this.ReserveStaging(store, collection, session.Definition, delta);
                payload.ReservedBytes += delta;
                await context.SaveChangesAsync(token).ConfigureAwait(false);
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return payload.ReservedBytes;
        }, cancellationToken);

    private ValueTask<Result<bool>> WriteStagedAsync(CacheWriteSession session, byte[] bytes, long offset, int ordinal, bool inline,
        CancellationToken cancellationToken) => this.ExecuteAsync(async (context, token) =>
        {
            await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
            var payload = await this.StagedPayloadAsync(context, session, token).ConfigureAwait(false);
            var end = checked(offset + bytes.Length);
            if (end > payload.ReservedBytes || bytes.Length > this.options.ChunkSize || (inline && (offset != 0 || bytes.Length > this.options.InlinePayloadThreshold)) ||
                (!inline && offset != payload.SizeBytes && !(offset == 0 && payload.InlineBytes is not null)))
            {
                throw new CacheRelationalException(CacheErrorCode.Contract, "The staged chunk position or reservation is invalid.");
            }

            if (inline)
            {
                payload.InlineBytes = bytes;
            }
            else
            {
                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == payload.CollectionId), true, token).ConfigureAwait(false);
                this.ReserveMetadata(store, collection, 4096);
                payload.MetadataBytes += 4096;
                payload.InlineBytes = null;
                context.Add(new CacheChunkEntity { PayloadId = payload.Id, Ordinal = ordinal, Offset = offset, Data = bytes,
                    Hash = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
            }

            payload.SizeBytes = end;
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private async ValueTask<CachePayloadEntity> StagedPayloadAsync(DbContext context, CacheWriteSession session, CancellationToken cancellationToken)
    {
        var payload = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CachePayloadEntity>().Where(e => e.Id == session.Version.Value && e.OperationId == session.OperationId), true, cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.State != 0 || payload.StagingOwner != this.node || payload.DeadlineTicks <= this.time.GetUtcNow().UtcTicks)
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "The exact staged version is no longer writable.");
        }

        return payload;
    }

    private ValueTask<Result<Stream>> OpenStagedAsync(CacheWriteSession session, CancellationToken cancellationToken) =>
        this.ExecuteAsync<Stream>(async (context, token) =>
        {
            if (!ResourceCharge.TryReserve(512 * CacheDefaults.KiB + this.options.InlinePayloadThreshold, out var memory,
                this.transient, this.ResidentFor(session.Definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "Staging-reader admission was rejected.");
            }

            var handedOff = false;
            try
            {
                await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
                var payload = await this.StagedPayloadAsync(context, session, token).ConfigureAwait(false);
                if (payload.SizeBytes != session.Destination.Written) { throw new CacheRelationalException(CacheErrorCode.Corruption, "The completed staging size is inconsistent."); }

                string readerId = null;
                if (payload.InlineBytes is null && payload.SizeBytes != 0)
                {
                    readerId = Guid.NewGuid().ToString("N");
                    var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                    var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == payload.CollectionId), true, token).ConfigureAwait(false);
                    this.ReserveMetadata(store, collection, 256);
                    context.Add(new CacheReaderEntity { Id = readerId, PayloadId = payload.Id,
                        ExpiryTicks = this.time.GetUtcNow().Add(session.Definition.Policies.Streaming.Lifetime).Add(this.options.AttemptTimeout).UtcTicks });
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                var minimal = new CacheEntryMetadata { Identity = session.Request.Identity, Version = session.Version, StoredSizeBytes = payload.SizeBytes };
                var handle = new CacheRelationalReadHandle(minimal, payload.InlineBytes ?? (payload.SizeBytes == 0 ? [] : null), memory,
                    (offset, readToken) => this.ReadChunkAsync(session.Definition, payload.Id, readerId, offset, readToken), () => this.ReleaseReaderAsync(readerId));
                handedOff = true;
                return handle.Stream;
            }
            finally { if (!handedOff) { memory.Dispose(); } }
        }, cancellationToken);

    private ValueTask<Result<bool>> AbortStagedAsync(CacheWriteSession session) => this.ExecuteAsync(async (context, token) =>
    {
        await using var transaction = await LockStoreAsync(context, token).ConfigureAwait(false);
        var payload = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CachePayloadEntity>().Where(e => e.Id == session.Version.Value && e.OperationId == session.OperationId), true, token).ConfigureAwait(false);
        if (payload is null || payload.State is 1 or 2) { return false; }

        if (await context.Set<CacheReaderEntity>().AnyAsync(e => e.PayloadId == payload.Id, token).ConfigureAwait(false))
        {
            payload.State = 3;
            payload.DeadlineTicks = this.time.GetUtcNow().UtcTicks;
        }
        else
        {
            var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
            var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == payload.CollectionId), true, token).ConfigureAwait(false);
            store.StagingBytes -= payload.ReservedBytes;
            collection.StagingBytes -= payload.ReservedBytes;
            store.MetadataBytes -= payload.MetadataBytes;
            collection.MetadataBytes -= payload.MetadataBytes;
            context.Remove(payload);
        }

        await context.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return true;
    }, CancellationToken.None);

    // Write session lifecycle.

    private sealed class CacheWriteSession : ICacheProviderWriteSession
    {
        private readonly EntityFrameworkCacheStoreProvider owner;
        private readonly ResourceCharge memory;
        private readonly Lock gate = new();
        private Task<Result<CacheWriteOutcome>> publication;
        private Task disposal;
        private bool disposed;

        /// <summary>Creates one staged immutable generation and retains its reserved memory until actual session disposal.</summary>
        public CacheWriteSession(EntityFrameworkCacheStoreProvider owner, CacheProviderWriteRequest request, CacheProviderCollection definition,
            CacheEntryVersion version, string operationId, long credit, ResourceCharge memory)
        {
            this.owner = owner;
            this.Request = request;
            this.Definition = definition;
            this.Version = version;
            this.OperationId = operationId;
            this.memory = memory;
            this.Destination = new(owner, this, credit);
        }

        /// <summary>Gets the original bounded write request, including its captured publication fence and condition.</summary>
        public CacheProviderWriteRequest Request { get; }

        /// <summary>Gets the validated collection contract and policy used by this staged generation.</summary>
        public CacheProviderCollection Definition { get; }

        /// <summary>Gets the provider-owned bounded staging stream for this unpublished generation.</summary>
        public CacheStagingStream Destination { get; }

        /// <inheritdoc />
        public Stream Stream => this.Destination;

        /// <inheritdoc />
        public string OperationId { get; }

        /// <inheritdoc />
        public CacheEntryVersion Version { get; }

        /// <inheritdoc />
        public async ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default)
        {
            lock (this.gate)
            {
                if (this.disposed || this.publication is not null)
                {
                    return Result<Stream>.Failure(Error(CacheErrorCode.Contract, "This staging session is no longer available for an independent read."));
                }
            }

            await this.Destination.SealAsync(cancellationToken).ConfigureAwait(false);
            return await this.owner.OpenStagedAsync(this, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default)
        {
            lock (this.gate)
            {
                if (this.disposed || this.publication is not null)
                {
                    return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(Error(CacheErrorCode.Contract, "A staging session permits one publication attempt.")));
                }

                this.publication = this.PublishCoreAsync(metadata, cancellationToken);
                return new(this.publication);
            }
        }

        private async Task<Result<CacheWriteOutcome>> PublishCoreAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken)
        {
            try
            {
                await this.Destination.SealAsync(cancellationToken).ConfigureAwait(false);
                return await this.owner.CommitWriteAsync(this, metadata, cancellationToken).ConfigureAwait(false);
            }
            catch (CacheTransferException exception) { return Result<CacheWriteOutcome>.Failure(Error(exception.Code, "The staged destination could not complete.")); }
            catch (InvalidOperationException) { return Result<CacheWriteOutcome>.Failure(Error(CacheErrorCode.Contract, "A failed or prematurely closed staging destination cannot publish.")); }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (this.gate)
            {
                if (this.disposal is not null) { return new(this.disposal); }

                this.disposed = true;
                this.disposal = this.DisposeCoreAsync(this.publication);
                return new(this.disposal);
            }
        }

        private async Task DisposeCoreAsync(Task<Result<CacheWriteOutcome>> pending)
        {
            var confirmed = false;
            try
            {
                if (pending is not null)
                {
                    try
                    {
                        var result = await pending.ConfigureAwait(false);
                        confirmed = result.IsSuccess && result.Value.StorageOutcome == CacheStorageOutcome.Stored;
                    }
                    catch { /* The exact operation/version is still checked under native authority before any abort. */ }
                }

                await this.Destination.DisposeAsync().ConfigureAwait(false);
                if (!confirmed)
                {
                    var released = await this.owner.AbortStagedAsync(this).ConfigureAwait(false);
                    if (released.IsFailure)
                    {
                        CacheRelationalLog.Deferred(this.owner.logger, this.owner.Name, "staging-release");
                    }
                }
            }
            finally { this.memory.Dispose(); }
        }
    }

    private sealed class CacheStagingStream(EntityFrameworkCacheStoreProvider owner, CacheWriteSession session, long credit) : Stream
    {
        private readonly SemaphoreSlim gate = new(1);
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private ResourceCharge memory;
        private byte[] buffer;
        private int buffered;
        private long written;
        private long storedOffset;
        private int ordinal;
        private long reserved = credit;
        private bool chunked;
        private bool closed;
        private bool failed;
        private bool sealedStream;

        /// <summary>Gets the logical staged byte count after completed native appends.</summary>
        public long Written => this.written;

        /// <summary>Gets the final stored-byte SHA-256 digest after sealing completes.</summary>
        public string StoredHash { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanWrite => !this.closed && !this.failed && !this.sealedStream;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => this.written;

        /// <inheritdoc />
        public override long Position { get => this.written; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.CheckWritable();
                cancellationToken.ThrowIfCancellationRequested();
                if (input.IsEmpty) { return; }

                if (input.Length > session.Request.MaximumStoredBytes - this.written) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                var target = this.written + input.Length;
                if (target > this.reserved)
                {
                    var remainder = (target - 1) % owner.options.ChunkSize;
                    var wanted = target + Math.Min(session.Request.MaximumStoredBytes - target, owner.options.ChunkSize - remainder - 1);
                    var reservedResult = await owner.ReserveStageCreditAsync(session, wanted, target, cancellationToken).ConfigureAwait(false);
                    if (reservedResult.IsFailure) { throw Transfer(reservedResult); }

                    this.reserved = reservedResult.Value;
                }

                var capacity = target <= owner.options.InlinePayloadThreshold ? owner.options.InlinePayloadThreshold : owner.options.ChunkSize;
                this.EnsureBuffer(capacity);
                while (!input.IsEmpty)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var count = Math.Min(input.Length, this.buffer.Length - this.buffered);
                    input.Span[..count].CopyTo(this.buffer.AsSpan(this.buffered));
                    this.hash.AppendData(input.Span[..count]);
                    this.buffered += count;
                    this.written += count;
                    input = input[count..];
                    if (this.buffered == this.buffer.Length && (this.chunked || target > owner.options.InlinePayloadThreshold))
                    {
                        await this.FlushCoreAsync(cancellationToken, forceChunks: true).ConfigureAwait(false);
                    }
                }
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        private void EnsureBuffer(int capacity)
        {
            if (this.buffer is not null && this.buffer.Length >= capacity) { return; }

            if (!ResourceCharge.TryReserve(capacity, out var charge, owner.transient, owner.ResidentFor(session.Definition)))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            var replacement = GC.AllocateUninitializedArray<byte>(capacity);
            if (this.buffer is not null) { this.buffer.AsSpan(0, this.buffered).CopyTo(replacement); }

            this.memory?.Dispose();
            this.buffer = replacement;
            this.memory = charge;
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.CheckWritable();
                await this.FlushCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        private async Task FlushCoreAsync(CancellationToken cancellationToken, bool forceChunks = false)
        {
            if (this.buffered == 0) { return; }

            if (!ResourceCharge.TryReserve(this.buffered, out var copyMemory, owner.transient, owner.ResidentFor(session.Definition)))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            using (copyMemory)
            {
                var inline = !forceChunks && !this.chunked && this.written <= owner.options.InlinePayloadThreshold;
                var snapshot = this.buffer.AsSpan(0, this.buffered).ToArray();
                var stored = await owner.WriteStagedAsync(session, snapshot, inline ? 0 : this.storedOffset, this.ordinal, inline, cancellationToken).ConfigureAwait(false);
                if (stored.IsFailure) { throw Transfer(stored); }

                if (!inline)
                {
                    this.chunked = true;
                    this.storedOffset += this.buffered;
                    this.ordinal++;
                    this.buffered = 0;
                }
            }
        }

        /// <summary>Flushes the final bounded chunk and freezes the staging digest before publication.</summary>
        public async Task SealAsync(CancellationToken cancellationToken)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (this.sealedStream) { return; }

                this.CheckWritable();
                await this.FlushCoreAsync(cancellationToken).ConfigureAwait(false);
                this.StoredHash = Convert.ToHexStringLower(this.hash.GetHashAndReset());
                this.sealedStream = true;
                this.buffer = null;
                this.memory?.Dispose();
                this.memory = null;
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            this.closed = true;
            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                this.buffer = null;
                this.memory?.Dispose();
                this.memory = null;
                this.hash.Dispose();
            }
            finally { this.gate.Release(); }
        }

        private void CheckWritable()
        {
            if (!this.CanWrite) { throw new InvalidOperationException("The staged destination is closed, failed or frozen."); }
        }

        private static CacheTransferException Transfer(IResult result) => new(result.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.ProviderUnavailable);

        protected override void Dispose(bool disposing)
        {
            if (disposing) { this.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        /// <inheritdoc />
        public override void Flush() => this.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // Atomic publication.

    private async ValueTask<Result<CacheWriteOutcome>> CommitWriteAsync(CacheWriteSession session, CacheEntryMetadata metadata, CancellationToken cancellationToken)
    {
        var result = await this.ExecuteAsync(async (context, token) =>
        {
            if (metadata is null) { throw new CacheRelationalException(CacheErrorCode.Contract, "The staged publication metadata is required."); }

            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var payload = await this.StagedPayloadAsync(context, session, token).ConfigureAwait(false);
            if (payload.SizeBytes != metadata.StoredSizeBytes) { throw new CacheRelationalException(CacheErrorCode.Corruption, "The persisted staging size is inconsistent."); }

            var fence = await this.VerifyFenceAsync(context, session.Request.Identity, session.Request.Fence, session.Request.Owner, session.Request.Deadline, token).ConfigureAwait(false);
            var previous = await ReadEntryAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == metadata.Identity.Digest), true, token).ConfigureAwait(false);
            if (previous is not null) { _ = Metadata(previous, metadata.Identity); }

            var condition = session.Request.Condition;
            if ((condition.CreateOnly && (fence.PublishedSinceCapture || (previous is { IsNotFound: false } && previous.ExpiryTicks > this.time.GetUtcNow().UtcTicks))) ||
                (condition.ExpectedVersion.HasValue && (fence.PublishedSinceCapture ||
                    (previous is null ? fence.ObservedVersion != condition.ExpectedVersion.Value.Value : previous.PayloadId != condition.ExpectedVersion.Value.Value))))
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "The current or captured expected version changed.");
            }

            var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
            var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == payload.CollectionId), true, token).ConfigureAwait(false);
            var oldPayload = previous?.PayloadId is { } oldPayloadId ?
                await CacheRelationalProjection.SingleAsync(context, context.Set<CachePayloadEntity>().Where(e => e.Id == oldPayloadId), true, token).ConfigureAwait(false) : null;

            if (previous is not null) { await context.Set<CacheDimensionEntity>().Where(e => e.EntryId == previous.Id).ExecuteDeleteAsync(token).ConfigureAwait(false); }

            await context.Set<CacheFenceEntity>().Where(e => e.EntryId == metadata.Identity.Digest)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.PublishedSinceCapture, true), token).ConfigureAwait(false);
            // Validation/transforms and native authority waits cannot consume the entry's selected lifetime.
            metadata = CacheExpirationPolicy.Rebase(metadata, this.time.GetUtcNow()) with { RefreshFailureCount = 0, RefreshNotBefore = null };
            var encoded = PrepareMetadata(session, metadata);
            var count = previous is null ? 1 : 0;
            var metadataBytes = checked(8192L + Encoding.UTF8.GetByteCount(encoded) * 2L + metadata.Dimensions.Sum(d => (d.Values.Count + 1L) * 256));
            if (store.CurrentEntries + count > store.MaximumEntries || collection.CurrentEntries + count > collection.MaximumEntries ||
                metadataBytes - (previous?.MetadataBytes ?? 0) + metadata.StoredSizeBytes - payload.ReservedBytes > collection.MaximumBytes - collection.CurrentBytes -
                    collection.RetiredBytes - collection.StagingBytes - collection.MetadataBytes)
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "The complete entry exceeds shared metadata or count admission.");
            }

            var oldBytes = previous?.StoredBytes ?? 0;
            store.CurrentBytes += metadata.StoredSizeBytes - oldBytes;
            collection.CurrentBytes += metadata.StoredSizeBytes - oldBytes;
            store.RetiredBytes += oldBytes;
            collection.RetiredBytes += oldBytes;
            store.StagingBytes -= payload.ReservedBytes;
            collection.StagingBytes -= payload.ReservedBytes;
            store.CurrentEntries += count;
            collection.CurrentEntries += count;
            store.MetadataBytes -= previous?.MetadataBytes ?? 0;
            collection.MetadataBytes -= previous?.MetadataBytes ?? 0;
            this.ReserveMetadata(store, collection, metadataBytes);
            collection.Revision++;
            if (oldPayload is not null) { oldPayload.State = 2; }

            payload.State = 1;
            payload.ReservedBytes = metadata.StoredSizeBytes;
            payload.StoredHash = metadata.StoredHash;
            var entry = previous ?? new CacheEntryEntity { Id = metadata.Identity.Digest };
            entry.CollectionId = payload.CollectionId;
            entry.ContractId = ContractId(metadata.Identity);
            entry.KeyId = KeyId(metadata.Identity);
            entry.PayloadId = payload.Id;
            var prefix = Convert.ToHexString(Encoding.UTF8.GetBytes(metadata.Identity.Key));
            entry.KeyPrefix = prefix[..Math.Min(prefix.Length, 512)];
            entry.MetadataJson = encoded;
            entry.PublishedTicks = metadata.PublishedAt.UtcTicks;
            entry.PublicationSequence = collection.Revision;
            entry.ExpiryTicks = metadata.ExpiresAt.UtcTicks;
            entry.RetentionGraceTicks = session.Definition.Policies.Expiration.RetentionGrace.Ticks;
            entry.PurgeAfterTicks = metadata.ExpiresAt.Add(session.Definition.Policies.Expiration.RetentionGrace).UtcTicks;
            entry.AbsoluteExpiryTicks = metadata.AbsoluteExpiresAt.UtcTicks;
            entry.LastAccessTicks = metadata.LastAccessAt.UtcTicks;
            entry.HasForegroundAccess = metadata.HasForegroundAccess;
            entry.StoredBytes = metadata.StoredSizeBytes;
            entry.MetadataBytes = metadataBytes;
            entry.Priority = (int)metadata.Priority;
            entry.IsNotFound = metadata.IsNotFound;
            var lead = Math.Min(session.Definition.Policies.Refresh.RefreshAhead.Ticks, (metadata.AbsoluteExpiresAt - metadata.PublishedAt).Ticks / 5);
            entry.RefreshDueTicks = !metadata.IsNotFound && session.Definition.Policies.Refresh.Enabled ? metadata.AbsoluteExpiresAt.UtcTicks - lead : null;
            entry.RefreshBackoffTicks = null;
            if (previous is null) { context.Add(entry); }

            foreach (var dimension in metadata.Dimensions)
            {
                var nameHash = Hash(dimension.Name);
                context.Add(new CacheDimensionEntity { EntryId = entry.Id, CollectionId = entry.CollectionId, NameHash = nameHash, Ordinal = -1,
                    Kind = (int)dimension.Kind, Form = (int)dimension.Form });
                if (dimension.Form == CacheDimensionForm.Interval)
                {
                    context.Add(new CacheDimensionEntity { EntryId = entry.Id, CollectionId = entry.CollectionId, NameHash = nameHash, Ordinal = 0,
                        Kind = (int)dimension.Kind, Form = (int)dimension.Form, NumericValue = Number(dimension.Values[0]), IntervalEnd = Number(dimension.Values[1]) });
                }
                else
                {
                    for (var index = 0; index < dimension.Values.Count; index++)
                    {
                        var value = dimension.Values[index];
                        context.Add(new CacheDimensionEntity { EntryId = entry.Id, CollectionId = entry.CollectionId, NameHash = nameHash, Ordinal = index,
                            Kind = (int)value.Kind, Form = (int)dimension.Form, IsNull = value.IsNull, ValueHash = DimensionHash(value), NumericValue = Number(value) });
                    }
                }
            }

            await context.SaveChangesAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new CacheWriteOutcome(CacheStorageOutcome.Stored, metadata.Version, metadata);
        }, cancellationToken).ConfigureAwait(false);
        return result.IsFailure && result.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.Indeterminate) ?
            Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Indeterminate)) : result;
    }

    private static string PrepareMetadata(CacheWriteSession session, CacheEntryMetadata metadata)
    {
        var policy = session.Definition.Policies;
        if (metadata is null || metadata.Identity != session.Request.Identity || metadata.Version != session.Version || metadata.StoredSizeBytes != session.Destination.Written ||
            metadata.LogicalSizeBytes < 0 || metadata.LogicalSizeBytes > policy.Limits.MaxLogicalBytes || metadata.EncodedSizeBytes < 0 ||
            metadata.EncodedSizeBytes > policy.Limits.MaxEncodedBytes || metadata.DecodedSizeBytes is < 0 || metadata.DecodedSizeBytes > policy.Limits.MaxDecodedBytes ||
            metadata.ItemCount is < 0 || !Enum.IsDefined(metadata.Priority) || metadata.EnvelopeVersion <= 0 || metadata.CodecVersion <= 0 ||
            metadata.ContractId != session.Definition.ContractId || metadata.ContentType?.Length > 512 ||
            CacheIdentityNormalizer.Key(metadata.DisplayKey ?? metadata.Identity.Key) != metadata.Identity.Key)
        {
            throw new CacheRelationalException(CacheErrorCode.Contract, "The staged metadata violates its exact identity, size or value contract.");
        }

        _ = CacheIdentityNormalizer.Name(metadata.CodecId);
        _ = CacheIdentityNormalizer.Name(metadata.Compression ?? "none");
        var dimensions = CacheSelectionValidator.ValidateDimensions(metadata.Dimensions, session.Definition.Dimensions);
        var maximum = policy.Expiration.SlidingEnabled ? policy.Expiration.AbsoluteCap : policy.Expiration.AbsoluteTtl;
        var lifetime = metadata.AbsoluteExpiresAt - metadata.PublishedAt;
        if (dimensions.IsFailure || lifetime <= TimeSpan.Zero || lifetime > maximum || metadata.ExpiresAt <= metadata.PublishedAt || metadata.ExpiresAt > metadata.AbsoluteExpiresAt ||
            (metadata.SlidingIdleTimeout.HasValue && (!policy.Expiration.SlidingEnabled || metadata.SlidingIdleTimeout <= TimeSpan.Zero ||
                metadata.SlidingIdleTimeout > policy.Expiration.SlidingIdleTimeout)) ||
            (metadata.IsNotFound && (!policy.NegativeCaching.Enabled || lifetime > policy.NegativeCaching.Ttl || metadata.SlidingIdleTimeout.HasValue ||
                metadata.StoredSizeBytes != 0 || metadata.LogicalSizeBytes != 0 || metadata.EncodedSizeBytes != 0 || metadata.PayloadHash is not null || metadata.StoredHash is not null)) ||
            (!metadata.IsNotFound && (metadata.StoredHash != session.Destination.StoredHash || metadata.PayloadHash?.Length != 64)))
        {
            throw new CacheRelationalException(CacheErrorCode.Contract, "The staged freshness, dimensions or integrity facts violate the collection policy.");
        }

        var encoded = Serialize(metadata);
        if (Encoding.UTF8.GetByteCount(encoded) > policy.Limits.MaxEntryMetadataBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The complete entry metadata exceeds the configured byte ceiling.");
        }

        return encoded;
    }

    private static string DimensionHash(CacheDimensionValue value) => Hash(((int)value.Kind).ToString(CultureInfo.InvariantCulture) + ":" + (value.IsNull ? "null" : "value:" + value.CanonicalValue));

    private static long? Number(CacheDimensionValue value) => !value.IsNull && value.Kind is CacheDimensionKind.Integer or CacheDimensionKind.Timestamp ?
        long.Parse(value.CanonicalValue, CultureInfo.InvariantCulture) : null;

    /// <summary>Reconciles one exact operation/version without replaying its mutation or source stream.</summary>
    public ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) { throw new CacheRelationalException(CacheErrorCode.Validation, "An exact issued write-operation identity is required."); }

            var payload = await context.Set<CachePayloadEntity>().AsNoTracking().Where(e => e.OperationId == operationId)
                .Select(e => new { Id = e.Id.Substring(0, 33), EntryId = e.EntryId.Substring(0, 65), e.State }).SingleOrDefaultAsync(token).ConfigureAwait(false);
            if (payload is null) { throw new CacheRelationalException(CacheErrorCode.NotFound, "This bounded reconciliation record is unavailable; no publication is inferred."); }

            CacheRelationalProjection.ValidateScalar(payload.Id, 32);
            CacheRelationalProjection.ValidateScalar(payload.EntryId, 64);

            if (payload.State is 1 or 2)
            {
                var entry = await ReadEntryAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == payload.EntryId && e.PayloadId == payload.Id), false, token).ConfigureAwait(false);
                return new CacheWriteOutcome(CacheStorageOutcome.Stored, new(payload.Id), entry is null ? null : Metadata(entry));
            }

            return new CacheWriteOutcome(payload.State == 3 ? CacheStorageOutcome.Failed : CacheStorageOutcome.Indeterminate);
        }, cancellationToken);

    // Exact removal.

    /// <summary>Fences one logical key across retained contracts, even when it has no current entry.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) => this.RemoveAsync(new CacheProviderRemoval(identity, condition), cancellationToken);

    /// <summary>Checks exact acquired versions and optional source authority before any key fence or deletion is changed.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            _ = this.Definition(request.Identity);
            if (request.Condition.CreateOnly || (request.OnlyExpectedVersion && !request.Condition.ExpectedVersion.HasValue) ||
                (request.Owner.HasValue && !request.Fence.HasValue))
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "This exact removal requires a permitted expected-version/fence condition.");
            }

            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            if (request.Deadline.HasValue && request.Deadline.Value <= this.time.GetUtcNow())
            {
                throw new CacheRelationalException(CacheErrorCode.SourceTimeout, "The fixed source removal deadline ended.");
            }

            if (request.Fence.HasValue)
            {
                await this.VerifyFenceAsync(context, request.Identity, request.Fence.Value, request.Owner, request.Deadline, token).ConfigureAwait(false);
            }

            var identity = request.Identity;
            if (request.Condition.ExpectedVersion.HasValue)
            {
                var current = await context.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.Id == identity.Digest)
                    .Select(e => e.PayloadId == null ? null : e.PayloadId.Substring(0, 33)).SingleOrDefaultAsync(token).ConfigureAwait(false);
                CacheRelationalProjection.ValidateScalar(current, 32, true);
                if (current != request.Condition.ExpectedVersion.Value.Value)
                {
                    throw new CacheRelationalException(CacheErrorCode.Conflict, "The expected cache version changed; this removal had no effect.");
                }
            }

            var keyId = KeyId(identity);
            var collectionId = CollectionId(identity);
            var key = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheKeyEntity>().Where(e => e.Id == keyId), true, token).ConfigureAwait(false);
            if (key is not null)
            {
                if (key.CanonicalKey != identity.Key || key.CollectionId != collectionId)
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "The logical key digest has a different canonical identity.");
                }

                key.Epoch = checked(key.Epoch + 1);
            }

            await context.Set<CacheOwnershipEntity>().Where(e => e.KeyId == keyId && e.State == (int)CacheOwnershipState.Owned)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.State, (int)CacheOwnershipState.Lost), token).ConfigureAwait(false);
            var entries = context.Set<CacheEntryEntity>().Where(e => e.CollectionId == collectionId && e.KeyId == keyId);
            if (request.OnlyExpectedVersion) { entries = entries.Where(e => e.Id == identity.Digest); }

            var affected = await this.RetireEntriesAsync(context, entries, collectionId, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new CacheInvalidationOutcome { Completed = true, AffectedCount = affected.Count, CountIsExact = true };
        }, cancellationToken);

    private async ValueTask<Retirement> RetireEntriesAsync(DbContext context, IQueryable<CacheEntryEntity> entries, string collectionId,
        CancellationToken cancellationToken)
    {
        var affected = await entries.GroupBy(_ => 1).Select(g => new Retirement(g.LongCount(), g.Sum(e => e.StoredBytes), g.Sum(e => e.MetadataBytes)))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? new(0, 0, 0);
        var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, cancellationToken).ConfigureAwait(false);
        var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == collectionId), true, cancellationToken).ConfigureAwait(false);
        collection.Revision++;
        if (affected.Count == 0) { return affected; }

        var payloadIds = entries.Where(e => e.PayloadId != null).Select(e => e.PayloadId);
        await context.Set<CachePayloadEntity>().Where(e => payloadIds.Contains(e.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.State, 2), cancellationToken).ConfigureAwait(false);
        var removed = await entries.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (removed != affected.Count)
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "The bounded retirement snapshot changed before deletion.");
        }

        store.CurrentBytes -= affected.Bytes;
        collection.CurrentBytes -= affected.Bytes;
        store.RetiredBytes += affected.Bytes;
        collection.RetiredBytes += affected.Bytes;
        store.CurrentEntries -= affected.Count;
        collection.CurrentEntries -= affected.Count;
        store.MetadataBytes -= affected.MetadataBytes;
        collection.MetadataBytes -= affected.MetadataBytes;
        return affected;
    }

    /// <summary>Returns authoritative aggregate current, staged, retired and conservatively charged metadata counters without reading payloads.</summary>
    public ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) => this.ExecuteAsync(async (context, token) =>
    {
        var quota = await context.Set<CacheStoreEntity>().AsNoTracking().Where(e => e.Id == "root")
            .Select(e => new CacheProviderQuota(e.CurrentBytes, e.RetiredBytes, e.StagingBytes, e.MetadataBytes, e.CurrentEntries, e.MaximumBytes, false))
            .SingleAsync(token).ConfigureAwait(false);
        return quota;
    }, cancellationToken);

    private sealed record Retirement(long Count, long Bytes, long MetadataBytes);

    // Portable indexed selections.

    // SQL only narrows indexed candidates. The shared evaluator verifies canonical identity,
    // hash collisions, exact string/null/form semantics, and every residual predicate.
    private sealed class CacheSelectionQuery(DbContext context, string collectionId, long cutoff)
    {
        /// <summary>Builds an indexed collection and cutoff-constrained candidate query for portable residual selection.</summary>
        public IQueryable<CacheEntryEntity> Build(CacheSelection selection)
        {
            var basis = context.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.CollectionId == collectionId && e.PublishedTicks <= cutoff);
            if (selection.IsEmpty) { return basis.Where(_ => false); }

            switch (selection.Kind)
            {
                case CacheSelectionKind.Key:
                    // Scope is already canonical; the logical key digest deliberately omits contract version.
                    return basis.Where(e => e.KeyId == this.KeyDigest(selection.Text));
                case CacheSelectionKind.Prefix:
                    var prefix = Convert.ToHexString(Encoding.UTF8.GetBytes(selection.Text));
                    prefix = prefix[..Math.Min(prefix.Length, 512)];
                    var names = context.GetService<ISqlGenerationHelper>();
                    var table = this.EntryTable(names);
                    var scopeColumn = names.DelimitIdentifier("CollectionId");
                    var keyColumn = names.DelimitIdentifier("KeyPrefix");
                    // ASCII hex has only 0-9/A-F. The upper sentinel includes every extension
                    // of this prefix and excludes the next prefix, using the native binary index.
#pragma warning disable EF1002 // Only startup-validated, provider-quoted identifiers are interpolated; every data value is a binary SQL parameter.
                    return context.Set<CacheEntryEntity>().FromSqlRaw($"SELECT * FROM {table} WHERE {scopeColumn} = {{0}} AND {keyColumn} >= {{1}} AND {keyColumn} < {{2}}",
                            Encoding.ASCII.GetBytes(collectionId), Encoding.ASCII.GetBytes(prefix), Encoding.ASCII.GetBytes(prefix + "G"))
                        .AsNoTracking().Where(e => e.PublishedTicks <= cutoff);
#pragma warning restore EF1002
                case CacheSelectionKind.And:
                    foreach (var child in selection.Children)
                    {
                        var ids = this.Build(child).Select(e => e.Id);
                        basis = basis.Where(e => ids.Contains(e.Id));
                    }

                    return basis;
                case CacheSelectionKind.Or:
                    IQueryable<string> union = null;
                    foreach (var child in selection.Children)
                    {
                        var ids = this.Build(child).Select(e => e.Id);
                        union = union is null ? ids : union.Union(ids);
                    }

                    return union is null ? basis.Where(_ => false) : basis.Where(e => union.Contains(e.Id));
                case CacheSelectionKind.PublishedRange:
                    var publishedLower = Number(selection.Values[0]).Value;
                    var publishedUpper = Number(selection.Values[1]).Value;
                    return basis.Where(e => e.PublishedTicks >= publishedLower && e.PublishedTicks < publishedUpper);
                case CacheSelectionKind.StoredSizeRange:
                    var sizeLower = Number(selection.Values[0]).Value;
                    var sizeUpper = Number(selection.Values[1]).Value;
                    return basis.Where(e => e.StoredBytes >= sizeLower && e.StoredBytes < sizeUpper);
                case CacheSelectionKind.Equal:
                case CacheSelectionKind.Contains:
                case CacheSelectionKind.ContainsAny:
                    var nameHash = Hash(selection.Name);
                    var hashes = selection.Values.Select(DimensionHash).ToArray();
                    var kind = (int)selection.Values[0].Kind;
                    var form = selection.Kind == CacheSelectionKind.Equal ? (int)CacheDimensionForm.Scalar : (int)CacheDimensionForm.Set;
                    return basis.Where(e => context.Set<CacheDimensionEntity>().Any(d => d.EntryId == e.Id && d.CollectionId == collectionId &&
                        d.NameHash == nameHash && d.Kind == kind && d.Form == form && d.Ordinal >= 0 && hashes.Contains(d.ValueHash)));
                case CacheSelectionKind.Overlap:
                    var intervalName = Hash(selection.Name);
                    var intervalKind = (int)selection.Values[0].Kind;
                    var lower = Number(selection.Values[0]).Value;
                    var upper = Number(selection.Values[1]).Value;
                    return basis.Where(e => context.Set<CacheDimensionEntity>().Any(d => d.EntryId == e.Id && d.CollectionId == collectionId &&
                        d.NameHash == intervalName && d.Kind == intervalKind && d.Form == (int)CacheDimensionForm.Interval && d.Ordinal == 0 &&
                        d.NumericValue < upper && d.IntervalEnd > lower));
                case CacheSelectionKind.Present:
                    var presentName = Hash(selection.Name);
                    return basis.Where(e => context.Set<CacheDimensionEntity>().Any(d => d.EntryId == e.Id && d.CollectionId == collectionId && d.NameHash == presentName && d.Ordinal == -1));
                default:
                    // Globs and omission retain the bounded collection candidate walk. Narrowing
                    // omission with a dimension hash could incorrectly exclude a collision.
                    return basis;
            }
        }

        /// <summary>Adds a binary ordinal continuation predicate without relying on database string collation.</summary>
        public IQueryable<CacheEntryEntity> After(IQueryable<CacheEntryEntity> candidates, string after)
        {
            if (after is null) { return candidates; }

            var names = context.GetService<ISqlGenerationHelper>();
            var table = this.EntryTable(names);
            var id = names.DelimitIdentifier("Id");
#pragma warning disable EF1002 // The table/column identifiers are validated and provider-quoted; the cursor value is a binary SQL parameter.
            var tail = context.Set<CacheEntryEntity>().FromSqlRaw($"SELECT * FROM {table} WHERE {id} > {{0}}", Encoding.ASCII.GetBytes(after)).Select(e => e.Id);
#pragma warning restore EF1002
            return candidates.Where(e => tail.Contains(e.Id));
        }

        /// <summary>Gets the canonical namespace used to narrow exact-key candidates.</summary>
        public string Namespace { get; init; }

        /// <summary>Gets the canonical collection used to narrow exact-key candidates.</summary>
        public string Collection { get; init; }

        private string EntryTable(ISqlGenerationHelper names)
        {
            var entity = context.Model.FindEntityType(typeof(CacheEntryEntity));
            var schema = context.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true ? null : entity.GetSchema();

            return names.DelimitIdentifier(entity.GetTableName(), schema);
        }

        private string KeyDigest(string key) => KeyId(new(this.Namespace, this.Collection, "logical-key", key));
    }

    // Metadata queries and continuations.

    /// <summary>Returns one bounded indexed metadata page with canonical residual checks, fixed cutoff and store-signed continuation.</summary>
    public ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(query);
            var definition = this.ScopeDefinition(query.Namespace, query.Collection);
            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var workMemory, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded metadata work admission was rejected.");
            }

            using (workMemory)
            {
                var scope = await this.QueryScopeAsync(context, definition, query.Selection, query.Cutoff, token).ConfigureAwait(false);
                this.ValidatePage(scope, query);
                var fingerprint = SelectionFingerprint(query.Selection, token);
                var cursor = query.Continuation is null ? new QueryCheckpoint(2, scope.Id, fingerprint, query.Cutoff.UtcTicks, "query", null, scope.Revision, 0, false, scope.Revision) :
                    this.DecodeQueryCursor(query.Continuation, scope.Id, fingerprint, query.Cutoff, "query");
                var page = await this.ReadMetadataPageAsync(context, scope, query, cursor, token).ConfigureAwait(false);
                return page.Page with { Continuation = page.Page.Completed ? null : this.EncodeQueryCursor(page.Cursor) };
            }
        }, cancellationToken);

    private CacheProviderCollection ScopeDefinition(string @namespace, string collection)
    {
        this.CheckAvailable();
        var space = CacheIdentityNormalizer.Namespace(@namespace);
        var name = CacheIdentityNormalizer.Name(collection);
        return this.definitions.Values.FirstOrDefault(d => d.Namespace == space && d.Collection == name && d.Policies.Enabled) ??
            throw new CacheRelationalException(CacheErrorCode.Contract, "The logical cache collection is unavailable.");
    }

    private async ValueTask<QueryScope> QueryScopeAsync(DbContext context, CacheProviderCollection definition, CacheSelection selection,
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
        var id = CollectionId(identity);
        var collection = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == id), false, cancellationToken).ConfigureAwait(false);
        if (collection is null || collection.CanonicalIdentity != identity.CollectionIdentity)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The persisted collection identity is inconsistent.");
        }

        CacheSelectionEvaluator evaluator = null;
        var count = 0;
        // Stream retained schemas one at a time. The evaluator only retains the selection/patterns;
        // schema validation still covers every retained version before any caller can mutate.
        await foreach (var contract in context.Set<CacheContractEntity>().AsNoTracking().Where(e => e.CollectionId == id)
            .Select(e => new { SchemaJson = e.SchemaJson.Substring(0, 16385), e.MetadataFormat }).AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (++count > 1024 || contract.SchemaJson is null || contract.SchemaJson.Length > 16384 || contract.MetadataFormat != 1)
            {
                throw new CacheRelationalException(CacheErrorCode.Compatibility, "The retained contract catalog exceeds this metadata format's bounds.");
            }

            var schema = JsonSerializer.Deserialize<CacheDimensionSchema[]>(contract.SchemaJson) ?? [];
            if (schema.Length > 16 || CacheSelectionValidator.Validate(selection, schema).IsFailure)
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "The selection cannot be evaluated safely against every retained contract schema.");
            }

            evaluator ??= new(selection, schema, cutoff);
        }

        if (evaluator is null) { throw new CacheRelationalException(CacheErrorCode.Compatibility, "The persisted collection has no compatible contract schema."); }

        return new(id, definition, collection.Revision, evaluator);
    }

    private void ValidatePage(QueryScope scope, CacheMetadataQuery query)
    {
        var limits = scope.Definition.Policies.Limits;
        if (query.PageSize <= 0 || query.PageSize > Math.Min(1000, limits.MaxMetadataPageSize) || query.MaximumCandidates <= 0 ||
            query.MaximumCandidates > Math.Min(100000, limits.MaxQueryCandidates))
        {
            throw new CacheRelationalException(CacheErrorCode.Validation, "The metadata page or candidate limit exceeds this collection's bound.");
        }
    }

    private async ValueTask<NativeMetadataPage> ReadMetadataPageAsync(DbContext context, QueryScope scope, CacheMetadataQuery query,
        QueryCheckpoint cursor, CancellationToken cancellationToken)
    {
        var compiler = new CacheSelectionQuery(context, scope.Id, query.Cutoff.UtcTicks) { Namespace = scope.Definition.Namespace, Collection = scope.Definition.Collection };
        var candidates = compiler.Build(query.Selection).Where(e => e.PublicationSequence <= cursor.MaximumPublicationSequence);
        var entries = new List<CacheEntryMetadata>();
        var charges = new List<ResourceCharge>();
        var examined = 0;
        long bytes = 0;
        var started = this.time.GetTimestamp();
        var limits = scope.Definition.Policies.Limits;
        var changed = cursor.Changed || cursor.Revision != scope.Revision;
        var completed = query.Selection.IsEmpty;
        string reason = null;
        try
        {
            while (!completed && entries.Count < query.PageSize && examined < query.MaximumCandidates && this.time.GetElapsedTime(started) < limits.MaxQueryWork)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var take = Math.Min(16, Math.Min(query.MaximumCandidates - examined, query.PageSize - entries.Count));
                var heads = await compiler.After(candidates, cursor.After).OrderBy(e => e.Id)
                    .Select(e => new MetadataHead(e.Id.Substring(0, 65), e.PayloadId.Substring(0, 33), e.MetadataBytes, (e.MetadataJson.Substring(0, 65537) + "!").Length - 1))
                    .Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
                if (heads.Count == 0) { completed = true; break; }

                long reserve = 0;
                foreach (var head in heads)
                {
                    CacheRelationalProjection.ValidateScalar(head.Id, 64);
                    CacheRelationalProjection.ValidateScalar(head.Version, 32);
                    if (head.Characters is <= 0 or > 65536 || head.MetadataBytes < 0 || head.MetadataBytes > 4 * CacheDefaults.MiB)
                    {
                        throw new CacheRelationalException(CacheErrorCode.Corruption, "A persisted metadata row exceeds its native format bound.");
                    }

                    // Persisted size counters cannot reduce admission below one bounded JSON/graph workspace.
                    reserve = checked(reserve + Math.Max(512 * CacheDefaults.KiB, head.MetadataBytes + 2L * head.Characters));
                }

                if (!ResourceCharge.TryReserve(reserve, out var dataMemory, this.transient, this.ResidentFor(scope.Definition)))
                {
                    throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded metadata page admission was rejected.");
                }

                charges.Add(dataMemory);
                var ids = heads.Select(h => h.Id).ToArray();
                var data = await BoundedEntries(context.Set<CacheEntryEntity>().Where(e => ids.Contains(e.Id))).ToDictionaryAsync(e => e.Id, cancellationToken).ConfigureAwait(false);
                var pageFull = false;
                long retainedCharge = 0;
                foreach (var head in heads)
                {
                    examined++;
                    if (!data.TryGetValue(head.Id, out var row) || row.PayloadId != head.Version)
                    {
                        changed = true;
                        cursor = cursor with { After = head.Id };
                        continue;
                    }

                    var metadata = Metadata(row);
                    var matches = scope.Evaluator.Matches(metadata);
                    var size = Encoding.UTF8.GetByteCount(row.MetadataJson);
                    if (matches && (size > limits.MaxMetadataPageBytes - bytes))
                    {
                        if (entries.Count == 0) { throw new CacheRelationalException(CacheErrorCode.Capacity, "One metadata row exceeds the configured page byte ceiling."); }

                        pageFull = true;
                        reason = "PageByteLimit";
                        break;
                    }

                    cursor = cursor with { After = head.Id };
                    if (matches)
                    {
                        entries.Add(metadata);
                        retainedCharge += head.MetadataBytes == 0 ? 512 * CacheDefaults.KiB : Math.Max(16384, head.MetadataBytes + 2L * head.Characters);
                        bytes += size;
                        cursor = cursor with { Observed = checked(cursor.Observed + 1) };
                    }
                }

                data = null; // EF rows/JSON strings are temporary; only selected immutable metadata remains owned.
                if (retainedCharge == 0)
                {
                    charges.RemoveAt(charges.Count - 1);
                    dataMemory.Dispose();
                }
                else { dataMemory.ReduceTo(retainedCharge); }

                if (pageFull) { break; }

                completed = heads.Count < take;
            }

            if (!completed && reason is null)
            {
                reason = examined >= query.MaximumCandidates ? "CandidateLimit" : entries.Count >= query.PageSize ? "PageLimit" : "WorkLimit";
            }

            var revision = await context.Set<CacheCollectionEntity>().Where(e => e.Id == scope.Id).Select(e => e.Revision).SingleAsync(cancellationToken).ConfigureAwait(false);
            changed |= revision != cursor.Revision;
            cursor = cursor with { Changed = changed };
            return new(new(entries.AsReadOnly(), null, cursor.Observed, completed && !changed, examined, completed, reason ?? (changed ? "CollectionChanged" : null)), cursor);
        }
        finally
        {
            foreach (var charge in charges) { charge.Dispose(); }
        }
    }

    private string EncodeQueryCursor(QueryCheckpoint cursor)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(cursor);
        if (body.Length > 4096) { throw new CacheRelationalException(CacheErrorCode.Capacity, "The native continuation exceeds its fixed bound."); }

        return Base64Url(body) + "." + Base64Url(HMACSHA256.HashData(this.continuationKey, body));
    }

    private QueryCheckpoint DecodeQueryCursor(string value, string scope, string fingerprint, DateTimeOffset cutoff, string purpose)
    {
        try
        {
            if (value.Length > 6000) { throw new FormatException(); }

            var parts = value.Split('.');
            if (parts.Length != 2 || parts[1].Length != 43) { throw new FormatException(); }

            var body = Unbase64Url(parts[0]);
            var signature = Unbase64Url(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(this.continuationKey, body))) { throw new FormatException(); }

            var cursor = JsonSerializer.Deserialize<QueryCheckpoint>(body);
            if (cursor is null || cursor.Format != 2 || cursor.Scope != scope || cursor.Fingerprint != fingerprint || cursor.Cutoff != cutoff.UtcTicks ||
                cursor.Purpose != purpose || cursor.Observed < 0 || cursor.Revision < 0 || cursor.MaximumPublicationSequence < 0 ||
                (cursor.After is not null && (cursor.After.Length != 64 || cursor.After.Any(c => !char.IsAsciiHexDigit(c)))))
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new CacheRelationalException(CacheErrorCode.Validation, "The continuation does not belong to this store, selection, cutoff or operation.");
        }
    }

    private static string SelectionFingerprint(CacheSelection selection, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sink = new IncrementalHashWriteStream(hash, cancellationToken);
        JsonSerializer.Serialize(sink, selection);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Unbase64Url(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));

    private sealed record QueryScope(string Id, CacheProviderCollection Definition, long Revision, CacheSelectionEvaluator Evaluator);

    private sealed record QueryCheckpoint(int Format, string Scope, string Fingerprint, long Cutoff, string Purpose, string After, long Revision, long Observed, bool Changed,
        long MaximumPublicationSequence);

    private sealed record MetadataHead(string Id, string Version, long MetadataBytes, int Characters);

    private sealed record NativeMetadataPage(CacheMetadataPage Page, QueryCheckpoint Cursor);

    // Refresh candidates and backoff.

    /// <summary>Reads a bounded indexed page of current-contract refresh candidates without opening payloads or recording foreground activity.</summary>
    /// <param name="query">The fixed refresh horizon, activity window and candidate allowance.</param>
    /// <param name="cancellationToken">The cancellation token for bounded native reads.</param>
    /// <returns>Eligible immutable metadata and signed keyset progress.</returns>
    /// <example><code>var page = await provider.QueryRefreshAsync(new("catalog", "cards", "1", now, lead, activity), token);</code></example>
    public ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(query);
            if (query.Limit is <= 0 or > 1000 || query.RefreshAhead <= TimeSpan.Zero || query.ActivityWindow <= TimeSpan.Zero)
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "The refresh candidate allowance and timing bounds are invalid.");
            }

            var definition = this.Definition(new(query.Namespace, query.Collection, query.ContractVersion, "refresh"));
            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var workMemory, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded refresh-candidate admission was rejected.");
            }

            using (workMemory)
            {
                return await this.ReadRefreshPageAsync(context, definition, query, token).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <summary>Records finite scheduler backoff only while the selected publication version remains current.</summary>
    /// <param name="attempt">The exact version, source outcome and completion timestamp.</param>
    /// <param name="cancellationToken">The cancellation token for the conditional metadata transaction.</param>
    /// <returns>Success for an applied update or an already superseded version.</returns>
    /// <example><code>await provider.RecordRefreshAttemptAsync(new(identity, selectedVersion, true, now), token);</code></example>
    public async ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(attempt);
            var definition = this.Definition(attempt.Identity);
            if (!ResourceCharge.TryReserve(CacheDefaults.MiB, out var memory, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "The refresh-attempt metadata admission was rejected.");
            }

            using (memory)
            {
                await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
                var row = await ReadEntryAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == attempt.Identity.Digest && e.PayloadId == attempt.Version.Value), true, token).ConfigureAwait(false);
                if (row is not null)
                {
                    var metadata = Metadata(row, attempt.Identity);
                    var policy = definition.Policies.Refresh;
                    var failures = attempt.SourceFailed ? (int)Math.Min(policy.FailuresBeforePause, (long)metadata.RefreshFailureCount + 1) : 0;
                    var updated = metadata with
                    {
                        RefreshFailureCount = failures,
                        RefreshNotBefore = attempt.SourceFailed ? CacheRefreshPolicy.NextAttempt(policy, failures, attempt.CompletedAt) : null
                    };
                    var encoded = Serialize(updated);
                    if (Encoding.UTF8.GetByteCount(encoded) > definition.Policies.Limits.MaxEntryMetadataBytes)
                    {
                        throw new CacheRelationalException(CacheErrorCode.Capacity, "The refresh-attempt metadata exceeds the collection byte ceiling.");
                    }

                    row.MetadataJson = encoded;
                    row.RefreshBackoffTicks = updated.RefreshNotBefore?.UtcTicks;
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return true;
            }
        }, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private async ValueTask<CacheRefreshPage> ReadRefreshPageAsync(DbContext context, CacheProviderCollection definition, CacheRefreshQuery query, CancellationToken token)
    {
        var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "refresh");
        var collectionId = CollectionId(identity);
        var contractId = ContractId(identity);
        var fingerprint = Hash("refresh:" + definition.ContractVersion + ":" + query.RefreshAhead.Ticks + ":" + query.ActivityWindow.Ticks);
        var cursor = query.Continuation is null ? new RefreshCheckpoint(1, "refresh", collectionId, fingerprint, query.Now.UtcTicks, 0, null) :
            this.DecodeRefreshCursor(query.Continuation, collectionId, fingerprint, query.Now.UtcTicks);
        var now = query.Now.UtcTicks;
        var upper = now > DateTimeOffset.MaxValue.UtcTicks - query.RefreshAhead.Ticks ? DateTimeOffset.MaxValue.UtcTicks : now + query.RefreshAhead.Ticks;
        var activeSince = query.ActivityWindow.Ticks > now ? 0 : now - query.ActivityWindow.Ticks;
        var candidates = context.Set<CacheEntryEntity>().AsNoTracking().Where(e => e.CollectionId == collectionId && e.ContractId == contractId &&
            e.HasForegroundAccess && e.AbsoluteExpiryTicks > now && e.AbsoluteExpiryTicks <= upper && e.ExpiryTicks > now &&
            !e.IsNotFound && e.LastAccessTicks >= activeSince && (e.RefreshBackoffTicks == null || e.RefreshBackoffTicks <= now));
        if (cursor.After is not null)
        {
            var tailIds = new CacheSelectionQuery(context, collectionId, now).After(candidates, cursor.After).Select(e => e.Id);
            candidates = candidates.Where(e => e.AbsoluteExpiryTicks > cursor.AfterExpiry || (e.AbsoluteExpiryTicks == cursor.AfterExpiry && tailIds.Contains(e.Id)));
        }

        var limit = Math.Min(query.Limit, definition.Policies.Limits.MaxQueryCandidates);
        var heads = await candidates.OrderBy(e => e.AbsoluteExpiryTicks).ThenBy(e => e.Id)
            .Select(e => new RefreshHead(e.Id.Substring(0, 65), e.PayloadId.Substring(0, 33), e.AbsoluteExpiryTicks, e.MetadataBytes, e.MetadataJson.Length)).Take(limit).ToListAsync(token).ConfigureAwait(false);
        var entries = new List<CacheEntryMetadata>();
        var charges = new List<ResourceCharge>();
        var examined = 0;
        long bytes = 0;
        var started = this.time.GetTimestamp();
        try
        {
            foreach (var head in heads)
            {
                token.ThrowIfCancellationRequested();
                if (this.time.GetElapsedTime(started) >= definition.Policies.Limits.MaxQueryWork) { break; }

                CacheRelationalProjection.ValidateScalar(head.Id, 64);
                CacheRelationalProjection.ValidateScalar(head.Version, 32);
                if (head.Characters is <= 0 or > 65536 || head.MetadataBytes < 0 || head.MetadataBytes > 4 * CacheDefaults.MiB)
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "A refresh metadata row exceeds the native format bound.");
                }

                if (!ResourceCharge.TryReserve(Math.Max(16384, head.MetadataBytes + 2L * head.Characters), out var dataMemory, this.transient, this.ResidentFor(definition)))
                {
                    throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded refresh metadata page admission was rejected.");
                }

                charges.Add(dataMemory);
                var row = await ReadEntryAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == head.Id && e.PayloadId == head.Version), false, token).ConfigureAwait(false);
                var metadata = row is null ? null : Metadata(row);
                if (metadata is not null && (metadata.Identity.Namespace != definition.Namespace || metadata.Identity.Collection != definition.Collection ||
                    metadata.Identity.ContractVersion != definition.ContractVersion))
                {
                    throw new CacheRelationalException(CacheErrorCode.Corruption, "The refresh row has a different canonical collection or contract identity.");
                }

                var eligible = CacheRefreshPolicy.IsEligible(metadata, query);
                var size = eligible ? Encoding.UTF8.GetByteCount(row.MetadataJson) : 0;
                if (size > definition.Policies.Limits.MaxMetadataPageBytes - bytes)
                {
                    if (entries.Count == 0) { throw new CacheRelationalException(CacheErrorCode.Capacity, "One refresh metadata row exceeds the configured page byte ceiling."); }

                    break;
                }

                examined++;
                cursor = cursor with { AfterExpiry = head.AbsoluteExpiry, After = head.Id };
                if (eligible)
                {
                    entries.Add(metadata);
                    bytes += size;
                }
                else
                {
                    charges.RemoveAt(charges.Count - 1);
                    dataMemory.Dispose();
                }
            }

            var completed = heads.Count < limit && examined == heads.Count;
            return new(entries.AsReadOnly(), completed ? null : this.EncodeRefreshCursor(cursor), examined);
        }
        finally
        {
            foreach (var charge in charges) { charge.Dispose(); }
        }
    }

    private string EncodeRefreshCursor(RefreshCheckpoint cursor)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Base64Url(body) + "." + Base64Url(HMACSHA256.HashData(this.continuationKey, body));
    }

    private RefreshCheckpoint DecodeRefreshCursor(string value, string scope, string fingerprint, long now)
    {
        try
        {
            if (value.Length > 4096) { throw new FormatException(); }

            var parts = value.Split('.');
            if (parts.Length != 2 || parts[1].Length != 43) { throw new FormatException(); }

            var body = Unbase64Url(parts[0]);
            if (!CryptographicOperations.FixedTimeEquals(Unbase64Url(parts[1]), HMACSHA256.HashData(this.continuationKey, body))) { throw new FormatException(); }

            var cursor = JsonSerializer.Deserialize<RefreshCheckpoint>(body);
            if (cursor is null || cursor.Format != 1 || cursor.Purpose != "refresh" || cursor.Scope != scope || cursor.Fingerprint != fingerprint || cursor.Now != now ||
                cursor.AfterExpiry < 0 || cursor.AfterExpiry > DateTimeOffset.MaxValue.UtcTicks ||
                (cursor.After is not null && (cursor.After.Length != 64 || cursor.After.Any(c => !char.IsAsciiHexDigit(c)))))
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException)
        {
            throw new CacheRelationalException(CacheErrorCode.Validation, "The refresh continuation does not belong to this store, contract, horizon or sweep time.");
        }
    }

    private sealed record RefreshCheckpoint(int Format, string Purpose, string Scope, string Fingerprint, long Now, long AfterExpiry, string After);

    private sealed record RefreshHead(string Id, string Version, long AbsoluteExpiry, long MetadataBytes, int Characters);

    // Bounded invalidation and cutoffs.

    /// <summary>Fences preexisting source publication, then conditionally retires one bounded metadata batch and commits tracked progress with those deletions.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            using var unit = this.AdmitOperationUnit(request.OperationId);
            var definition = this.ScopeDefinition(request.Namespace, request.Collection);
            var selection = request.Clear ? CacheSelection.Pattern("*") : request.Selection;
            var purpose = (request.Clear ? "clear:" : "invalidate:") + request.OperationId;
            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var memory, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded invalidation work admission was rejected.");
            }

            using (memory)
            {
                QueryScope scope;
                QueryCheckpoint cursor;
                var fingerprint = SelectionFingerprint(selection, token);
                string expectedContinuation;
                if (request.Continuation is null)
                {
                    await using var fenceTransaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
                    scope = await this.QueryScopeAsync(context, definition, selection, request.Cutoff, token).ConfigureAwait(false);
                    var tracked = await this.RunningOperationAsync(context, request, null, token).ConfigureAwait(false);
                    if (tracked is not null && Operation(tracked).CancellationRequested)
                    {
                        var cancelled = this.CancelAtBoundary(tracked, request);
                        await context.SaveChangesAsync(token).ConfigureAwait(false);
                        await fenceTransaction.CommitAsync(token).ConfigureAwait(false);
                        return cancelled;
                    }

                    if (selection.IsEmpty)
                    {
                        var empty = new CacheInvalidationOutcome { Completed = true, OperationId = request.OperationId };
                        if (tracked is not null) { this.CheckpointOperation(tracked, empty); await context.SaveChangesAsync(token).ConfigureAwait(false); }

                        await fenceTransaction.CommitAsync(token).ConfigureAwait(false);
                        return empty;
                    }

                    var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == scope.Id), true, token).ConfigureAwait(false);
                    var boundary = collection.Revision;
                    collection.Epoch = checked(collection.Epoch + 1);
                    collection.Revision++;
                    await context.Set<CacheOwnershipEntity>().Where(e => e.CollectionId == scope.Id && e.State == (int)CacheOwnershipState.Owned)
                        .ExecuteUpdateAsync(set => set.SetProperty(e => e.State, (int)CacheOwnershipState.Lost), token).ConfigureAwait(false);
                    cursor = new(2, scope.Id, fingerprint, request.Cutoff.UtcTicks, purpose, null, collection.Revision, 0, false, boundary);
                    expectedContinuation = this.EncodeQueryCursor(cursor);
                    if (tracked is not null)
                    {
                        this.CheckpointOperation(tracked, new() { Completed = false, Continuation = expectedContinuation, OperationId = request.OperationId });
                    }

                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    await fenceTransaction.CommitAsync(token).ConfigureAwait(false);
                    context.ChangeTracker.Clear();
                    scope = scope with { Revision = collection.Revision };
                }
                else
                {
                    scope = await this.QueryScopeAsync(context, definition, selection, request.Cutoff, token).ConfigureAwait(false);
                    cursor = this.DecodeQueryCursor(request.Continuation, scope.Id, fingerprint, request.Cutoff, purpose);
                    expectedContinuation = request.Continuation;
                    if (request.OperationId is not null)
                    {
                        await using var admission = await this.LockStoreAsync(context, token).ConfigureAwait(false);
                        var tracked = await this.RunningOperationAsync(context, request, expectedContinuation, token).ConfigureAwait(false);
                        if (Operation(tracked).CancellationRequested)
                        {
                            var cancelled = this.CancelAtBoundary(tracked, request);
                            await context.SaveChangesAsync(token).ConfigureAwait(false);
                            await admission.CommitAsync(token).ConfigureAwait(false);
                            return cancelled;
                        }

                        await admission.CommitAsync(token).ConfigureAwait(false);
                        context.ChangeTracker.Clear();
                    }
                }

                var query = new CacheMetadataQuery(definition.Namespace, definition.Collection, selection, request.Cutoff,
                    Math.Min(128, definition.Policies.Limits.MaxMetadataPageSize), MaximumCandidates: definition.Policies.Limits.MaxQueryCandidates);
                this.ValidatePage(scope, query);
                var page = await this.ReadMetadataPageAsync(context, scope, query, cursor, token).ConfigureAwait(false);
                await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
                var operation = await this.RunningOperationAsync(context, request, expectedContinuation, token).ConfigureAwait(false);
                var current = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == scope.Id), true, token).ConfigureAwait(false);
                var changed = page.Cursor.Changed || current.Revision != cursor.Revision;
                var selected = ExpectedEntries(context, scope.Id, page.Page.Entries);
                var retired = await this.RetireEntriesAsync(context, selected, scope.Id, token).ConfigureAwait(false);
                changed |= retired.Count != page.Page.Entries.Count;
                var next = page.Cursor with { Revision = current.Revision, Observed = checked(cursor.Observed + retired.Count), Changed = changed };
                var outcome = new CacheInvalidationOutcome { Completed = page.Page.Completed, AffectedCount = next.Observed,
                    CountIsExact = page.Page.Completed && !changed, Continuation = page.Page.Completed ? null : this.EncodeQueryCursor(next),
                    OperationId = request.OperationId };
                if (operation is not null) { this.CheckpointOperation(operation, outcome); }

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return outcome;
            }
        }, cancellationToken);

    private async ValueTask<CacheOperationEntity> RunningOperationAsync(DbContext context, CacheProviderInvalidation request, string continuation,
        CancellationToken cancellationToken)
    {
        if (request.OperationId is null) { return null; }

        var row = await ReadOperationAsync(context, context.Set<CacheOperationEntity>().Where(e => e.Id == request.OperationId), true, cancellationToken).ConfigureAwait(false);
        if (row is null || row.State != (int)CacheOperationState.Running || row.ClaimNode != this.node || !row.ClaimUntilTicks.HasValue ||
            row.ClaimUntilTicks.Value <= this.time.GetUtcNow().UtcTicks)
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "The tracked invalidation does not hold the current execution claim.");
        }

        var operation = Operation(row);
        if (operation.Outcome?.Continuation != continuation || operation.Request.Clear != request.Clear || operation.Cutoff != request.Cutoff ||
            !StringComparer.OrdinalIgnoreCase.Equals(operation.Request.Namespace, request.Namespace) ||
            !StringComparer.OrdinalIgnoreCase.Equals(operation.Request.Collection, request.Collection) ||
            (!request.Clear && SelectionFingerprint(operation.Request.Selection, cancellationToken) != SelectionFingerprint(request.Selection, cancellationToken)))
        {
            throw new CacheRelationalException(CacheErrorCode.Conflict, "The tracked operation scope or confirmed checkpoint changed.");
        }

        return row;
    }

    private void CheckpointOperation(CacheOperationEntity row, CacheInvalidationOutcome outcome)
    {
        var now = this.time.GetUtcNow();
        var previous = Operation(row);
        var state = outcome.Completed ? CacheOperationState.Completed : previous.CancellationRequested ? CacheOperationState.Cancelled : CacheOperationState.Running;
        var changed = previous with { State = state,
            Outcome = outcome, Revision = Guid.NewGuid().ToString("N"), UpdatedAt = now };
        ApplyOperation(row, changed, this.node, now);
    }

    private CacheInvalidationOutcome CancelAtBoundary(CacheOperationEntity row, CacheProviderInvalidation request)
    {
        var previous = Operation(row);
        var outcome = previous.Outcome ?? new CacheInvalidationOutcome { Completed = false, CountIsExact = false, OperationId = request.OperationId };
        this.CheckpointOperation(row, outcome);

        return outcome;
    }

    private static IQueryable<CacheEntryEntity> ExpectedEntries(DbContext context, string scope, IReadOnlyList<CacheEntryMetadata> metadata)
    {
        var parameter = Expression.Parameter(typeof(CacheEntryEntity), "entry");
        Expression predicate = Expression.Constant(false);
        foreach (var entry in metadata)
        {
            var exact = Expression.AndAlso(Expression.Equal(Expression.Property(parameter, nameof(CacheEntryEntity.Id)), Expression.Constant(entry.Identity.Digest)),
                Expression.Equal(Expression.Property(parameter, nameof(CacheEntryEntity.PayloadId)), Expression.Constant(entry.Version.Value)));
            predicate = Expression.OrElse(predicate, exact);
        }

        var expected = Expression.Lambda<Func<CacheEntryEntity, bool>>(predicate, parameter);
        return context.Set<CacheEntryEntity>().Where(e => e.CollectionId == scope).Where(expected);
    }

    // Shared-store ownership.

    /// <summary>Creates one authoritative shared flight or registers bounded interest in its active owner. It never invokes application source work.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.ValidateOwnershipRequest(request);
            using var memory = this.ReserveOwnershipMemory(definition);
            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var now = this.time.GetUtcNow();
            var flights = await context.Set<CacheOwnershipEntity>().AsNoTracking().Where(e => e.EntryId == request.Identity.Digest && e.State == (int)CacheOwnershipState.Owned)
                .OrderBy(e => e.Id).Select(e => new OwnershipHeader(e.Id.Substring(0, 33), e.DeadlineTicks, e.LeaseExpiryTicks, e.Independent)).Take(129).ToListAsync(token).ConfigureAwait(false);
            foreach (var header in flights) { CacheRelationalProjection.ValidateScalar(header.Id, 32); }

            if (flights.Count > 128) { throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded source-flight registry is full for this identity."); }

            var expired = flights.Where(e => e.DeadlineTicks <= now.UtcTicks || e.LeaseExpiryTicks <= now.UtcTicks).Select(e => e.Id).ToArray();
            if (expired.Length > 0)
            {
                await context.Set<CacheOwnershipEntity>().Where(e => expired.Contains(e.Id))
                    .ExecuteUpdateAsync(set => set.SetProperty(e => e.State, (int)CacheOwnershipState.Lost), token).ConfigureAwait(false);
            }

            var active = flights.Where(e => e.DeadlineTicks > now.UtcTicks && e.LeaseExpiryTicks > now.UtcTicks).ToArray();
            var shared = active.Where(e => !e.Independent).ToArray();
            if (shared.Length > 1) { throw new CacheRelationalException(CacheErrorCode.Corruption, "The native shared source authority is inconsistent."); }

            var joined = !request.Independent && shared.Length == 1;
            CacheOwnershipEntity flight;
            if (joined)
            {
                var id = shared[0].Id;
                flight = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.Id == id), true, token).ConfigureAwait(false);
            }
            else
            {
                if (active.Length >= 128) { throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded source-flight registry is full for this identity."); }

                var fence = await this.CaptureFenceAsync(context, request.Identity, definition, token).ConfigureAwait(false);
                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == fence.CollectionId), true, token).ConfigureAwait(false);
                this.ReserveMetadata(store, collection, 8192);
                flight = new() { Id = Guid.NewGuid().ToString("N"), EntryId = request.Identity.Digest, KeyId = fence.KeyId, CollectionId = fence.CollectionId,
                    NodeId = request.NodeId, OwnerToken = Guid.NewGuid().ToString("N"), FenceId = fence.Id, State = (int)CacheOwnershipState.Owned,
                    DeadlineTicks = request.Deadline.UtcTicks, LeaseExpiryTicks = Math.Min(request.Deadline.UtcTicks, now.Add(this.options.LeaseLifetime).UtcTicks),
                    Independent = request.Independent };
                context.Add(flight);
            }

            await this.AddInterestAsync(context, flight, request, definition, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return Ownership(flight, !joined);
        }, cancellationToken);

    /// <summary>Renews only a live exact owner and its unchanged publication fence. It never extends the original source deadline.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.Definition(identity);
            using var memory = this.ReserveOwnershipMemory(definition);
            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var row = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.EntryId == identity.Digest && e.OwnerToken == owner.Value), true, token).ConfigureAwait(false);
            if (row is null) { throw new CacheRelationalException(CacheErrorCode.Conflict, "The issued source owner is unavailable."); }

            var now = this.time.GetUtcNow();
            if (row.State == (int)CacheOwnershipState.Owned)
            {
                var interested = definition.Policies.Loading.ContinueWithoutWaiters ||
                    CacheOwnershipInterestState.Read(row.InterestsJson).Any(interest => interest.DeadlineTicks > now.UtcTicks);
                var fence = await CacheRelationalProjection.SingleOrDefaultAsync(context, context.Set<CacheFenceEntity>().Where(e => e.Id == row.FenceId && e.EntryId == row.EntryId), false, token).ConfigureAwait(false);
                var keyEpoch = await context.Set<CacheKeyEntity>().Where(e => e.Id == row.KeyId).Select(e => (long?)e.Epoch).SingleOrDefaultAsync(token).ConfigureAwait(false);
                var collectionEpoch = await context.Set<CacheCollectionEntity>().Where(e => e.Id == row.CollectionId).Select(e => (long?)e.Epoch).SingleOrDefaultAsync(token).ConfigureAwait(false);
                if (!interested || row.LeaseExpiryTicks <= now.UtcTicks || row.DeadlineTicks <= now.UtcTicks || fence is null ||
                    fence.KeyEpoch != keyEpoch || fence.CollectionEpoch != collectionEpoch)
                {
                    row.State = (int)CacheOwnershipState.Lost;
                }
                else
                {
                    row.LeaseExpiryTicks = Math.Min(row.DeadlineTicks, now.Add(this.options.LeaseLifetime).UtcTicks);
                }
            }

            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return Ownership(row, row.State == (int)CacheOwnershipState.Owned);
        }, cancellationToken);

    /// <summary>Observes one already registered flight using a short metadata read; waiting and polling delays remain outside native I/O admission.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.ValidateOwnershipRequest(request);
            using var memory = this.ReserveOwnershipMemory(definition);
            if (!Guid.TryParseExact(flightId, "N", out _)) { throw new CacheRelationalException(CacheErrorCode.Validation, "An issued flight identity is required."); }

            var row = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.Id == flightId && e.EntryId == request.Identity.Digest), false, token).ConfigureAwait(false);
            if (row is null)
            {
                return new CacheOwnershipObservation(CacheOwnershipState.Lost, null, default, flightId, this.time.GetUtcNow(), Failure: CacheErrorCode.ProviderUnavailable);
            }

            if (row.State == (int)CacheOwnershipState.Owned && (row.LeaseExpiryTicks <= this.time.GetUtcNow().UtcTicks || row.DeadlineTicks <= this.time.GetUtcNow().UtcTicks))
            {
                return Ownership(row, false) with { State = CacheOwnershipState.Lost, Failure = CacheErrorCode.ProviderUnavailable };
            }

            if (row.State == (int)CacheOwnershipState.Owned && !CacheOwnershipInterestState.Read(row.InterestsJson)
                .Any(interest => StringComparer.Ordinal.Equals(interest.InterestId, request.InterestId)))
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "This observation group has released its shared-source interest.");
            }

            return Ownership(row, false);
        }, cancellationToken);

    /// <summary>Completes one confirmed owner with an exact published version, metadata-only absence, or bounded failure classification.</summary>
    public async ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.Definition(identity);
            using var memory = this.ReserveOwnershipMemory(definition);
            ArgumentNullException.ThrowIfNull(completion);
            if (completion.State is not (CacheOwnershipState.Published or CacheOwnershipState.Unretained or CacheOwnershipState.Failed or CacheOwnershipState.Lost) ||
                (completion.State == CacheOwnershipState.Published) != completion.Version.HasValue ||
                (completion.Failure.HasValue && !Enum.IsDefined(completion.Failure.Value)))
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "The shared source completion is invalid.");
            }

            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var row = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.EntryId == identity.Digest && e.OwnerToken == owner.Value), true, token).ConfigureAwait(false);
            if (row is null) { throw new CacheRelationalException(CacheErrorCode.Conflict, "The exact source owner is unavailable."); }

            if (row.State != (int)CacheOwnershipState.Owned)
            {
                if (row.State != (int)completion.State || row.PublishedVersion != completion.Version?.Value || row.IsNotFound != completion.IsNotFound)
                {
                    throw new CacheRelationalException(CacheErrorCode.Conflict, "The source flight already ended with another outcome.");
                }

                return true;
            }

            if (completion.State == CacheOwnershipState.Published)
            {
                var version = completion.Version.Value.Value;
                if (!await context.Set<CachePayloadEntity>().AnyAsync(e => e.Id == version && e.EntryId == identity.Digest && (e.State == 1 || e.State == 2), token).ConfigureAwait(false))
                {
                    throw new CacheRelationalException(CacheErrorCode.Indeterminate, "The reported publication version is not confirmed in this store.");
                }
            }
            else if (completion.State != CacheOwnershipState.Lost)
            {
                await this.VerifyFenceAsync(context, identity, new(row.FenceId), owner, null, token).ConfigureAwait(false);
            }

            row.State = (int)completion.State;
            row.PublishedVersion = completion.Version?.Value;
            row.FailureCode = completion.Failure.HasValue ? (int)completion.Failure.Value : null;
            row.IsNotFound = completion.IsNotFound;
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <summary>Releases exactly one local observation group's interest. Other nodes' interest and actual source work remain independent.</summary>
    public async ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default)
    {
        var released = await this.ExecuteAsync(async (context, token) =>
        {
            var definition = this.Definition(identity);
            using var memory = this.ReserveOwnershipMemory(definition);
            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var flight = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.Id == flightId && e.EntryId == identity.Digest), true, token).ConfigureAwait(false);
            if (flight is not null)
            {
                var interests = CacheOwnershipInterestState.Read(flight.InterestsJson);
                var removed = interests.RemoveAll(interest => StringComparer.Ordinal.Equals(interest.InterestId, interestId));
                if (removed != 0)
                {
                    flight.InterestsJson = CacheOwnershipInterestState.Write(interests);
                    var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                    var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == flight.CollectionId), true, token).ConfigureAwait(false);
                    store.MetadataBytes -= CacheOwnershipInterestState.InterestMetadataBytes * removed;
                    collection.MetadataBytes -= CacheOwnershipInterestState.InterestMetadataBytes * removed;
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return released.IsSuccess ? Result.Success() : Result.Failure().WithErrors(released.Errors);
    }

    private CacheProviderCollection ValidateOwnershipRequest(CacheOwnershipRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = this.Definition(request.Identity);
        _ = CacheIdentityNormalizer.Name(request.NodeId);
        _ = CacheIdentityNormalizer.Name(request.InterestId);
        var now = this.time.GetUtcNow();
        if (request.Deadline <= now || request.Deadline > now.Add(definition.Policies.Loading.SourceTimeout) || request.LeaseLifetime != this.options.LeaseLifetime)
        {
            throw new CacheRelationalException(CacheErrorCode.SourceTimeout, "The fixed source deadline or native lease profile is invalid.");
        }

        return definition;
    }

    private async ValueTask AddInterestAsync(DbContext context, CacheOwnershipEntity flight, CacheOwnershipRequest request,
        CacheProviderCollection definition, CancellationToken cancellationToken)
    {
        var interests = CacheOwnershipInterestState.Read(flight.InterestsJson);
        if (interests.Any(interest => StringComparer.Ordinal.Equals(interest.InterestId, request.InterestId))) { return; }

        if (interests.Count >= Math.Min((long)definition.Policies.Limits.MaxFollowersPerKey + 1, CacheOwnershipInterestState.MaximumInterests))
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded shared observation-group limit was reached.");
        }

        var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, cancellationToken).ConfigureAwait(false);
        var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == flight.CollectionId), true, cancellationToken).ConfigureAwait(false);
        interests.Add(new(request.InterestId, Math.Min(flight.DeadlineTicks, request.Deadline.UtcTicks)));
        var json = CacheOwnershipInterestState.Write(interests);
        this.ReserveMetadata(store, collection, CacheOwnershipInterestState.InterestMetadataBytes);
        flight.InterestsJson = json;
    }

    private static CacheOwnershipObservation Ownership(CacheOwnershipEntity flight, bool owner)
    {
        if (!Enum.IsDefined((CacheOwnershipState)flight.State) || flight.State == (int)CacheOwnershipState.Pending ||
            !Guid.TryParseExact(flight.Id, "N", out _) || !Guid.TryParseExact(flight.FenceId, "N", out _) || !Guid.TryParseExact(flight.OwnerToken, "N", out _) ||
            (flight.FailureCode.HasValue && !Enum.IsDefined((CacheErrorCode)flight.FailureCode.Value)))
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The native source authority metadata is invalid.");
        }

        var state = (CacheOwnershipState)flight.State;
        return new(state == CacheOwnershipState.Owned && !owner ? CacheOwnershipState.Pending : state,
            owner && state == CacheOwnershipState.Owned ? new CacheOwnerToken(flight.OwnerToken) : null, new(flight.FenceId), flight.Id,
            new(flight.LeaseExpiryTicks, TimeSpan.Zero), flight.PublishedVersion is null ? null : new CacheEntryVersion(flight.PublishedVersion),
            flight.FailureCode.HasValue ? (CacheErrorCode)flight.FailureCode.Value : null, flight.IsNotFound);
    }

    // Bounded ownership interests.

    private ResourceCharge ReserveOwnershipMemory(CacheProviderCollection definition)
    {
        if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var memory, this.transient, this.ResidentFor(definition)))
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded source-flight metadata admission was rejected.");
        }

        return memory;
    }

    private async ValueTask<CacheOwnershipEntity> ReadOwnershipAsync(DbContext context, IQueryable<CacheOwnershipEntity> query,
        bool tracking, CancellationToken token)
    {
        // Project a bounded prefix on the database. Corrupt oversized text cannot allocate an
        // unbounded CLR string, even on engines whose string-length function trims trailing spaces.
        return await CacheRelationalProjection.SingleOrDefaultAsync(context, query, tracking, token).ConfigureAwait(false);
    }

    private sealed record OwnershipHeader(string Id, long DeadlineTicks, long LeaseExpiryTicks, bool Independent);

    // Tracked operations and cancellation.

    private const long OperationMetadataBytes = 132 * CacheDefaults.KiB;
    private readonly Lock operationUnitsGate = new();
    private readonly HashSet<string> operationUnits = new(StringComparer.Ordinal);

    /// <summary>Creates or conditionally changes a bounded native invalidation record. Running records hold one renewable fenced execution claim.</summary>
    public ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) => this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (!Guid.TryParseExact(operation.Id, "N", out _) || operation.Request is null || operation.Request.Continuation is not null ||
                operation.Origin?.Length > 128 || !Enum.IsDefined(operation.State) || operation.Cutoff != operation.Request.Cutoff ||
                operation.Outcome?.FailedCollections?.Count > 32)
            {
                throw new CacheRelationalException(CacheErrorCode.Validation, "The tracked operation violates its fixed scope or metadata bounds.");
            }

            using var memory = this.ReserveOperationReadMemory();
            var definition = this.ScopeDefinition(operation.Request.Namespace, operation.Request.Collection);
            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var selection = operation.Request.Clear ? CacheSelection.Pattern("*") : operation.Request.Selection;
            var scope = await this.QueryScopeAsync(context, definition, selection, operation.Cutoff, token).ConfigureAwait(false);
            var existing = await ReadOperationAsync(context, context.Set<CacheOperationEntity>().Where(e => e.Id == operation.Id), true, token).ConfigureAwait(false);
            var now = this.time.GetUtcNow();
            var previous = existing is null ? null : Operation(existing);
            var cancellationRequest = previous?.State == CacheOperationState.Running && operation.State == CacheOperationState.Running && operation.CancellationRequested;
            if (previous is not null && (expectedRevision != previous.Revision || !SameOperation(previous, operation) ||
                Serialize(previous.Outcome) != Serialize(operation.Outcome) || !OperationTransition(previous.State, operation.State) ||
                (previous.CancellationRequested && !operation.CancellationRequested && operation.State == CacheOperationState.Running) ||
                (previous.State == CacheOperationState.Running && operation.State != CacheOperationState.Running &&
                    (this.HasOperationUnit(operation.Id) || (existing.ClaimNode != this.node && existing.ClaimUntilTicks > now.UtcTicks))) ||
                (!cancellationRequest && operation.State == CacheOperationState.Running && existing.ClaimNode != this.node && existing.ClaimUntilTicks > now.UtcTicks)))
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "The operation revision, state or live execution claim changed.");
            }

            if (previous is null && (expectedRevision is not null || operation.State != CacheOperationState.Queued || operation.Outcome is not null))
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "A missing operation can only be created in the queued state.");
            }

            var normalized = operation with { Collection = definition.Collection, Cutoff = operation.Cutoff.ToUniversalTime(),
                Request = operation.Request with { Namespace = definition.Namespace, Collection = definition.Collection, OperationId = operation.Id },
                Revision = Guid.NewGuid().ToString("N"), CreatedAt = previous?.CreatedAt ?? now, UpdatedAt = now };
            var encoded = Serialize(normalized);
            if (previous is null && Encoding.UTF8.GetByteCount(encoded) > 32768)
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "Initial operation metadata leaves insufficient space for bounded progress.");
            }

            if (existing is null)
            {
                if (await context.Set<CacheOperationEntity>().CountAsync(token).ConfigureAwait(false) >= this.diagnostics.MaximumOperationRecords)
                {
                    throw new CacheRelationalException(CacheErrorCode.Capacity, "Native operation history is full; active records are never evicted.");
                }

                var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
                var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == scope.Id), true, token).ConfigureAwait(false);
                if (OperationMetadataBytes > definition.Policies.Limits.MaxStorageBytes - collection.CurrentBytes - collection.RetiredBytes - collection.StagingBytes - collection.MetadataBytes)
                {
                    throw new CacheRelationalException(CacheErrorCode.Capacity, "Tracked operation metadata exceeds the collection's quota.");
                }

                this.ReserveMetadata(store, collection, OperationMetadataBytes);
                existing = new() { Id = operation.Id, CollectionId = scope.Id, CreatedTicks = now.UtcTicks };
                context.Add(existing);
            }

            if (cancellationRequest)
            {
                // A management node can request cancellation, but cannot acquire or extend execution authority.
                existing.MetadataJson = encoded;
                existing.Revision = normalized.Revision;
                existing.UpdatedTicks = now.UtcTicks;
            }
            else
            {
                ApplyOperation(existing, normalized, this.node, now);
            }

            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return normalized;
        }, cancellationToken);

    /// <summary>Reads one native record only when its logical scope belongs to this provider instance's declared catalog.</summary>
    public ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync(async (context, token) =>
        {
            using var memory = this.ReserveOperationReadMemory();
            var row = await ReadOperationAsync(context, context.Set<CacheOperationEntity>().Where(e => e.Id == operationId), false, token).ConfigureAwait(false);
            if (row is null || !this.AllowedOperationScope(row.CollectionId))
            {
                throw new CacheRelationalException(CacheErrorCode.NotFound, "The retained operation status is unavailable in this declared scope.");
            }

            return Operation(row);
        }, cancellationToken);

    /// <summary>Lists a bounded four-MiB native operation page without inspecting payload tables or returning another namespace's records.</summary>
    public ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync<IReadOnlyList<CacheProviderOperation>>(async (context, token) =>
        {
            if (limit is <= 0 or > 1000) { throw new CacheRelationalException(CacheErrorCode.Validation, "The operation page limit is invalid."); }

            var scopes = this.definitions.Values.Select(d => CollectionId(new(d.Namespace, d.Collection, d.ContractVersion, "catalog"))).Distinct().ToArray();
            var rows = BoundedOperations(context.Set<CacheOperationEntity>().Where(e => scopes.Contains(e.CollectionId))).OrderByDescending(e => e.UpdatedTicks)
                .ThenBy(e => e.Id).Take(Math.Min(limit, 4 * 1024 * 1024 / 65536));
            var operations = new List<CacheProviderOperation>();
            var charges = new List<ResourceCharge>();
            long bytes = 0;
            try
            {
                // Stream bounded records so aggregate page memory is admitted before each materialization.
                await using var enumerator = rows.AsAsyncEnumerable().GetAsyncEnumerator(token);
                while (true)
                {
                    if (!ResourceCharge.TryReserve(512 * CacheDefaults.KiB, out var memory, this.transient, this.resident))
                    {
                        throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded operation page admission was rejected.");
                    }

                    var retained = false;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) { break; }

                        var row = enumerator.Current;
                        var operation = Operation(row);
                        bytes = checked(bytes + Encoding.UTF8.GetByteCount(row.MetadataJson));
                        if (bytes > 4 * CacheDefaults.MiB)
                        {
                            throw new CacheRelationalException(CacheErrorCode.Capacity, "The native operation page exceeds its four-MiB byte bound.");
                        }

                        operations.Add(operation);
                        charges.Add(memory);
                        retained = true;
                    }
                    finally { if (!retained) { memory.Dispose(); } }
                }

                return operations.AsReadOnly();
            }
            finally
            {
                foreach (var charge in charges) { charge.Dispose(); }
            }
        }, cancellationToken);

    private bool AllowedOperationScope(string scope) => this.definitions.Values.Any(d => CollectionId(new(d.Namespace, d.Collection, d.ContractVersion, "catalog")) == scope);

    private ResourceCharge ReserveOperationReadMemory()
    {
        if (!ResourceCharge.TryReserve(512 * CacheDefaults.KiB, out var memory, this.transient, this.resident))
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The bounded operation metadata admission was rejected.");
        }

        return memory;
    }

    private OperationUnit AdmitOperationUnit(string id)
    {
        if (id is null) { return null; }

        lock (this.operationUnitsGate)
        {
            if (!this.operationUnits.Add(id))
            {
                throw new CacheRelationalException(CacheErrorCode.Conflict, "This provider already has an admitted unit for the tracked operation.");
            }
        }

        return new(this, id);
    }

    private bool HasOperationUnit(string id)
    {
        lock (this.operationUnitsGate) { return this.operationUnits.Contains(id); }
    }

    private static CacheProviderOperation Operation(CacheOperationEntity row)
    {
        if (row.MetadataJson is null || row.MetadataJson.Length > 65536 || row.ClaimNode?.Length > 128)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The native operation metadata is invalid.");
        }

        var operation = JsonSerializer.Deserialize<CacheProviderOperation>(row.MetadataJson);
        if (operation is null || !Enum.IsDefined(operation.State) || operation.Id != row.Id || operation.Revision != row.Revision || (int)operation.State != row.State || operation.Request is null ||
            (operation.State == CacheOperationState.Running && (string.IsNullOrEmpty(row.ClaimNode) || !row.ClaimUntilTicks.HasValue)) ||
            CollectionId(new(operation.Request.Namespace, operation.Request.Collection, "operation", "scope")) != row.CollectionId)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The native operation state does not match its record.");
        }

        return operation;
    }

    private static void ApplyOperation(CacheOperationEntity row, CacheProviderOperation operation, string node, DateTimeOffset now)
    {
        row.MetadataJson = Serialize(operation);
        row.Revision = operation.Revision;
        row.State = (int)operation.State;
        row.UpdatedTicks = operation.UpdatedAt.UtcTicks;
        row.ClaimNode = operation.State == CacheOperationState.Running ? node : null;
        row.ClaimUntilTicks = operation.State == CacheOperationState.Running ? now.AddSeconds(30).UtcTicks : null;
    }

    private static bool SameOperation(CacheProviderOperation previous, CacheProviderOperation next) =>
        previous.Cutoff == next.Cutoff && previous.Request.Clear == next.Request.Clear &&
        StringComparer.OrdinalIgnoreCase.Equals(previous.Request.Namespace, next.Request.Namespace) &&
        StringComparer.OrdinalIgnoreCase.Equals(previous.Request.Collection, next.Request.Collection) &&
        (previous.Request.Clear || SelectionFingerprint(previous.Request.Selection, default) == SelectionFingerprint(next.Request.Selection, default));

    private static bool OperationTransition(CacheOperationState previous, CacheOperationState next) => previous == next || previous switch
    {
        CacheOperationState.Queued => next is CacheOperationState.Running or CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Failed,
        CacheOperationState.Running => next is CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Completed or CacheOperationState.Failed,
        CacheOperationState.Paused => next is CacheOperationState.Queued or CacheOperationState.Cancelled,
        CacheOperationState.Cancelled or CacheOperationState.Failed => next == CacheOperationState.Queued,
        _ => false
    };

    private sealed class OperationUnit(EntityFrameworkCacheStoreProvider owner, string id) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            lock (owner.operationUnitsGate) { owner.operationUnits.Remove(id); }
        }
    }

    // Pressure reclamation.

    private async ValueTask ReclaimPressureAsync(DbContext context, CacheStoreEntity store, CacheCollectionEntity collection,
        CacheProviderCollection definition, string protectedEntry, CancellationToken token)
    {
        if (definition.Policies.Eviction != CacheEvictionMode.Evict) { return; }

        var used = store.CurrentBytes + store.RetiredBytes + store.StagingBytes + store.MetadataBytes;
        var childUsed = collection.CurrentBytes + collection.RetiredBytes + collection.StagingBytes + collection.MetadataBytes;
        if (used < store.MaximumBytes * 0.9 && childUsed < collection.MaximumBytes * 0.9 &&
            store.CurrentEntries < store.MaximumEntries && collection.CurrentEntries < collection.MaximumEntries) { return; }

        if (!ResourceCharge.TryReserve(256 * CacheDefaults.KiB, out var memory, this.transient, this.ResidentFor(definition))) { return; }

        using (memory)
        {
            var budget = new CleanupBudget(128, this.time.GetUtcNow().Add(definition.Policies.Limits.MaxQueryWork), this.time);
            await this.PurgePayloadsAsync(context, store, this.time.GetUtcNow().UtcTicks, budget, token).ConfigureAwait(false);
            if (!budget.CanStart) { return; }

            var now = this.time.GetUtcNow().UtcTicks;
            var eligible = context.Set<CacheEntryEntity>().Where(e => e.CollectionId == collection.Id && e.Id != protectedEntry &&
                !context.Set<CacheReaderEntity>().Any(r => r.PayloadId == e.PayloadId));
            var expired = await eligible.Where(e => e.ExpiryTicks <= now).OrderBy(e => e.ExpiryTicks).ThenBy(e => e.Id)
                .Select(e => new PressureCandidate(e.Id.Substring(0, 65), e.ExpiryTicks, e.Priority, e.LastAccessTicks)).Take(64).ToArrayAsync(token).ConfigureAwait(false);
            var ranked = await eligible.OrderBy(e => e.Priority).ThenBy(e => e.LastAccessTicks).ThenBy(e => e.Id)
                .Select(e => new PressureCandidate(e.Id.Substring(0, 65), e.ExpiryTicks, e.Priority, e.LastAccessTicks)).Take(64).ToArrayAsync(token).ConfigureAwait(false);
            var candidates = expired.Concat(ranked).DistinctBy(e => e.Id).OrderBy(e => e.Expiry <= now ? 0 : 1)
                .ThenBy(e => e.Priority).ThenBy(e => e.Activity).ToArray();
            foreach (var candidate in candidates)
            {
                CacheRelationalProjection.ValidateScalar(candidate.Id, 64);
                if (!budget.CanStart || PressureRecovered(store, collection)) { break; }

                await this.RetireEntriesAsync(context, context.Set<CacheEntryEntity>().Where(e => e.Id == candidate.Id), collection.Id, token).ConfigureAwait(false);
                budget.Use();
                await this.PurgePayloadsAsync(context, store, now, budget, token).ConfigureAwait(false);
            }
        }
    }

    private static bool PressureRecovered(CacheStoreEntity store, CacheCollectionEntity collection) =>
        store.CurrentBytes + store.RetiredBytes + store.StagingBytes + store.MetadataBytes <= store.MaximumBytes * 0.8 &&
        collection.CurrentBytes + collection.RetiredBytes + collection.StagingBytes + collection.MetadataBytes <= collection.MaximumBytes * 0.8 &&
        store.CurrentEntries <= Math.Min(store.MaximumEntries - 1, (long)(store.MaximumEntries * 0.8)) &&
        collection.CurrentEntries <= Math.Min(collection.MaximumEntries - 1, (long)(collection.MaximumEntries * 0.8));

    private sealed record PressureCandidate(string Id, long Expiry, int Priority, long Activity);

    // Maintenance and physical reclamation.

    /// <summary>Reclaims bounded indexed metadata and chunk batches. Protected generations remain charged; cleanup never loads payload content.</summary>
    public ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteMetadataAsync(async (context, token) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.BatchSize is <= 0 or > 1024) { throw new CacheRelationalException(CacheErrorCode.Validation, "The native cleanup batch bound is invalid."); }

            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var workMemory, this.transient, this.resident))
            {
                throw new CacheRelationalException(CacheErrorCode.Capacity, "Bounded native cleanup memory admission was rejected.");
            }

            using var memory = workMemory;
            var budget = new CleanupBudget(Math.Min(request.BatchSize, 128), request.NewWorkDeadline, this.time);
            if (!budget.CanStart) { return new CacheMaintenanceOutcome(0, 0, true, 0); }

            await using var transaction = await this.LockStoreAsync(context, token).ConfigureAwait(false);
            var store = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheStoreEntity>().Where(e => e.Id == "root"), true, token).ConfigureAwait(false);
            var now = this.time.GetUtcNow();
            await this.FlushActivityAsync(context, budget, token).ConfigureAwait(false);
            await this.PurgeReadersAsync(context, store, now.UtcTicks, budget, token).ConfigureAwait(false);
            var retired = await this.PurgeExpiredEntriesAsync(context, request.RetentionCutoff, now, budget, token).ConfigureAwait(false);
            var released = await this.PurgePayloadsAsync(context, store, now.UtcTicks, budget, token).ConfigureAwait(false);
            await this.PurgeAuthorityAsync(context, store, now, budget, token).ConfigureAwait(false);
            await this.PurgeOperationsAsync(context, store, now, budget, token).ConfigureAwait(false);
            await this.PurgeKeysAsync(context, store, budget, token).ConfigureAwait(false);
            await context.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new CacheMaintenanceOutcome(retired, released, !budget.CanStart, 0);
        }, cancellationToken);

    private async ValueTask PurgeReadersAsync(DbContext context, CacheStoreEntity store, long now, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return; }

        var expired = await context.Set<CacheReaderEntity>().Where(e => e.ExpiryTicks <= now).OrderBy(e => e.ExpiryTicks).ThenBy(e => e.Id)
            .Join(context.Set<CachePayloadEntity>(), e => e.PayloadId, p => p.Id, (e, p) => new { Id = e.Id.Substring(0, 33), CollectionId = p.CollectionId.Substring(0, 65) }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        foreach (var row in expired)
        {
            CacheRelationalProjection.ValidateScalar(row.Id, 32);
            CacheRelationalProjection.ValidateScalar(row.CollectionId, 64);
            if (!budget.CanStart) { break; }

            var removed = await context.Set<CacheReaderEntity>().Where(e => e.Id == row.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            if (removed != 0) { await this.ReleaseMetadataAsync(context, store, row.CollectionId, 256, token).ConfigureAwait(false); }

            budget.Use();
        }
    }

    private async ValueTask<long> PurgeExpiredEntriesAsync(DbContext context, DateTimeOffset requestedCutoff, DateTimeOffset now,
        CleanupBudget budget, CancellationToken token)
    {
        var retired = 0L;
        if (!budget.CanStart) { return retired; }

        var cutoff = Math.Min(requestedCutoff.UtcTicks, now.UtcTicks);
        var candidates = await context.Set<CacheEntryEntity>().Where(e => e.PurgeAfterTicks <= cutoff).OrderBy(e => e.PurgeAfterTicks).ThenBy(e => e.Id)
            .Select(e => new { Id = e.Id.Substring(0, 65), CollectionId = e.CollectionId.Substring(0, 65) }).Take(budget.Remaining).ToArrayAsync(token).ConfigureAwait(false);
        foreach (var candidate in candidates)
        {
            CacheRelationalProjection.ValidateScalar(candidate.Id, 64);
            CacheRelationalProjection.ValidateScalar(candidate.CollectionId, 64);
        }

        foreach (var group in candidates.GroupBy(e => e.CollectionId))
        {
            if (!budget.CanStart) { break; }

            var ids = group.Select(e => e.Id).ToArray();
            var removed = await this.RetireEntriesAsync(context, context.Set<CacheEntryEntity>().Where(e => ids.Contains(e.Id)), group.Key, token).ConfigureAwait(false);
            retired += removed.Count;
            budget.Use(ids.Length);
        }

        return retired;
    }

    private async ValueTask<long> PurgePayloadsAsync(DbContext context, CacheStoreEntity store, long now, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return 0; }

        var candidates = await context.Set<CachePayloadEntity>().Where(e => (e.State == 2 || e.State == 3 || (e.State == 0 && e.DeadlineTicks <= now)) &&
                !context.Set<CacheReaderEntity>().Any(r => r.PayloadId == e.Id))
            .OrderBy(e => e.DeadlineTicks).ThenBy(e => e.Id)
            .Select(e => new { Id = e.Id.Substring(0, 33), CollectionId = e.CollectionId.Substring(0, 65), e.State, e.SizeBytes, e.ReservedBytes, e.MetadataBytes }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        var released = 0L;
        foreach (var row in candidates)
        {
            CacheRelationalProjection.ValidateScalar(row.Id, 32);
            CacheRelationalProjection.ValidateScalar(row.CollectionId, 64);
            if (!budget.CanStart) { break; }

            // Revoke expired staging before touching its chunks. Every future write rechecks State under the same store authority.
            if (row.State == 0)
            {
                await context.Set<CachePayloadEntity>().Where(e => e.Id == row.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.State, 3), token).ConfigureAwait(false);
            }

            var chunks = await context.Set<CacheChunkEntity>().Where(e => e.PayloadId == row.Id).OrderBy(e => e.Ordinal)
                .Select(e => e.Ordinal).Take(budget.Remaining).ToArrayAsync(token).ConfigureAwait(false);
            if (chunks.Length != 0)
            {
                await context.Set<CacheChunkEntity>().Where(e => e.PayloadId == row.Id && chunks.Contains(e.Ordinal)).ExecuteDeleteAsync(token).ConfigureAwait(false);
                var metadata = 4096L * chunks.Length;
                await context.Set<CachePayloadEntity>().Where(e => e.Id == row.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.MetadataBytes, e => e.MetadataBytes - metadata), token).ConfigureAwait(false);
                await this.ReleaseMetadataAsync(context, store, row.CollectionId, metadata, token).ConfigureAwait(false);
                budget.Use(chunks.Length);
            }

            if (!budget.CanStart || await context.Set<CacheChunkEntity>().AnyAsync(e => e.PayloadId == row.Id, token).ConfigureAwait(false)) { continue; }

            var bytes = row.State == 2 ? row.SizeBytes : row.ReservedBytes;
            var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == row.CollectionId), true, token).ConfigureAwait(false);
            if (row.State == 2) { store.RetiredBytes -= bytes; collection.RetiredBytes -= bytes; }
            else { store.StagingBytes -= bytes; collection.StagingBytes -= bytes; }

            await this.ReleaseMetadataAsync(context, store, row.CollectionId, row.MetadataBytes - 4096L * chunks.Length, token).ConfigureAwait(false);
            await context.Set<CachePayloadEntity>().Where(e => e.Id == row.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            released += bytes;
            budget.Use();
        }

        return released;
    }

    private async ValueTask PurgeAuthorityAsync(DbContext context, CacheStoreEntity store, DateTimeOffset now, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return; }

        var expired = await context.Set<CacheOwnershipEntity>().Where(e => e.DeadlineTicks <= now.UtcTicks).OrderBy(e => e.DeadlineTicks).ThenBy(e => e.Id)
            .Select(e => new { Id = e.Id.Substring(0, 33), CollectionId = e.CollectionId.Substring(0, 65) }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        foreach (var header in expired)
        {
            CacheRelationalProjection.ValidateScalar(header.Id, 32);
            CacheRelationalProjection.ValidateScalar(header.CollectionId, 64);
            if (!budget.CanStart) { break; }

            var flight = await this.ReadOwnershipAsync(context, context.Set<CacheOwnershipEntity>().Where(e => e.Id == header.Id), true, token).ConfigureAwait(false);
            if (flight is null) { continue; }

            var interests = CacheOwnershipInterestState.Read(flight.InterestsJson);
            var removed = Math.Min(interests.Count, budget.Remaining);
            if (removed != 0)
            {
                interests.RemoveRange(0, removed);
                flight.InterestsJson = CacheOwnershipInterestState.Write(interests);
                await this.ReleaseMetadataAsync(context, store, flight.CollectionId, (long)CacheOwnershipInterestState.InterestMetadataBytes * removed, token).ConfigureAwait(false);
                budget.Use(removed);
            }

            if (!budget.CanStart || interests.Count != 0) { continue; }

            context.Remove(flight);
            await this.ReleaseMetadataAsync(context, store, flight.CollectionId, 8192, token).ConfigureAwait(false);
            budget.Use();
        }

        if (!budget.CanStart) { return; }

        var fences = await context.Set<CacheFenceEntity>().Where(e => e.DeadlineTicks <= now.UtcTicks).OrderBy(e => e.DeadlineTicks).ThenBy(e => e.Id)
            .Select(e => new { Id = e.Id.Substring(0, 33), CollectionId = e.CollectionId.Substring(0, 65) }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        foreach (var fence in fences)
        {
            CacheRelationalProjection.ValidateScalar(fence.Id, 32);
            CacheRelationalProjection.ValidateScalar(fence.CollectionId, 64);
            if (!budget.CanStart) { break; }

            await context.Set<CacheFenceEntity>().Where(e => e.Id == fence.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            await this.ReleaseMetadataAsync(context, store, fence.CollectionId, 1536, token).ConfigureAwait(false);
            budget.Use();
        }
    }

    private async ValueTask PurgeOperationsAsync(DbContext context, CacheStoreEntity store, DateTimeOffset now, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return; }

        var cutoff = now.Subtract(this.diagnostics.OperationRetention).UtcTicks;
        var rows = await context.Set<CacheOperationEntity>().Where(e => e.UpdatedTicks <= cutoff &&
                (e.State == (int)CacheOperationState.Completed || e.State == (int)CacheOperationState.Failed || e.State == (int)CacheOperationState.Cancelled))
            .OrderBy(e => e.UpdatedTicks).ThenBy(e => e.Id).Select(e => new { Id = e.Id.Substring(0, 33), CollectionId = e.CollectionId.Substring(0, 65) }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        foreach (var row in rows)
        {
            CacheRelationalProjection.ValidateScalar(row.Id, 32);
            CacheRelationalProjection.ValidateScalar(row.CollectionId, 64);
            if (!budget.CanStart) { break; }

            await context.Set<CacheOperationEntity>().Where(e => e.Id == row.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            await this.ReleaseMetadataAsync(context, store, row.CollectionId, OperationMetadataBytes, token).ConfigureAwait(false);
            budget.Use();
        }
    }

    private async ValueTask PurgeKeysAsync(DbContext context, CacheStoreEntity store, CleanupBudget budget, CancellationToken token)
    {
        if (!budget.CanStart) { return; }

        var rows = await context.Set<CacheKeyEntity>().Where(e => !context.Set<CacheEntryEntity>().Any(i => i.KeyId == e.Id) &&
                !context.Set<CacheFenceEntity>().Any(f => f.KeyId == e.Id) && !context.Set<CacheOwnershipEntity>().Any(o => o.KeyId == e.Id))
            .OrderBy(e => e.Id).Select(e => new { Id = e.Id.Substring(0, 65), CollectionId = e.CollectionId.Substring(0, 65), CanonicalKey = e.CanonicalKey.Substring(0, 4097) }).Take(budget.Remaining).ToListAsync(token).ConfigureAwait(false);
        foreach (var row in rows)
        {
            CacheRelationalProjection.ValidateScalar(row.Id, 64);
            CacheRelationalProjection.ValidateScalar(row.CollectionId, 64);
            CacheRelationalProjection.ValidateScalar(row.CanonicalKey, 4096);
            if (!budget.CanStart) { break; }

            await context.Set<CacheKeyEntity>().Where(e => e.Id == row.Id).ExecuteDeleteAsync(token).ConfigureAwait(false);
            await this.ReleaseMetadataAsync(context, store, row.CollectionId, 512 + Encoding.UTF8.GetByteCount(row.CanonicalKey), token).ConfigureAwait(false);
            budget.Use();
        }
    }

    private async ValueTask ReleaseMetadataAsync(DbContext context, CacheStoreEntity store, string collectionId, long bytes, CancellationToken token)
    {
        var collection = await CacheRelationalProjection.SingleAsync(context, context.Set<CacheCollectionEntity>().Where(e => e.Id == collectionId), true, token).ConfigureAwait(false);
        if (bytes < 0 || bytes > store.MetadataBytes || bytes > collection.MetadataBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "Native cleanup accounting is inconsistent; charges remain reserved.");
        }

        store.MetadataBytes -= bytes;
        collection.MetadataBytes -= bytes;
    }

    private sealed class CleanupBudget(int maximum, DateTimeOffset deadline, TimeProvider time)
    {
        /// <summary>Gets the remaining physical cleanup allowance for this pass.</summary>
        public int Remaining { get; private set; } = maximum;

        /// <summary>Gets the remaining physical cleanup allowance for this pass.</summary>
        public bool CanStart => this.Remaining > 0 && time.GetUtcNow() < deadline;

        /// <summary>Gets the remaining physical cleanup allowance for this pass.</summary>
        public void Use(int count = 1) => this.Remaining -= count;
    }

    // Native execution and error translation.

    private async ValueTask<Result<T>> ExecuteMetadataAsync<T>(Func<DbContext, CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken)
    {
        var admitted = await this.queries.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure) { return Result<T>.Failure(admitted); }

        using (admitted.Value) { return await this.ExecuteAsync(work, cancellationToken).ConfigureAwait(false); }
    }

    private async ValueTask<Result<T>> ExecuteAsync<T>(Func<DbContext, CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var admitted = await this.calls.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure) { return Result<T>.Failure(admitted); }

        using (admitted.Value)
        {
            try
            {
                var context = this.CreateContext();
                var completed = false;
                try
                {
                    var result = await work(context, cancellationToken).ConfigureAwait(false);
                    completed = true;
                    return Result<T>.Success(result);
                }
                finally
                {
                    try { await context.DisposeAsync().ConfigureAwait(false); }
                    catch when (completed)
                    {
                        CacheRelationalLog.Deferred(this.logger, this.Name, "context-cleanup");
                    }
                }
            }
            catch (CacheRelationalException exception) { return Result<T>.Failure(Error(exception.Code, exception.Message)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
            {
                return Result<T>.Failure(Error(CacheErrorCode.ProviderUnavailable, "The selected cache database operation is unavailable."));
            }
            catch (JsonException) { return Result<T>.Failure(Error(CacheErrorCode.Corruption, "Persisted cache metadata is invalid.")); }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return Result<T>.Failure(Error(CacheErrorCode.Validation, "The native cache request violates its declared contract."));
            }
        }
    }

    private ResourceBudget ResidentFor(CacheProviderCollection definition) => this.resources?.ForCollection(definition.Collection).Resident ?? this.resident;

    // Bound persisted JSON on the server, including trailing spaces that SQL Server LEN ignores.
    // The extra character preserves oversize detection before any truncated value can be used.
    private static IQueryable<CacheEntryEntity> BoundedEntries(IQueryable<CacheEntryEntity> query) => CacheRelationalProjection.Select(query);

    private static async ValueTask<CacheEntryEntity> ReadEntryAsync(DbContext context, IQueryable<CacheEntryEntity> query,
        bool tracking, CancellationToken token)
    {
        return await CacheRelationalProjection.SingleOrDefaultAsync(context, query, tracking, token).ConfigureAwait(false);
    }

    private static IQueryable<CacheOperationEntity> BoundedOperations(IQueryable<CacheOperationEntity> query) => CacheRelationalProjection.Select(query);

    private static async ValueTask<CacheOperationEntity> ReadOperationAsync(DbContext context, IQueryable<CacheOperationEntity> query,
        bool tracking, CancellationToken token)
    {
        return await CacheRelationalProjection.SingleOrDefaultAsync(context, query, tracking, token).ConfigureAwait(false);
    }

    private static CacheEntryMetadata Metadata(CacheEntryEntity entry, CacheEntryIdentity? expected = null)
    {
        CacheRelationalProjection.Validate(entry);
        if (entry.MetadataJson is null || entry.MetadataJson.Length > 65536 || !Guid.TryParseExact(entry.PayloadId, "N", out _))
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "Persisted cache metadata exceeds its format bounds.");
        }

        CacheEntryMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<CacheEntryMetadata>(entry.MetadataJson) ??
                throw new CacheRelationalException(CacheErrorCode.Corruption, "Persisted cache metadata is missing.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "Persisted cache metadata is invalid.");
        }

        if ((expected.HasValue && metadata.Identity != expected.Value) || metadata.Identity.Digest != entry.Id ||
            metadata.Version.Value != entry.PayloadId || metadata.PublishedAt.UtcTicks != entry.PublishedTicks || metadata.ExpiresAt.UtcTicks != entry.ExpiryTicks ||
            metadata.AbsoluteExpiresAt.UtcTicks != entry.AbsoluteExpiryTicks || metadata.StoredSizeBytes != entry.StoredBytes || metadata.IsNotFound != entry.IsNotFound ||
            metadata.RefreshFailureCount < 0 || metadata.RefreshNotBefore?.UtcTicks != entry.RefreshBackoffTicks)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The acquired cache identity or metadata snapshot is inconsistent.");
        }

        return metadata.LastAccessAt.UtcTicks == entry.LastAccessTicks && metadata.HasForegroundAccess == entry.HasForegroundAccess ? metadata :
            metadata with { LastAccessAt = new(entry.LastAccessTicks, TimeSpan.Zero), HasForegroundAccess = entry.HasForegroundAccess };
    }
}