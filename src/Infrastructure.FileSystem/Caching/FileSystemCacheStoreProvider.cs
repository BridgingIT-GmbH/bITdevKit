// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Collections.Frozen;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using System.Security.Cryptography;
using System.Text;

/// <summary>Owns a native advanced-cache root, checked metadata indexes, immutable payload generations, and durable quota authority.</summary>
/// <remarks>The root belongs exclusively to this cache format. Shared filesystem profiles require their documented native qualification.</remarks>
/// <example><code>var provider = new FileSystemCacheStoreProvider("files", new() { Kind = "FileSystem", RootPath = absoluteRoot });</code></example>
public sealed class FileSystemCacheStoreProvider : ICacheStoreProvider, IAsyncDisposable
{
    private readonly FileSystemCacheOptions profile;
    private readonly CacheRuntimeResources resources;
    private readonly ResourceBudget resident;
    private readonly ResourceBudget transient;
    private readonly CacheIndexBlockCache blocks;
    private readonly CacheStoreRoot root;
    private readonly SemaphoreSlim initialization = new(1);
    private readonly CacheAdmission calls;
    private readonly TimeProvider time;
    private readonly string node = Guid.NewGuid().ToString("N");
    private readonly CacheAdmission readers;
    private readonly CacheAdmission stages;
    private readonly CacheAdmission metadataQueries;
    private readonly CacheDiagnosticsOptions diagnostics;
    private readonly Lock lifetime = new();
    private readonly List<ResourceCharge> catalogMemory = [];
    private FrozenDictionary<string, CacheProviderCollection> definitions;
    private CacheStoreHeader header;
    private CacheRootQuotaLedger ledger;
    private byte[] cursorKey;
    private int active;
    private bool disposed;
    private bool released;

    /// <summary>Validates native settings without accessing the selected root. Store initialization is explicit and lazy.</summary>
    public FileSystemCacheStoreProvider(string name, CacheProviderOptions options, TimeProvider timeProvider = null, CacheRuntimeResources resources = null,
        CacheDiagnosticsOptions diagnostics = null)
    {
        this.Name = CacheIdentityNormalizer.Name(name);
        this.profile = new(options);
        this.resources = resources;
        this.time = timeProvider ?? TimeProvider.System;
        this.diagnostics = diagnostics ?? new();
        this.resident = resources?.ForProvider(this.Name) ?? new(this.Name, options.MaxResidentBytes);
        this.transient = resources?.Transient ?? new("filesystem-transient", 64 * CacheDefaults.MiB);
        this.blocks = new(options.MetadataCacheBytes);
        this.root = new(this.profile, this.transient, this.resident);
        this.calls = new("filesystem-native", 64, 256, timeProvider: this.time);
        this.readers = new("filesystem-readers", 8, 0, timeProvider: this.time);
        this.stages = new("filesystem-stages", 64, 0, timeProvider: this.time);
        this.metadataQueries = new("filesystem-metadata", 4, 0, timeProvider: this.time);
        this.Capabilities = new(true, true, true, true, true, !this.profile.Shared);
    }

    /// <summary>Gets the normalized trusted provider alias.</summary>
    public string Name { get; }

    /// <summary>Gets native persistence, coordination, forward reading and private seek capabilities.</summary>
    public CacheProviderCapabilities Capabilities { get; }

    /// <summary>Gets or sets an optional deterministic fault callback for native publication probes; ordinary registrations leave it null.</summary>
    /// <remarks>The callback executes synchronously on real native paths. It may stop a disposable probe process at a named boundary; it must never contain application logic.</remarks>
    /// <example><code>provider.Fault = boundary => { if (boundary == "manifest-published") { throw new IOException("Probe acknowledgement loss."); } };</code></example>
    public Action<string> Fault { get; set; }

    /// <summary>Validates a native profile and reports its capabilities without opening a root or creating files.</summary>
    /// <param name="options">The finalized named provider settings.</param>
    /// <returns>Complete persistent-store capabilities for the selected native access profile.</returns>
    /// <example><code>var capabilities = FileSystemCacheStoreProvider.CapabilitiesFor(options);</code></example>
    public static CacheProviderCapabilities CapabilitiesFor(CacheProviderOptions options)
    {
        var selected = new FileSystemCacheOptions(options);
        return new(true, true, true, true, true, !selected.Shared);
    }

    /// <summary>Checks the native profile and binds immutable declared contracts while preserving compatible retained versions.</summary>
    public async ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default)
    {
        if (collections is null || collections.Count > 1024) { return Result.Failure(Error(CacheErrorCode.Validation)); }

        await this.initialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var lifetimeLease = this.TryEnter();
        if (lifetimeLease is null) { this.initialization.Release(); return Result.Failure(Error(CacheErrorCode.ProviderUnavailable)); }

        var retained = new List<ResourceCharge>();
        try
        {
            if (!ResourceCharge.TryReserve(3 * CacheDefaults.MiB + 16384L * collections.Count, out var preparing, this.transient, this.resident))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            using var preparationMemory = preparing;
            var contracts = collections.Select(CacheNativeContract.From).ToArray();
            var declared = contracts.ToFrozenDictionary(c => c.Identity.Digest, c => new CacheProviderCollection(c.Identity.Namespace, c.Identity.Collection,
                c.Identity.ContractVersion, c.ContractId, c.Dimensions, c.Policies), StringComparer.Ordinal);
            if (this.definitions is not null)
            {
                return this.definitions.Count == declared.Count && declared.All(p => this.definitions.TryGetValue(p.Key, out var previous) &&
                    JsonSerializer.Serialize(previous) == JsonSerializer.Serialize(p.Value)) ? Result.Success() : Result.Failure(Error(CacheErrorCode.Compatibility));
            }

            foreach (var contract in contracts)
            {
                using var buffer = new CacheMetadataBuffer();
                JsonSerializer.Serialize(buffer, contract);
                if (!ResourceCharge.TryReserve(32768 + 33 * buffer.Length, out var memory, this.resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                retained.Add(memory);
            }

            using var acquired = await this.root.OpenAsync(cancellationToken).ConfigureAwait(false);
            this.header = acquired.Value;
            var nativeLedger = new CacheRootQuotaLedger(this.root, this.header, acquired.Bytes, this.transient, this.resident, this.blocks, this.time);
            foreach (var definition in declared.Values)
            {
                await nativeLedger.RegisterCollectionAsync(definition, cancellationToken).ConfigureAwait(false);
                await nativeLedger.RegisterContractAsync(definition, cancellationToken).ConfigureAwait(false);
            }

            this.cursorKey = await nativeLedger.GetCursorKeyAsync(cancellationToken).ConfigureAwait(false);
            this.header = await this.root.MarkInitializedAsync(this.header, cancellationToken).ConfigureAwait(false);
            this.ledger = nativeLedger;
            this.catalogMemory.AddRange(retained);
            retained.Clear();
            this.definitions = declared;
            return Result.Success();
        }
        catch (CacheTransferException exception) { return Result.Failure(Error(exception.Code)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException) { return Result.Failure(Error(CacheErrorCode.ProviderUnavailable)); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or OverflowException) { return Result.Failure(Error(CacheErrorCode.Validation)); }
        finally
        {
            foreach (var memory in retained) { memory.Dispose(); }

            this.initialization.Release();
        }
    }

    /// <summary>Reads bounded native accounting, retaining uncertain credits and protected data in the reported footprint.</summary>
    public ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.ledger.GetQuotaAsync(token), cancellationToken);

    /// <summary>Stops admission. Occupied native work and protected readers keep their actual resources until termination.</summary>
    public ValueTask DisposeAsync()
    {
        lock (this.lifetime) { this.disposed = true; this.ReleaseIfIdle(); }

        return ValueTask.CompletedTask;
    }

    private async ValueTask<Result<T>> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lifetimeLease = this.TryEnter();
        if (lifetimeLease is null || this.definitions is null) { return Result<T>.Failure(Error(CacheErrorCode.ProviderUnavailable)); }

        if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var memory, this.transient, this.resident)) { return Result<T>.Failure(Error(CacheErrorCode.Capacity)); }

        using (memory)
        {
            var admitted = await this.calls.AcquireAsync(this.profile.Native.AttemptTimeout, cancellationToken).ConfigureAwait(false);
            if (admitted.IsFailure) { return Result<T>.Failure(admitted); }

            using (admitted.Value)
            {
                try { return Result<T>.Success(await work(cancellationToken).ConfigureAwait(false)); }
                catch (CacheTransferException exception) { return Result<T>.Failure(Error(exception.Code)); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException) { return Result<T>.Failure(Error(CacheErrorCode.ProviderUnavailable)); }
                catch (Exception exception) when (exception is ArgumentException or JsonException or OverflowException) { return Result<T>.Failure(Error(CacheErrorCode.Validation)); }
            }
        }
    }

    private LifetimeLease TryEnter()
    {
        lock (this.lifetime)
        {
            if (this.disposed) { return null; }

            this.active++;
            return new(this);
        }
    }

    private void Leave()
    {
        lock (this.lifetime) { this.active--; this.ReleaseIfIdle(); }
    }

    private void ReleaseIfIdle()
    {
        if (!this.disposed || this.active != 0 || this.released) { return; }

        this.released = true;
        this.blocks.Dispose();
        foreach (var memory in this.catalogMemory) { memory.Dispose(); }

        this.catalogMemory.Clear();
    }

    private CacheProviderCollection Definition(CacheEntryIdentity identity)
    {
        var key = new CacheEntryIdentity(identity.Namespace, identity.Collection, identity.ContractVersion, "catalog").Digest;
        if (this.definitions is null || !this.definitions.TryGetValue(key, out var definition) || !definition.Policies.Enabled) { throw new CacheTransferException(CacheErrorCode.Contract); }

        return definition;
    }

    private ResourceBudget ResidentFor(CacheProviderCollection definition) => this.resources?.ForCollection(definition.Collection).Resident ?? this.resident;

    private CacheShardStore Shard(CacheEntryIdentity identity) => this.Shard(identity, CacheStoreRoot.Shard(identity));

    private CacheShardStore Shard(CacheEntryIdentity scope, int shard) => new(this.root, this.root.ShardDirectory(scope, shard), this.header.StoreId,
        scope.CollectionIdentity, shard, this.header.RecoveryGeneration, this.transient, this.resident, this.blocks) { Fault = this.Fault };

    private static CacheError Error(CacheErrorCode code) => new(code, "The native filesystem cache operation could not complete.");

    private sealed class LifetimeLease(FileSystemCacheStoreProvider provider) : IDisposable
    {
        private int disposed;

        public void Dispose() { if (Interlocked.Exchange(ref this.disposed, 1) == 0) { provider.Leave(); } }
    }

    private async ValueTask<bool> TryCompactShardAsync(CacheEntryIdentity scope, int shard, CacheProviderCollection definition, CancellationToken token)
    {
        CacheManifest input;
        long inputBytes;
        await using (var transaction = await this.AcquireVerifiedAsync(scope, false, null, token, shard).ConfigureAwait(false))
        {
            if (transaction is null || (transaction.Manifest.Segments.Count < 32 && transaction.Manifest.JournalBytes < this.profile.Native.CheckpointBytes)) { return false; }

            input = transaction.Manifest with { Segments = transaction.Manifest.Segments.ToArray() };
            inputBytes = transaction.Manifest.MetadataCharge;
        }

        var identity = MaintenanceIdentity(scope, shard);
        var operationId = Guid.NewGuid().ToString("N");
        await using var mutation = await CacheNativeMutation.CreateAsync(this.ledger, this.Shard(scope, shard), identity, this.node, null, operationId,
            this.transient, this.ResidentFor(definition), token, CacheSpaceCreditKind.Compaction).ConfigureAwait(false);
        var plan = new CacheNativeCompactionPlan(input.Generation, input.Sequence, inputBytes, input.Segments.Select(s => s.Id).ToArray(), input.Journal, operationId);
        await this.ledger.AttachCompactionAsync(mutation.CreditId, this.node, plan, token).ConfigureAwait(false);
        await using (var prepared = await CacheShardCheckpoint.PrepareAsync(this.Shard(scope, shard), this.ReaderRegistry(identity), mutation.Commit,
            operationId, operationId, mutation.ReserveAsync, this.transient, this.ResidentFor(definition), token, input.Generation).ConfigureAwait(false))
        {
            var output = prepared.Output;
            var epoch = await this.ledger.GetEpochAsync(scope.CollectionIdentity, token).ConfigureAwait(false);
            var bytes = checked(output.Segments.Sum(s => s.IndexBytes + s.RecordsBytes) + 8192);
            await prepared.PublishAsync((publish, ct) => this.ledger.PublishAsync(mutation.CreditId, this.node, epoch,
                new(MetadataBytes: bytes), publish, ct, output.Sequence, input.Generation, input.Sequence), token).ConfigureAwait(false);
        }

        await this.CollectCompactionAsync(await this.ledger.GetCreditAsync(mutation.CreditId, token).ConfigureAwait(false), token).ConfigureAwait(false);
        return true;
    }

    private async ValueTask<bool> CollectCompactionAsync(CacheSpaceCredit credit, CancellationToken token)
    {
        if (credit.Kind != CacheSpaceCreditKind.Compaction || credit.State != CacheSpaceCreditState.Committed || credit.Compaction is null ||
            credit.CompactionReclaimed) { return false; }

        await using var transaction = await this.AcquireVerifiedAsync(credit.Identity, false, null, token).ConfigureAwait(false);
        if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var directory = this.root.ShardDirectory(credit.Identity);
        var readers = this.ReaderRegistry(credit.Identity);
        foreach (var id in credit.Compaction.Segments)
        {
            if (transaction.Manifest.Segments.Any(s => s.Id == id) || await readers.IsPinnedAsync(transaction.Authority, "index", id, token).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (transaction.Manifest.Journal == credit.Compaction.Journal) { return false; }

        foreach (var id in credit.Compaction.Segments)
        {
            foreach (var suffix in new[] { ".idx", ".records" }) { DeleteNativeFile(Path.Combine(directory, id + suffix)); }
        }

        DeleteNativeFile(Path.Combine(directory, credit.Compaction.Journal + ".journal"));
        FileSystemDurability.SyncDirectory(directory);
        await this.ledger.ReclaimCompactionAsync(credit.Id, token).ConfigureAwait(false);
        return true;
    }

    /// <summary>Fences already captured source work, then conditionally retires one bounded metadata batch without reading payloads.</summary>
    /// <remarks>Persist and pass the returned continuation. Tracked batches commit their continuation with native publication recovery.</remarks>
    /// <example><code>var outcome = await provider.InvalidateAsync(new("catalog", "products", selection, DateTimeOffset.UtcNow), cancellationToken);</code></example>
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.ScopeDefinition(request.Namespace, request.Collection);
            var scope = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
            var selection = request.Clear ? CacheSelection.Pattern("*") : request.Selection;
            var schemas = await this.ledger.ValidateSelectionAsync(scope, selection, token).ConfigureAwait(false);
            var fingerprint = SelectionHash(selection, token);
            var purpose = (request.Clear ? "clear:" : "invalidate:") + (request.OperationId ?? "direct");
            if (request.OperationId is not null)
            {
                using var status = await this.ledger.GetOperationAsync(request.OperationId, token).ConfigureAwait(false);
                var operation = status.Value.Value;
                if (operation.Request.Namespace != definition.Namespace || operation.Request.Collection != definition.Collection ||
                    operation.Request.Clear != request.Clear || operation.Cutoff != request.Cutoff) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                var cancelled = await this.ledger.ObserveCancellationBoundaryAsync(request.OperationId, request.Continuation, this.node, token).ConfigureAwait(false);
                if (cancelled is not null) { return cancelled; }
            }

            CacheFileQueryCursor cursor;
            if (request.Continuation is null)
            {
                var started = await this.ledger.BeginInvalidationAsync(scope, fingerprint, request.OperationId, this.node, boundary =>
                {
                    var initial = new CacheFileQueryCursor(1, this.header.StoreId, this.header.RecoveryGeneration, scope.CollectionIdentity, fingerprint,
                        request.Cutoff.UtcTicks, purpose, selection.IsEmpty ? 256 : 0, null, boundary.Epoch, boundary.MaximumSequence, 0, 0,
                        definition.Policies.Limits.MaxQueryCandidates, false);
                    return new() { Completed = selection.IsEmpty, Continuation = selection.IsEmpty ? null : this.EncodeCursor(initial), OperationId = request.OperationId };
                }, token, advance: !selection.IsEmpty).ConfigureAwait(false);
                if (started.Completed || request.OperationId is not null) { return started; }

                cursor = this.DecodeCursor(started.Continuation, scope, fingerprint, request.Cutoff, purpose);
            }
            else { cursor = this.DecodeCursor(request.Continuation, scope, fingerprint, request.Cutoff, purpose); }

            var admitted = await this.metadataQueries.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
            if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            using (admitted.Value)
            {
                if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var scratch, this.transient, this.ResidentFor(definition)))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (scratch)
                {
                    return await this.InvalidateBatchAsync(request, scope, definition, selection, new(selection, schemas, request.Cutoff),
                        fingerprint, cursor, token).ConfigureAwait(false);
                }
            }
        }, cancellationToken);

    private async ValueTask<CacheInvalidationOutcome> InvalidateBatchAsync(CacheProviderInvalidation request, CacheEntryIdentity scope,
        CacheProviderCollection definition, CacheSelection selection, CacheSelectionEvaluator evaluator, string fingerprint,
        CacheFileQueryCursor initial, CancellationToken token)
    {
        var cursor = initial;
        var started = this.time.GetTimestamp();
        var plan = CacheFileSelectionPlan.Build(scope, selection);
        var tracked = request.OperationId is not null;
        var allowance = Math.Min(128, tracked ? initial.MaximumCandidates : initial.MaximumCandidates - initial.Examined);
        var examined = 0;
        CacheNativeMutation mutation = null;
        CacheShardTransaction transaction = null;
        try
        {
            while (cursor.Shard < 256 && cursor.MaximumSequence[cursor.Shard] == 0) { cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null }; }

            long affected = 0;
            long bytes = 0;
            if (cursor.Shard < 256 && allowance > 0)
            {
                transaction = await this.AcquireVerifiedAsync(scope, false, null, token, cursor.Shard).ConfigureAwait(false);
                if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                cursor = cursor with { Changed = cursor.Changed || transaction.Manifest.Sequence != cursor.MaximumSequence[cursor.Shard] };
                var lower = cursor.LastKey is null ? plan.Range.Lower : new CacheIndexKey(Convert.FromBase64String(cursor.LastKey)).After();
                var ended = true;
                if (lower is not null && (plan.Range.Upper is null || lower.CompareTo(plan.Range.Upper) < 0))
                {
                    // Gather bounded candidate pointers before staging writes; the snapshot scanner intentionally rejects mutated transactions.
                    var candidates = new List<(CacheIndexKey Key, bool Deleted, byte[] Pointer)>();
                    await foreach (var candidate in transaction.ScanAsync(lower, allowance, token, plan.Range.Upper).ConfigureAwait(false))
                    {
                        if (this.time.GetElapsedTime(started) >= definition.Policies.Limits.MaxQueryWork) { ended = false; break; }

                        candidates.Add((candidate.Key, candidate.Deleted, plan.Primary ? null : candidate.Bytes.ToArray()));
                    }

                    if (candidates.Count == allowance) { ended = false; }

                    foreach (var candidate in candidates)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!candidate.Deleted)
                        {
                            var primary = plan.Primary ? candidate.Key : candidate.Pointer.Length == 48 ? CacheMetadataIndex.Primary(candidate.Pointer.AsSpan(0, 32)) :
                                throw new CacheTransferException(CacheErrorCode.Corruption);
                            using var document = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, primary, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                            if (document is not null)
                            {
                                var entry = document.Value;
                                entry.Validate(this.header.StoreId);
                                if (!CacheMetadataIndex.Primary(entry.Metadata.Identity).Equals(primary) || entry.Metadata.Identity.CollectionIdentity != scope.CollectionIdentity ||
                                    CacheStoreRoot.Shard(entry.Metadata.Identity) != cursor.Shard) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                                var versionMatches = plan.Primary || entry.Metadata.Version.Value == new Guid(candidate.Pointer.AsSpan(32, 16), bigEndian: true).ToString("N");
                                if (versionMatches && entry.PublicationSequence <= cursor.MaximumSequence[cursor.Shard] && evaluator.Matches(entry.Metadata))
                                {
                                    mutation ??= await CacheNativeMutation.CreateAsync(this.ledger, this.Shard(scope, cursor.Shard), entry.Metadata.Identity, this.node, null,
                                        Guid.NewGuid().ToString("N"), this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                                    this.RetireEntry(transaction, entry, definition);
                                    affected++;
                                    bytes = checked(bytes + entry.Metadata.StoredSizeBytes);
                                }
                            }
                        }

                        examined++;
                        cursor = cursor with { LastKey = Convert.ToBase64String(candidate.Key.Bytes) };
                    }
                }

                if (ended) { cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null }; }
            }

            while (cursor.Shard < 256 && cursor.MaximumSequence[cursor.Shard] == 0) { cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null }; }

            var epoch = await this.ledger.GetEpochAsync(scope.CollectionIdentity, token).ConfigureAwait(false);
            cursor = cursor with { Observed = checked(cursor.Observed + affected), Examined = tracked ? 0 : cursor.Examined + examined,
                Changed = cursor.Changed || epoch != cursor.Epoch };
            var completed = cursor.Shard == 256;
            var outcome = new CacheInvalidationOutcome { Completed = completed, AffectedCount = cursor.Observed, CountIsExact = completed && !cursor.Changed,
                Continuation = completed ? null : this.EncodeCursor(cursor), OperationId = request.OperationId };
            var progress = tracked ? await this.ledger.CreateTrackedProgressAsync(request.OperationId, fingerprint, this.node, request.Continuation, outcome, token).ConfigureAwait(false) : null;
            if (mutation is not null)
            {
                await mutation.CommitAsync(transaction, epoch, new(-bytes, bytes, transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId), -affected),
                    token, shardEpoch: Math.Max(epoch, transaction.Manifest.Epoch), trackedProgress: progress).ConfigureAwait(false);
            }
            else if (progress is not null) { await this.ledger.SaveTrackedProgressAsync(progress, token).ConfigureAwait(false); }

            return outcome;
        }
        finally
        {
            try { if (transaction is not null) { await transaction.DisposeAsync().ConfigureAwait(false); } }
            finally { if (mutation is not null) { await mutation.DisposeAsync().ConfigureAwait(false); } }
        }
    }

    private readonly SemaphoreSlim maintenance = new(1);
    private CacheIndexKey maintenanceCredit;
    private int maintenanceScope;
    private int maintenanceShard;
    private CacheIndexKey maintenanceRow;
    private int maintenanceKind;

    /// <summary>Runs a bounded fair metadata sweep and native staging reconciliation while preserving every occupied reader and uncertain credit.</summary>
    /// <remarks>Shared orphan ownership requires explicit quiescent recovery; elapsed lease time alone cannot authorize deletion.</remarks>
    /// <example><code>var result = await provider.MaintainAsync(new(128, now, now.AddSeconds(5)), cancellationToken);</code></example>
    public ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.BatchSize is <= 0 or > 1000) { throw new CacheTransferException(CacheErrorCode.Validation); }

            if (request.NewWorkDeadline <= this.time.GetUtcNow()) { return new CacheMaintenanceOutcome(0, 0, true, 0); }

            if (!await this.maintenance.WaitAsync(0, token).ConfigureAwait(false)) { return new(0, 0, true, 0); }

            try
            {
                if (!ResourceCharge.TryReserve(4 * CacheDefaults.MiB, out var memory, this.transient, this.resident))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (memory)
                {
                    long reclaimed = 0;
                    long released = 0;
                    long uncertain = 0;
                    var started = this.time.GetTimestamp();
                    var work = request.NewWorkDeadline - this.time.GetUtcNow();
                    await this.ledger.MaintainOperationsAsync(this.diagnostics.MaximumOperationRecords, this.diagnostics.OperationRetention, token).ConfigureAwait(false);
                    using var credits = await this.ledger.GetCreditsAsync(this.maintenanceCredit, Math.Min(16, request.BatchSize), token).ConfigureAwait(false);
                    this.maintenanceCredit = credits.Continuation;
                    foreach (var credit in credits.Credits)
                    {
                        if (this.time.GetUtcNow() >= request.NewWorkDeadline || this.time.GetElapsedTime(started) >= work) { break; }

                        var result = await this.CleanCreditAsync(credit, token).ConfigureAwait(false);
                        reclaimed += result.ReclaimedEntries;
                        released += result.ReclaimedBytes;
                        uncertain += result.UncertainOperations;
                    }

                    var scopes = this.definitions.Values.Where(d => d.Policies.Enabled).GroupBy(d => (d.Namespace, d.Collection)).Select(g => g.First())
                        .OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Collection, StringComparer.Ordinal).ToArray();
                    var examined = 0;
                    var visited = 0;
                    while (scopes.Length != 0 && examined < request.BatchSize && visited < Math.Min(1536, request.BatchSize * 6) &&
                        this.time.GetUtcNow() < request.NewWorkDeadline && this.time.GetElapsedTime(started) < work)
                    {
                        this.maintenanceScope %= scopes.Length;
                        var definition = scopes[this.maintenanceScope];
                        var scope = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                        var result = await this.MaintainShardAsync(scope, definition, request, request.BatchSize - examined, token).ConfigureAwait(false);
                        reclaimed += result.ReclaimedEntries;
                        released += result.ReclaimedBytes;
                        examined += result.Examined;
                        visited++;
                    }

                    return new CacheMaintenanceOutcome(reclaimed, released, true, uncertain);
                }
            }
            finally { this.maintenance.Release(); }
        }, cancellationToken);

    private async ValueTask<CacheMaintenanceOutcome> CleanCreditAsync(CacheSpaceCredit observed, CancellationToken token)
    {
        if (observed.Kind == CacheSpaceCreditKind.Reader && observed.State == CacheSpaceCreditState.Reserved)
        {
            await using var pinned = await this.AcquireVerifiedAsync(observed.Identity, false, null, token).ConfigureAwait(false);
            if (pinned is null) { return new(0, 0, false, 1); }

            var reclaimed = await this.ReaderRegistry(observed.Identity).ReclaimLocalOrphanAsync(pinned.Authority, observed.Id, token).ConfigureAwait(false);
            return new(0, reclaimed ? observed.ReservedBytes : 0, false, 0);
        }

        if (observed.Kind == CacheSpaceCreditKind.Payload && observed.State == CacheSpaceCreditState.Committed && observed.NativeEnded)
        {
            await this.TryForgetPayloadCreditAsync(observed, token).ConfigureAwait(false);
            return new(0, 0, false, 0);
        }

        if (observed.Kind == CacheSpaceCreditKind.Compaction && observed.State == CacheSpaceCreditState.Committed)
        {
            await this.CollectCompactionAsync(observed, token).ConfigureAwait(false);
            await this.ledger.ForgetMetadataCreditAsync(observed.Id, token).ConfigureAwait(false);
            return new(0, 0, false, 0);
        }

        if (observed.State == CacheSpaceCreditState.Released || (observed.Kind == CacheSpaceCreditKind.Metadata &&
            observed.State == CacheSpaceCreditState.Committed && observed.NativeEnded))
        {
            var endedOwners = Path.Combine(this.root.ShardDirectory(observed.Identity), "stages");
            FileSystemPaths.RejectLinks(endedOwners);
            if (Directory.Exists(endedOwners))
            {
                var ended = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, endedOwners, observed.Owner);
                if (ended is null) { return new(0, 0, false, 1); }

                ended.Dispose();
                if (!this.profile.Shared) { DeleteNativeFile(Path.Combine(endedOwners, observed.Owner)); }
            }

            await this.ledger.ForgetMetadataCreditAsync(observed.Id, token).ConfigureAwait(false);
            return new(0, 0, false, 0);
        }

        if (observed.State is CacheSpaceCreditState.Committed or CacheSpaceCreditState.Prepared || observed.Kind == CacheSpaceCreditKind.Reader)
        {
            return new(0, 0, false, observed.State == CacheSpaceCreditState.Prepared ? 1 : 0);
        }

        var directory = this.root.ShardDirectory(observed.Identity);
        if (observed.NativeEnded && !Directory.Exists(directory))
        {
            // A durable actual-end proof can outlive a failed pre-staging directory create.
            // No committed shard may disappear behind an empty-directory observation.
            if (await this.ledger.GetReceiptAsync(observed.Identity, token).ConfigureAwait(false) is not null)
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            await this.ledger.ReleaseUnpublishedAsync(observed.Id, observed.NodeId, token).ConfigureAwait(false);
            return new(0, observed.ReservedBytes, false, 0);
        }

        var owners = Path.Combine(directory, "stages");
        FileSystemPaths.RejectLinks(owners);
        if (!Directory.Exists(owners)) { return new(0, 0, false, 1); }

        var ownerPath = Path.Combine(owners, observed.Owner);
        if (!observed.NativeEnded && (this.profile.Shared || !File.Exists(ownerPath))) { return new(0, 0, false, 1); }

        using var owner = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, owners, observed.Owner);
        if (owner is null) { return new(0, 0, false, 1); }

        await using var transaction = await this.AcquireVerifiedAsync(observed.Identity, false, null, token).ConfigureAwait(false);
        if (transaction is null) { return new(0, 0, false, 1); }

        var credit = await this.ledger.GetCreditAsync(observed.Id, token).ConfigureAwait(false);
        if (credit.State != CacheSpaceCreditState.Reserved || credit.Owner != observed.Owner ||
            (credit.Commit is not null && transaction.Manifest.Segments.Any(s => s.Id == credit.Commit.SegmentId))) { return new(0, 0, false, 1); }

        if (credit.Version is not null)
        {
            var definition = this.ScopeDefinition(credit.Identity.Namespace, credit.Identity.Collection);
            using var current = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(credit.Identity),
                this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (current?.Value.Metadata.Version.Value == credit.Version || await this.ReaderRegistry(credit.Identity).IsPinnedAsync(transaction.Authority,
                "payload", credit.Version, token).ConfigureAwait(false)) { return new(0, 0, false, 1); }

            var values = Path.Combine(directory, "values");
            if (Directory.Exists(values))
            {
                using var payloadOwner = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, values, "stage-" + credit.Version);
                if (payloadOwner is null) { return new(0, 0, false, 1); }

                foreach (var suffix in new[] { ".payload", ".payload.tmp", ".payload.json", ".payload.json.write.tmp" })
                {
                    DeleteNativeFile(Path.Combine(values, credit.Version + suffix));
                }

                FileSystemDurability.SyncDirectory(values);
            }
        }

        if (credit.Commit is not null)
        {
            foreach (var suffix in new[] { ".idx", ".idx.tmp", ".records", ".records.tmp" })
            {
                DeleteNativeFile(Path.Combine(directory, credit.Commit.SegmentId + suffix));
            }
        }

        if (credit.Compaction is not null && credit.Compaction.OutputJournal != transaction.Manifest.Journal)
        {
            DeleteNativeFile(Path.Combine(directory, credit.Compaction.OutputJournal + ".journal"));
            DeleteNativeFile(Path.Combine(directory, credit.Compaction.OutputJournal + ".journal.tmp"));
        }

        var journalPath = Path.Combine(directory, transaction.Manifest.Journal + ".journal");
        await using (var journal = FileSystemFileOperations.Open(journalPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            journal.SetLength(transaction.Manifest.JournalBytes);
            FileSystemDurability.Flush(journal);
        }

        FileSystemDurability.SyncDirectory(directory);
        await this.ledger.ReleaseUnpublishedAsync(credit.Id, credit.NodeId, token, transaction.Manifest.Sequence == 0 ? 8192 : 0).ConfigureAwait(false);
        return new(1, Math.Max(0, credit.ReservedBytes - (transaction.Manifest.Sequence == 0 ? 8192 : 0)), false, 0);
    }

    private async ValueTask TryForgetPayloadCreditAsync(CacheSpaceCredit credit, CancellationToken token)
    {
        await using var transaction = await this.AcquireVerifiedAsync(credit.Identity, false, null, token).ConfigureAwait(false);
        if (transaction is null) { return; }

        var definition = this.ScopeDefinition(credit.Identity.Namespace, credit.Identity.Collection);
        using var current = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(credit.Identity),
            this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
        current?.Value.Validate(this.header.StoreId, credit.Identity);
        if (current?.Value.Metadata.Version.Value == credit.Version) { return; }

        using var retired = await transaction.ReadAsync(CacheNativeRetiredPayload.Key(credit.Version), token).ConfigureAwait(false);
        using var receipt = await transaction.ReadAsync(CacheNativePublicationReceipt.Key(credit.Version), token).ConfigureAwait(false);
        if ((retired is not null && !retired.Deleted) || (receipt is not null && !receipt.Deleted)) { return; }

        var directory = this.root.ShardDirectory(credit.Identity);
        foreach (var suffix in new[] { ".payload", ".payload.tmp", ".payload.json", ".payload.json.write.tmp" })
        {
            var path = Path.Combine(directory, "values", credit.Version + suffix);
            FileSystemPaths.RejectLinks(path);
            if (File.Exists(path)) { return; }
        }

        foreach (var owner in new[] { (Directory: Path.Combine(directory, "stages"), Name: credit.Owner),
            (Directory: Path.Combine(directory, "values"), Name: "stage-" + credit.Version) })
        {
            var path = Path.Combine(owner.Directory, owner.Name);
            FileSystemPaths.RejectLinks(path);
            if (!File.Exists(path)) { continue; }

            using var proof = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, owner.Directory, owner.Name);
            if (proof is null) { return; }

            proof.Dispose();
            if (!this.profile.Shared) { File.Delete(path); }

            FileSystemDurability.SyncDirectory(owner.Directory);
        }

        await this.ledger.ForgetPayloadCreditAsync(credit.Id, credit.Identity, credit.Version, token).ConfigureAwait(false);
    }

    private static void DeleteNativeFile(string path)
    {
        FileSystemPaths.RejectLinks(path);
        File.Delete(path);
    }

    private void AdvanceMaintenanceShard()
    {
        this.maintenanceRow = null;
        this.maintenanceKind = 0;
        if (++this.maintenanceShard == 256) { this.maintenanceShard = 0; this.maintenanceScope++; }
    }

    /// <summary>Conditionally creates native shared ownership or registers one bounded foreign observation group.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            var definition = this.ValidateOwnershipRequest(request);
            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var scratch, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            using (scratch)
            {
                var transaction = await this.AcquireVerifiedAsync(request.Identity, false, null, token).ConfigureAwait(false);
                CacheNativeMutation mutation = null;
                try
                {
                    if (transaction is null)
                    {
                        mutation = await this.NewMutationAsync(request.Identity, definition, token).ConfigureAwait(false);
                        transaction = await this.AcquireVerifiedAsync(request.Identity, true, mutation.ReserveAsync, token).ConfigureAwait(false);
                    }

                    var epoch = await this.ledger.GetEpochAsync(request.Identity.CollectionIdentity, token).ConfigureAwait(false);
                    var owners = await this.OwnersAsync(transaction, request.Identity, definition, token).ConfigureAwait(false);
                    CacheNativeOwnership shared = null;
                    var lost = new List<CacheNativeOwnership>();
                    foreach (var owner in owners.Where(o => o.State == CacheOwnershipState.Owned))
                    {
                        if (!await this.OwnerValidAsync(transaction, owner, definition, epoch, token).ConfigureAwait(false))
                        {
                            lost.Add(owner with { State = CacheOwnershipState.Lost, Failure = CacheErrorCode.ProviderUnavailable, CompletedAt = this.time.GetUtcNow() });
                        }
                        else if (!owner.Independent)
                        {
                            if (shared is not null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                            shared = owner;
                        }
                    }

                    var joined = shared is not null && !request.Independent;
                    var flight = joined ? shared : null;
                    CacheNativeInterest interest = null;
                    if (joined)
                    {
                        using var existing = await CacheNativeRecords.ReadAsync<CacheNativeInterest>(transaction, CacheNativeInterest.Key(flight.FlightId, request.InterestId),
                            this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                        existing?.Value.Validate(flight, request.InterestId);
                        interest = existing?.Value;
                        if (interest is not null && lost.Count == 0) { return Ownership(flight, false); }

                        if (interest is null) { await this.CheckInterestCapacityAsync(transaction, flight, definition, token).ConfigureAwait(false); }
                    }
                    else if (owners.Count >= 128) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                    mutation ??= await this.NewMutationAsync(request.Identity, definition, token).ConfigureAwait(false);
                    foreach (var owner in lost) { CacheNativeRecords.Set(transaction, CacheNativeOwnership.Key(request.Identity, owner.OwnerToken), owner, this.transient, this.ResidentFor(definition)); }

                    if (!joined)
                    {
                        var fence = await this.CaptureFenceAsync(transaction, request.Identity, definition, epoch, token).ConfigureAwait(false);
                        var expires = this.time.GetUtcNow().Add(this.profile.Native.LeaseLifetime);
                        if (expires > request.Deadline) { expires = request.Deadline; }

                        flight = new(1, request.Identity, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), fence.Id, request.NodeId,
                            CacheOwnershipState.Owned, request.Deadline, expires, request.Independent);
                        CacheNativeRecords.Set(transaction, CacheNativeOwnership.Key(request.Identity, flight.OwnerToken), flight, this.transient, this.ResidentFor(definition));
                        CacheNativeRecords.Set(transaction, FlightKey(request.Identity, flight.FlightId), new CacheNativeFlightPointer(1, request.Identity, flight.FlightId, flight.OwnerToken),
                            this.transient, this.ResidentFor(definition));
                    }

                    if (interest is null)
                    {
                        CacheNativeRecords.Set(transaction, CacheNativeInterest.Key(flight.FlightId, request.InterestId),
                            new CacheNativeInterest(1, request.Identity, flight.FlightId, request.InterestId, request.Deadline < flight.Deadline ? request.Deadline : flight.Deadline),
                            this.transient, this.ResidentFor(definition));
                    }

                    await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
                    return Ownership(flight, !joined);
                }
                finally
                {
                    try { if (transaction is not null) { await transaction.DisposeAsync().ConfigureAwait(false); } }
                    finally { if (mutation is not null) { await mutation.DisposeAsync().ConfigureAwait(false); } }
                }
            }
        }, cancellationToken);

    /// <summary>Renews confirmed authority only while its original deadline, fence and interested groups remain valid.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            using var stored = await CacheNativeRecords.ReadAsync<CacheNativeOwnership>(transaction, CacheNativeOwnership.Key(identity, owner.Value), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (stored is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var flight = stored.Value;
            flight.Validate(identity, owner.Value);
            if (flight.State != CacheOwnershipState.Owned) { return Ownership(flight, false); }

            var epoch = await this.ledger.GetEpochAsync(identity.CollectionIdentity, token).ConfigureAwait(false);
            var valid = await this.OwnerValidAsync(transaction, flight, definition, epoch, token).ConfigureAwait(false);
            if (valid && !definition.Policies.Loading.ContinueWithoutWaiters) { valid = await this.HasInterestAsync(transaction, flight, definition, token).ConfigureAwait(false); }

            var expiry = this.time.GetUtcNow().Add(this.profile.Native.LeaseLifetime);
            flight = valid ? flight with { LeaseExpiry = expiry < flight.Deadline ? expiry : flight.Deadline } :
                flight with { State = CacheOwnershipState.Lost, Failure = CacheErrorCode.ProviderUnavailable, CompletedAt = this.time.GetUtcNow() };
            await using var mutation = await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
            CacheNativeRecords.Set(transaction, CacheNativeOwnership.Key(identity, owner.Value), flight, this.transient, this.ResidentFor(definition));
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return Ownership(flight, valid);
        }, cancellationToken);

    /// <summary>Observes a registered flight with no source work, long-held transaction or provider slot between calls.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            var definition = this.ValidateOwnershipRequest(request);
            await using var transaction = await this.AcquireVerifiedAsync(request.Identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { return Lost(flightId); }

            var flight = await this.FlightAsync(transaction, request.Identity, flightId, definition, token).ConfigureAwait(false);
            if (flight is null) { return Lost(flightId); }

            if (flight.State != CacheOwnershipState.Owned) { return Ownership(flight, false); }

            var epoch = await this.ledger.GetEpochAsync(request.Identity.CollectionIdentity, token).ConfigureAwait(false);
            if (!await this.OwnerValidAsync(transaction, flight, definition, epoch, token).ConfigureAwait(false))
            {
                return Ownership(flight with { State = CacheOwnershipState.Lost, Failure = CacheErrorCode.ProviderUnavailable }, false);
            }

            using var interest = await CacheNativeRecords.ReadAsync<CacheNativeInterest>(transaction, CacheNativeInterest.Key(flightId, request.InterestId),
                this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (interest is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            interest.Value.Validate(flight, request.InterestId);
            if (interest.Value.Deadline <= this.time.GetUtcNow()) { return Lost(flightId); }

            return Ownership(flight, false);
        }, cancellationToken);

    private CacheProviderCollection ValidateOwnershipRequest(CacheOwnershipRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = this.Definition(request.Identity);
        _ = CacheIdentityNormalizer.Name(request.NodeId);
        _ = CacheIdentityNormalizer.Name(request.InterestId);
        var now = this.time.GetUtcNow();
        if (request.Deadline <= now || request.Deadline > now.Add(definition.Policies.Loading.SourceTimeout) || request.LeaseLifetime != this.profile.Native.LeaseLifetime)
        {
            throw new CacheTransferException(CacheErrorCode.SourceTimeout);
        }

        return definition;
    }

    private async ValueTask<List<CacheNativeOwnership>> OwnersAsync(CacheShardTransaction transaction, CacheEntryIdentity identity, CacheProviderCollection definition, CancellationToken token)
    {
        var prefix = new byte[] { 43 }.Concat(Convert.FromHexString(identity.Digest)).ToArray();
        var range = CacheMetadataIndex.PrefixRange(prefix);
        var owners = new List<CacheNativeOwnership>();
        var examined = 0;
        await foreach (var candidate in transaction.ScanAsync(range.Lower, 129, token).ConfigureAwait(false))
        {
            if (!range.Contains(candidate.Key)) { break; }

            if (++examined > 128) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            if (candidate.Deleted) { continue; }

            using var owner = await CacheNativeRecords.ReadAsync<CacheNativeOwnership>(transaction, candidate.Key, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (owner is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            owner.Value.Validate(identity, Convert.ToHexStringLower(candidate.Key.Bytes.Slice(33, 16)));
            owners.Add(owner.Value);
        }

        return owners;
    }

    private async ValueTask<bool> OwnerValidAsync(CacheShardTransaction transaction, CacheNativeOwnership owner, CacheProviderCollection definition, long epoch, CancellationToken token)
    {
        var now = this.time.GetUtcNow();
        if (owner.LeaseExpiry <= now || owner.Deadline <= now) { return false; }

        using var fence = await CacheNativeRecords.ReadAsync<CacheNativeFence>(transaction, CacheNativeFence.Key(owner.Identity, owner.FenceId), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (fence is null) { return false; }

        fence.Value.Validate(owner.Identity, owner.FenceId);
        using var key = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(owner.Identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (key is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        key.Value.Validate(owner.Identity);
        return key.Value.Epoch == fence.Value.KeyEpoch && epoch == fence.Value.RootEpoch && fence.Value.Deadline > now;
    }

    private async ValueTask<CacheNativeOwnership> FlightAsync(CacheShardTransaction transaction, CacheEntryIdentity identity, string flightId,
        CacheProviderCollection definition, CancellationToken token)
    {
        using var pointer = await CacheNativeRecords.ReadAsync<CacheNativeFlightPointer>(transaction, FlightKey(identity, flightId), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (pointer is null) { return null; }

        if (pointer.Value.Format != 1 || pointer.Value.Identity != identity || pointer.Value.FlightId != flightId) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        using var owner = await CacheNativeRecords.ReadAsync<CacheNativeOwnership>(transaction, CacheNativeOwnership.Key(identity, pointer.Value.OwnerToken), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (owner is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        owner.Value.Validate(identity, pointer.Value.OwnerToken);
        if (owner.Value.FlightId != flightId) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        return owner.Value;
    }

    private async ValueTask CheckInterestCapacityAsync(CacheShardTransaction transaction, CacheNativeOwnership flight, CacheProviderCollection definition, CancellationToken token)
    {
        var maximum = Math.Min(4095, definition.Policies.Limits.MaxFollowersPerKey) + 1;
        var range = CacheNativeInterest.Range(flight.FlightId);
        var count = 0;
        await foreach (var candidate in transaction.ScanAsync(range.Lower, maximum + 1, token).ConfigureAwait(false))
        {
            if (!range.Contains(candidate.Key)) { break; }

            if (++count >= maximum) { throw new CacheTransferException(CacheErrorCode.Capacity); }
        }
    }

    private async ValueTask<bool> HasInterestAsync(CacheShardTransaction transaction, CacheNativeOwnership flight, CacheProviderCollection definition, CancellationToken token)
    {
        var range = CacheNativeInterest.Range(flight.FlightId);
        var maximum = Math.Min(4095, definition.Policies.Limits.MaxFollowersPerKey) + 1;
        var examined = 0;
        await foreach (var candidate in transaction.ScanAsync(range.Lower, maximum + 1, token).ConfigureAwait(false))
        {
            if (!range.Contains(candidate.Key)) { break; }

            if (++examined > maximum) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            if (candidate.Deleted) { continue; }

            using var interest = await CacheNativeRecords.ReadAsync<CacheNativeInterest>(transaction, candidate.Key, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (interest is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            interest.Value.Validate(flight, interest.Value.InterestId);
            if (!CacheNativeInterest.Key(flight.FlightId, interest.Value.InterestId).Equals(candidate.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (interest.Value.Deadline > this.time.GetUtcNow()) { return true; }
        }

        return false;
    }

    private static CacheOwnershipObservation Ownership(CacheNativeOwnership flight, bool owner) => new(
        flight.State == CacheOwnershipState.Owned && !owner ? CacheOwnershipState.Pending : flight.State,
        owner && flight.State == CacheOwnershipState.Owned ? new CacheOwnerToken(flight.OwnerToken) : null, new(flight.FenceId), flight.FlightId,
        flight.LeaseExpiry, flight.Version, flight.Failure, flight.IsNotFound);

    private CacheOwnershipObservation Lost(string flightId) => new(CacheOwnershipState.Lost, null, default, flightId, this.time.GetUtcNow(), Failure: CacheErrorCode.ProviderUnavailable);

    private static CacheIndexKey FlightKey(CacheEntryIdentity identity, string flightId)
    {
        if (!Guid.TryParseExact(flightId, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 44 }.Concat(Convert.FromHexString(identity.Digest)).Concat(Convert.FromHexString(flightId)).ToArray());
    }

    /// <summary>Records one exact owner's bounded completion; published versions require native retained publication proof.</summary>
    public async ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            ArgumentNullException.ThrowIfNull(completion);
            if (completion.State is not (CacheOwnershipState.Published or CacheOwnershipState.Unretained or CacheOwnershipState.Failed or CacheOwnershipState.Lost) ||
                (completion.State == CacheOwnershipState.Published) != completion.Version.HasValue || (completion.Failure.HasValue && !Enum.IsDefined(completion.Failure.Value)))
            {
                throw new CacheTransferException(CacheErrorCode.Validation);
            }

            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            using var stored = await CacheNativeRecords.ReadAsync<CacheNativeOwnership>(transaction, CacheNativeOwnership.Key(identity, owner.Value), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (stored is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            var flight = stored.Value;
            flight.Validate(identity, owner.Value);
            if (flight.State != CacheOwnershipState.Owned)
            {
                if (flight.State != completion.State || flight.Version != completion.Version || flight.IsNotFound != completion.IsNotFound)
                {
                    throw new CacheTransferException(CacheErrorCode.Conflict);
                }

                return true;
            }

            var epoch = await this.ledger.GetEpochAsync(identity.CollectionIdentity, token).ConfigureAwait(false);
            if (completion.State == CacheOwnershipState.Published)
            {
                using var proof = await CacheNativeRecords.ReadAsync<CacheNativePublicationReceipt>(transaction, CacheNativePublicationReceipt.Key(completion.Version.Value.Value),
                    this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                if (proof is null || proof.Value.Format != 1 || proof.Value.Identity != identity || proof.Value.Version != completion.Version ||
                    proof.Value.IsNotFound != completion.IsNotFound)
                {
                    throw new CacheTransferException(CacheErrorCode.Indeterminate);
                }

                var credit = await this.ledger.GetCreditAsync(proof.Value.OperationId, token).ConfigureAwait(false);
                if (credit.Kind != CacheSpaceCreditKind.Payload || credit.Identity != identity || credit.Version != completion.Version.Value.Value ||
                    credit.State != CacheSpaceCreditState.Committed)
                {
                    throw new CacheTransferException(CacheErrorCode.Indeterminate);
                }
            }
            else if (completion.State != CacheOwnershipState.Lost && !await this.OwnerValidAsync(transaction, flight, definition, epoch, token).ConfigureAwait(false))
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            flight = flight with { State = completion.State, Version = completion.Version, Failure = completion.Failure, IsNotFound = completion.IsNotFound, CompletedAt = this.time.GetUtcNow() };
            await using var mutation = await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
            CacheNativeRecords.Set(transaction, CacheNativeOwnership.Key(identity, owner.Value), flight, this.transient, this.ResidentFor(definition));
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <summary>Withdraws only one observation group's interest; other callers and actual source occupancy remain independent.</summary>
    public async ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            var key = CacheNativeInterest.Key(flightId, interestId);
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { return true; }

            var flight = await this.FlightAsync(transaction, identity, flightId, definition, token).ConfigureAwait(false);
            if (flight is null) { return true; }

            using var stored = await CacheNativeRecords.ReadAsync<CacheNativeInterest>(transaction, key, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (stored is null) { return true; }

            stored.Value.Validate(flight, interestId);
            await using var mutation = await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
            var epoch = await this.ledger.GetEpochAsync(identity.CollectionIdentity, token).ConfigureAwait(false);
            transaction.Set(key, [], deleted: true);
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private async ValueTask<Result<CacheWriteOutcome>> CommitSessionAsync(CacheFileWriteSession session, CacheEntryMetadata supplied,
        CachePayloadReference payload, CancellationToken cancellationToken)
    {
        var result = await this.ExecuteAsync(async token =>
        {
            var identity = session.Request.Identity;
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var fence = await this.VerifyFenceAsync(transaction, session.Request, session.Definition, token).ConfigureAwait(false);
            await this.CheckCapturedConditionAsync(transaction, session.Request, fence, session.Definition, token).ConfigureAwait(false);
            using var previousDocument = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(identity),
                this.transient, this.ResidentFor(session.Definition), token).ConfigureAwait(false);
            var previous = previousDocument?.Value;
            previous?.Validate(this.header.StoreId, identity);
            using var logical = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(identity), this.transient,
                this.ResidentFor(session.Definition), token).ConfigureAwait(false);
            logical?.Value.Validate(identity);
            var condition = session.Request.Condition;
            if ((condition.CreateOnly && previous is not null && !previous.Metadata.IsNotFound && previous.Metadata.ExpiresAt > this.time.GetUtcNow() && previous.PublicationSequence > (logical?.Value.ReadFenceSequence ?? 0)) ||
                (condition.ExpectedVersion.HasValue && (previous?.Metadata.Version ?? fence.ObservedVersion) != condition.ExpectedVersion))
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            using var exact = await CacheNativeRecords.ReadAsync<CacheNativeEntryState>(transaction, CacheNativeEntryState.Key(identity), this.transient,
                this.ResidentFor(session.Definition), token).ConfigureAwait(false);
            if (exact is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            exact.Value.Validate(identity);
            // Preserve the once-selected durations after finalization and publication-authority admission.
            using var prepared = this.PrepareMetadata(session, CacheExpirationPolicy.Rebase(supplied, this.time.GetUtcNow()), payload);
            var metadata = prepared.Value;
            var lead = Math.Min(session.Definition.Policies.Refresh.RefreshAhead.Ticks, (metadata.AbsoluteExpiresAt - metadata.PublishedAt).Ticks / 5);
            var due = !metadata.IsNotFound && session.Definition.Policies.Refresh.Enabled ? new DateTimeOffset(metadata.AbsoluteExpiresAt.UtcTicks - lead, TimeSpan.Zero) : (DateTimeOffset?)null;
            var entry = new CacheNativeEntry(1, metadata, payload, session.Mutation.CreditId, session.Mutation.Commit.Generation, transaction.NextSequence,
                fence.KeyEpoch, fence.RootEpoch, metadata.ExpiresAt.Add(session.Definition.Policies.Expiration.RetentionGrace), due);
            entry.Validate(this.header.StoreId, metadata.Identity);
            this.IndexEntry(transaction, previous, entry);
            CacheNativeRecords.Set(transaction, CacheNativeEntryState.Key(metadata.Identity), exact.Value with { PublicationSequence = transaction.NextSequence },
                this.transient, this.ResidentFor(session.Definition));
            CacheNativeRecords.Set(transaction, CacheNativePublicationReceipt.Key(metadata.Version.Value),
                new CacheNativePublicationReceipt(1, metadata.Identity, metadata.Version, session.OperationId, metadata.IsNotFound, this.time.GetUtcNow()),
                this.transient, this.ResidentFor(session.Definition));
            if (previous?.Payload is not null)
            {
                CacheNativeRecords.Set(transaction, CacheNativeRetiredPayload.Key(previous.Payload.Version),
                    new CacheNativeRetiredPayload(1, previous.Metadata.Identity, previous.Payload, previous.CreditId, this.time.GetUtcNow()),
                    this.transient, this.ResidentFor(session.Definition));
            }

            var body = payload?.Length ?? 0;
            var physical = payload?.PhysicalBytes ?? 0;
            var previousBytes = previous?.Metadata.StoredSizeBytes ?? 0;
            var metadataGrowth = checked(transaction.EstimateCommitGrowth(session.Mutation.Commit, session.OperationId) + physical - body + (payload is null ? 0 : 4096));
            var change = new CacheQuotaChange(body - previousBytes, previousBytes, metadataGrowth, previous is null ? 1 : 0);
            await session.Mutation.CommitAsync(transaction, fence.RootEpoch, change, token,
                existingStageBytes: checked(physical + (payload is null ? 0 : 4096))).ConfigureAwait(false);
            return new CacheWriteOutcome(CacheStorageOutcome.Stored, metadata.Version, metadata);
        }, cancellationToken).ConfigureAwait(false);
        return result.IsFailure && result.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.Indeterminate) ?
            Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Indeterminate)) : result;
    }

    private CacheMetadataDocument<CacheEntryMetadata> PrepareMetadata(CacheFileWriteSession session, CacheEntryMetadata metadata, CachePayloadReference payload)
    {
        var policy = session.Definition.Policies;
        if (metadata is null || metadata.Identity != session.Request.Identity || metadata.Version != session.Version || metadata.StoredSizeBytes != (payload?.Length ?? 0) ||
            metadata.StoredSizeBytes != session.Written || metadata.LogicalSizeBytes < 0 || metadata.LogicalSizeBytes > policy.Limits.MaxLogicalBytes ||
            metadata.EncodedSizeBytes < 0 || metadata.EncodedSizeBytes > policy.Limits.MaxEncodedBytes || metadata.DecodedSizeBytes is < 0 ||
            metadata.DecodedSizeBytes > policy.Limits.MaxDecodedBytes || metadata.ItemCount is < 0 || !Enum.IsDefined(metadata.Priority) ||
            metadata.EnvelopeVersion <= 0 || metadata.CodecVersion <= 0 || metadata.ContractId != session.Definition.ContractId || metadata.ContentType?.Length > 512 ||
            CacheIdentityNormalizer.Key(metadata.DisplayKey ?? metadata.Identity.Key) != metadata.Identity.Key || metadata.Dimensions is null || metadata.Dimensions.Count > 16)
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        var dimensions = new CacheDimension[metadata.Dimensions.Count];
        for (var index = 0; index < dimensions.Length; index++) { dimensions[index] = metadata.Dimensions[index]; }

        metadata = metadata with { Dimensions = Array.AsReadOnly(dimensions) };
        _ = CacheIdentityNormalizer.Name(metadata.CodecId);
        _ = CacheIdentityNormalizer.Name(metadata.Compression ?? "none");
        var lifetime = metadata.AbsoluteExpiresAt - metadata.PublishedAt;
        var maximum = policy.Expiration.SlidingEnabled ? policy.Expiration.AbsoluteCap : policy.Expiration.AbsoluteTtl;
        if (CacheSelectionValidator.ValidateDimensions(metadata.Dimensions, session.Definition.Dimensions).IsFailure || lifetime <= TimeSpan.Zero || lifetime > maximum ||
            metadata.ExpiresAt <= metadata.PublishedAt || metadata.ExpiresAt > metadata.AbsoluteExpiresAt ||
            (metadata.SlidingIdleTimeout.HasValue && (!policy.Expiration.SlidingEnabled || metadata.SlidingIdleTimeout <= TimeSpan.Zero ||
                metadata.SlidingIdleTimeout > policy.Expiration.SlidingIdleTimeout)) ||
            (metadata.IsNotFound && (!policy.NegativeCaching.Enabled || lifetime > policy.NegativeCaching.Ttl || metadata.SlidingIdleTimeout.HasValue ||
                metadata.StoredSizeBytes != 0 || metadata.LogicalSizeBytes != 0 || metadata.EncodedSizeBytes != 0 || metadata.PayloadHash is not null ||
                metadata.StoredHash is not null || payload is not null)) ||
            (!metadata.IsNotFound && (payload is null || metadata.StoredHash != payload.Hash || metadata.PayloadHash?.Length != 64 || !metadata.PayloadHash.All(char.IsAsciiHexDigit))))
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        if (!ResourceCharge.TryReserve(3 * CacheDefaults.MiB, out var memory, this.transient, this.ResidentFor(session.Definition)))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        try
        {
            var bound = (int)Math.Min(65536, policy.Limits.MaxEntryMetadataBytes);
            if (bound <= 0) { throw new CacheTransferException(CacheErrorCode.Validation); }

            using var buffer = new CacheMetadataBuffer(bound);
            JsonSerializer.Serialize(buffer, metadata);
            var snapshot = JsonSerializer.Deserialize<CacheEntryMetadata>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
            snapshot = snapshot with { Dimensions = Array.AsReadOnly(snapshot.Dimensions.ToArray()) };
            return new(snapshot, buffer.Length, memory);
        }
        catch { memory.Dispose(); throw; }
    }

    /// <summary>Reconciles one exact payload publication without invoking its source or replaying its bytes.</summary>
    public ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) => this.ExecuteAsync(async token =>
    {
        var credit = await this.ledger.GetCreditAsync(operationId, token).ConfigureAwait(false);
        if (credit.Kind != CacheSpaceCreditKind.Payload || credit.Version is null) { throw new CacheTransferException(CacheErrorCode.NotFound); }

        await using var transaction = await this.AcquireVerifiedAsync(credit.Identity, false, null, token).ConfigureAwait(false);
        credit = await this.ledger.GetCreditAsync(operationId, token).ConfigureAwait(false);
        if (credit.State == CacheSpaceCreditState.Released) { return new CacheWriteOutcome(CacheStorageOutcome.Failed); }

        if (credit.State != CacheSpaceCreditState.Committed) { return new CacheWriteOutcome(CacheStorageOutcome.Indeterminate); }

        using var current = transaction is null ? null : await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(credit.Identity),
            this.transient, this.resident, token).ConfigureAwait(false);
        current?.Value.Validate(this.header.StoreId, credit.Identity);
        return new CacheWriteOutcome(CacheStorageOutcome.Stored, new(credit.Version), current?.Value.Metadata.Version.Value == credit.Version ? current.Value.Metadata : null);
    }, cancellationToken);

    /// <summary>Captures durable logical-key and collection authority before source execution or direct same-store staging.</summary>
    public ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            await using var mutation = await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
            var epoch = await this.ledger.GetEpochAsync(identity.CollectionIdentity, token).ConfigureAwait(false);
            await using var transaction = await this.AcquireVerifiedAsync(identity, true, mutation.ReserveAsync, token).ConfigureAwait(false);
            var fence = await this.CaptureFenceAsync(transaction, identity, definition, epoch, token).ConfigureAwait(false);
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return new CachePublicationFence(fence.Id);
        }, cancellationToken);

    /// <summary>Releases one exact captured record after actual source/staging work ends. Repeated release is harmless.</summary>
    public async ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            var fenceKey = CacheNativeFence.Key(identity, fence.Value);
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { return true; }

            using var stored = await CacheNativeRecords.ReadAsync<CacheNativeFence>(transaction, fenceKey, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (stored is null) { return true; }

            stored.Value.Validate(identity, fence.Value);
            using var key = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(identity), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (key is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            key.Value.Validate(identity);
            if (key.Value.Captures <= 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            using var exact = await CacheNativeRecords.ReadAsync<CacheNativeEntryState>(transaction, CacheNativeEntryState.Key(identity), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (exact is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            exact.Value.Validate(identity);
            if (exact.Value.Captures <= 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            await using var mutation = await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
            var epoch = await this.ledger.GetEpochAsync(identity.CollectionIdentity, token).ConfigureAwait(false);
            transaction.Set(fenceKey, [], deleted: true);
            CacheNativeRecords.Set(transaction, CacheNativeKeyState.Key(identity), key.Value with { Captures = key.Value.Captures - 1 }, this.transient, this.ResidentFor(definition));
            CacheNativeRecords.Set(transaction, CacheNativeEntryState.Key(identity), exact.Value with { Captures = exact.Value.Captures - 1 }, this.transient, this.ResidentFor(definition));
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private async ValueTask<CacheNativeFence> CaptureFenceAsync(CacheShardTransaction transaction, CacheEntryIdentity identity,
        CacheProviderCollection definition, long rootEpoch, CancellationToken token)
    {
        using var storedKey = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        var key = storedKey?.Value ?? new CacheNativeKeyState(1, CacheNativeKeyState.LogicalIdentity(identity), 0, 0);
        key.Validate(identity);
        if (key.Captures >= 128) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using var entry = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        entry?.Value.Validate(this.header.StoreId, identity);
        using var storedExact = await CacheNativeRecords.ReadAsync<CacheNativeEntryState>(transaction, CacheNativeEntryState.Key(identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        var exact = storedExact?.Value ?? new CacheNativeEntryState(1, identity, entry?.Value.PublicationSequence ?? 0, 0);
        exact.Validate(identity);
        if (exact.Captures >= 128) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var lifetime = definition.Policies.Streaming.Lifetime > definition.Policies.Loading.SourceTimeout ? definition.Policies.Streaming.Lifetime : definition.Policies.Loading.SourceTimeout;
        var fence = new CacheNativeFence(1, Guid.NewGuid().ToString("N"), identity, key.Epoch, rootEpoch, exact.PublicationSequence,
            entry?.Value.Metadata.Version, this.time.GetUtcNow().Add(lifetime).Add(definition.Policies.Expiration.RetentionGrace));
        CacheNativeRecords.Set(transaction, CacheNativeFence.Key(identity, fence.Id), fence, this.transient, this.ResidentFor(definition));
        CacheNativeRecords.Set(transaction, CacheNativeKeyState.Key(identity), key with { Captures = key.Captures + 1 }, this.transient, this.ResidentFor(definition));
        CacheNativeRecords.Set(transaction, CacheNativeEntryState.Key(identity), exact with { Captures = exact.Captures + 1 }, this.transient, this.ResidentFor(definition));
        return fence;
    }

    private ValueTask<CacheNativeMutation> NewMutationAsync(CacheEntryIdentity identity, CacheProviderCollection definition, CancellationToken token,
        string version = null, string operationId = null) => CacheNativeMutation.CreateAsync(this.ledger, this.Shard(identity), identity, this.node,
            version, operationId ?? Guid.NewGuid().ToString("N"), this.transient, this.ResidentFor(definition), token);

    private async ValueTask<CacheNativeFence> VerifyFenceAsync(CacheShardTransaction transaction, CacheProviderWriteRequest request,
        CacheProviderCollection definition, CancellationToken token)
    {
        var now = this.time.GetUtcNow();
        if (request.Deadline.HasValue && request.Deadline.Value <= now) { throw new CacheTransferException(CacheErrorCode.SourceTimeout); }

        using var stored = await CacheNativeRecords.ReadAsync<CacheNativeFence>(transaction, CacheNativeFence.Key(request.Identity, request.Fence.Value),
            this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
        if (stored is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

        var fence = stored.Value;
        fence.Validate(request.Identity, request.Fence.Value);
        using var key = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(request.Identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (key is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        key.Value.Validate(request.Identity);
        if (fence.Deadline <= now || key.Value.Epoch != fence.KeyEpoch ||
            await this.ledger.GetEpochAsync(request.Identity.CollectionIdentity, token).ConfigureAwait(false) != fence.RootEpoch)
        {
            throw new CacheTransferException(CacheErrorCode.Conflict);
        }

        if (request.Owner.HasValue)
        {
            using var owner = await CacheNativeRecords.ReadAsync<CacheNativeOwnership>(transaction, CacheNativeOwnership.Key(request.Identity, request.Owner.Value.Value),
                this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (owner is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            owner.Value.Validate(request.Identity, request.Owner.Value.Value);
            if (owner.Value.State != CacheOwnershipState.Owned || owner.Value.FenceId != fence.Id || owner.Value.Deadline <= now || owner.Value.LeaseExpiry <= now)
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }
        }

        return fence;
    }

    private async ValueTask CommitMetadataAsync(CacheShardTransaction transaction, CacheNativeMutation mutation, long rootEpoch, CancellationToken token)
    {
        var bytes = transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId);
        await mutation.CommitAsync(transaction, rootEpoch, new(MetadataBytes: bytes), token,
            shardEpoch: Math.Max(rootEpoch, transaction.Manifest.Epoch)).ConfigureAwait(false);
    }

    private const string CursorProtectionPurpose = "BDK.ApplicationCaching.FileSystem.Cursor.V1";

    private string EncodeCursor(CacheFileQueryCursor cursor)
    {
        using var buffer = new CacheMetadataBuffer(16384);
        JsonSerializer.Serialize(buffer, cursor);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        return Convert.ToBase64String(AesGcmContinuationTokenProtector.Protect(bytes, this.cursorKey, CursorProtectionPurpose));
    }

    private CacheFileQueryCursor DecodeCursor(string value, CacheEntryIdentity scope, string selectionHash, DateTimeOffset cutoff, string purpose)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 32768) { throw new CacheTransferException(CacheErrorCode.Validation); }

        try
        {
            var bytes = AesGcmContinuationTokenProtector.Unprotect(Convert.FromBase64String(value), this.cursorKey, CursorProtectionPurpose);

            var cursor = JsonSerializer.Deserialize<CacheFileQueryCursor>(bytes, new JsonSerializerOptions { MaxDepth = 16 }) ?? throw new CacheTransferException(CacheErrorCode.Validation);
            if (cursor.Format != 1 || cursor.StoreId != this.header.StoreId || cursor.RecoveryGeneration != this.header.RecoveryGeneration || cursor.Scope != scope.CollectionIdentity || cursor.SelectionHash != selectionHash ||
                cursor.CutoffTicks != cutoff.UtcTicks || cursor.Purpose != purpose || cursor.Shard is < 0 or > 256 || cursor.Epoch < 0 ||
                cursor.MaximumSequence is null || cursor.MaximumSequence.Count != 256 || cursor.MaximumSequence.Any(v => v < 0) ||
                cursor.Observed < 0 || cursor.Examined < 0 || cursor.MaximumCandidates is <= 0 or > 100000 || cursor.Examined > cursor.MaximumCandidates ||
                (purpose == "query" && cursor.Observed > cursor.Examined) || (cursor.LastKey is not null && (Convert.FromBase64String(cursor.LastKey).Length != CacheIndexKey.Size || cursor.Shard == 256)))
            {
                throw new CacheTransferException(CacheErrorCode.Validation);
            }

            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException or InvalidDataException or CryptographicException)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }
    }

    private static string SelectionHash(CacheSelection selection, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new SelectionDigestStream(hash, token);
        JsonSerializer.Serialize(stream, selection);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private sealed class SelectionDigestStream(IncrementalHash hash, CancellationToken token) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(ReadOnlySpan<byte> buffer) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer); }

        public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>Acquires one indexed current snapshot, checking the durable generation receipt before exposing metadata or bytes.</summary>
    public ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.Definition(identity);
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { return new CacheReadValidation(CacheReadValidationState.Missing, null, default); }

            var document = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(identity), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (document is null) { return new CacheReadValidation(CacheReadValidationState.Missing, null, default); }

            var handedOff = false;
            try
            {
                var entry = document.Value;
                entry.Validate(this.header.StoreId, identity);
                if (entry.Metadata.ContractId != definition.ContractId || CacheSelectionValidator.ValidateDimensions(entry.Metadata.Dimensions, definition.Dimensions).IsFailure)
                {
                    throw new CacheTransferException(CacheErrorCode.Compatibility);
                }

                using var logical = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(identity), this.transient,
                    this.ResidentFor(definition), token).ConfigureAwait(false);
                logical?.Value.Validate(identity);
                if (entry.Metadata.ExpiresAt <= this.time.GetUtcNow() || entry.PublicationSequence <= (logical?.Value.ReadFenceSequence ?? 0))
                {
                    return new(CacheReadValidationState.Missing, null, default);
                }

                if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != entry.Metadata.Version)
                {
                    return new(CacheReadValidationState.Changed, entry.Metadata, default);
                }

                if (request.ObserveAccess && (entry.Metadata.SlidingIdleTimeout.HasValue || !entry.Metadata.HasForegroundAccess ||
                    this.time.GetUtcNow() - entry.Metadata.LastAccessAt >= TimeSpan.FromSeconds(15)))
                {
                    entry = await this.RenewAsync(transaction, entry, definition, token).ConfigureAwait(false);
                }

                if (!request.OpenPayload || entry.Metadata.IsNotFound) { return new(CacheReadValidationState.Valid, entry.Metadata, default); }

                var admitted = await this.readers.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
                if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                var retained = this.TryEnter();
                if (retained is null) { admitted.Value.Dispose(); throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

                try
                {
                    var registry = this.ReaderRegistry(identity);
                    var pin = await registry.RegisterAsync(transaction.Authority, "payload", [entry.Payload.Version], token).ConfigureAwait(false);
                    CachePayloadReader stream;
                    try
                    {
                        // No payload open/read spans shard authority. The native pin now protects this acquired version.
                        await transaction.DisposeAsync().ConfigureAwait(false);
                        var handedPin = pin;
                        pin = null;
                        stream = await CachePayloadReader.OpenAsync(Path.Combine(this.root.ShardDirectory(identity), "values"), this.header.StoreId,
                            entry.Payload, handedPin, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                    }
                    finally { if (pin is not null) { await pin.DisposeAsync().ConfigureAwait(false); } }

                    var handle = new CacheFileReadHandle(stream, entry.Metadata, document, admitted.Value, retained);
                    handedOff = true;
                    return new(CacheReadValidationState.Valid, entry.Metadata, default, handle);
                }
                catch { admitted.Value.Dispose(); retained.Dispose(); throw; }
            }
            finally { if (!handedOff) { document.Dispose(); } }
        }, cancellationToken);

    /// <summary>Validates every retained local reuse against the native committed generation and expected version.</summary>
    public ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion expectedVersion,
        CacheReadRequest request, CancellationToken cancellationToken = default) => this.LookupAsync(identity,
            (request ?? new()) with { ExpectedVersion = expectedVersion }, cancellationToken);

    /// <summary>Renews a valid version's sliding expiry atomically within its absolute cap.</summary>
    public ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? expectedVersion = null,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(async token =>
        {
            var definition = this.Definition(identity);
            await using var transaction = await this.AcquireVerifiedAsync(identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { throw new CacheTransferException(CacheErrorCode.NotFound); }

            using var stored = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(identity), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            if (stored is null) { throw new CacheTransferException(CacheErrorCode.NotFound); }

            stored.Value.Validate(this.header.StoreId, identity);
            using var logical = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(identity), this.transient,
                this.ResidentFor(definition), token).ConfigureAwait(false);
            logical?.Value.Validate(identity);
            if (stored.Value.Metadata.ExpiresAt <= this.time.GetUtcNow() || stored.Value.PublicationSequence <= (logical?.Value.ReadFenceSequence ?? 0))
            {
                throw new CacheTransferException(CacheErrorCode.NotFound);
            }

            if (expectedVersion.HasValue && expectedVersion.Value != stored.Value.Metadata.Version) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            return (await this.RenewAsync(transaction, stored.Value, definition, token).ConfigureAwait(false)).Metadata;
        }, cancellationToken);

    private async ValueTask<CacheShardTransaction> AcquireVerifiedAsync(CacheEntryIdentity identity, bool create,
        Func<long, CancellationToken, ValueTask<Result>> reserve, CancellationToken token, int? selectedShard = null)
    {
        var shard = selectedShard ?? CacheStoreRoot.Shard(identity);
        var transaction = await this.Shard(identity, shard).AcquireAsync(create, ct => this.ledger.GetEpochAsync(identity.CollectionIdentity, ct), reserve, token,
            async ct => await this.ledger.GetReceiptAsync(identity, shard, ct).ConfigureAwait(false) is null).ConfigureAwait(false);
        if (transaction is null) { return null; }

        try
        {
            var receipt = await this.ledger.GetReceiptAsync(identity, shard, token).ConfigureAwait(false);
            if (receipt is null)
            {
                if (transaction.Manifest.Sequence != 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                return transaction;
            }

            if (receipt.State == CacheShardReceiptState.Prepared && receipt.Generation == transaction.Manifest.Generation &&
                receipt.Sequence == transaction.Manifest.Sequence && receipt.CreditId == transaction.Manifest.OperationId)
            {
                var credit = await this.ledger.GetCreditAsync(receipt.CreditId, token).ConfigureAwait(false);
                await this.ledger.ConfirmPreparedAsync(credit.Id, credit.Identity, credit.Version, receipt.Generation, token).ConfigureAwait(false);
                receipt = receipt with { State = CacheShardReceiptState.Committed };
            }

            receipt.Verify(transaction.Manifest);
            return transaction;
        }
        catch { await transaction.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private async ValueTask<CacheNativeEntry> RenewAsync(CacheShardTransaction transaction, CacheNativeEntry entry, CacheProviderCollection definition, CancellationToken token)
    {
        var now = this.time.GetUtcNow();
        var expiry = entry.Metadata.SlidingIdleTimeout.HasValue ? now.Add(entry.Metadata.SlidingIdleTimeout.Value) : entry.Metadata.ExpiresAt;
        if (expiry > entry.Metadata.AbsoluteExpiresAt) { expiry = entry.Metadata.AbsoluteExpiresAt; }

        var renewed = entry with { Metadata = entry.Metadata with { ExpiresAt = expiry, LastAccessAt = now, HasForegroundAccess = true },
            PurgeAfter = expiry.Add(entry.PurgeAfter - entry.Metadata.ExpiresAt) };
        var rootEpoch = await this.ledger.GetEpochAsync(entry.Metadata.Identity.CollectionIdentity, token).ConfigureAwait(false);
        await using var mutation = await CacheNativeMutation.CreateAsync(this.ledger, this.Shard(entry.Metadata.Identity), entry.Metadata.Identity,
            this.node, null, Guid.NewGuid().ToString("N"), this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
        this.IndexEntry(transaction, entry, renewed);
        await mutation.CommitAsync(transaction, rootEpoch, new(MetadataBytes: transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId)), token).ConfigureAwait(false);
        return renewed;
    }

    private void IndexEntry(CacheShardTransaction transaction, CacheNativeEntry previous, CacheNativeEntry entry)
    {
        if (previous is not null)
        {
            foreach (var key in CacheMetadataIndex.Keys(previous.Metadata, previous.PurgeAfter, previous.RefreshDue)) { transaction.Set(key, [], deleted: true); }
        }

        CacheNativeRecords.Set(transaction, CacheMetadataIndex.Primary(entry.Metadata.Identity), entry, this.transient, this.ResidentFor(this.Definition(entry.Metadata.Identity)));
        var pointer = CacheMetadataIndex.Pointer(entry.Metadata);
        foreach (var key in CacheMetadataIndex.Keys(entry.Metadata, entry.PurgeAfter, entry.RefreshDue)) { transaction.Set(key, pointer); }
    }

    private CacheReaderRegistry ReaderRegistry(CacheEntryIdentity identity) => new(this.root.ShardDirectory(identity), this.header.StoreId,
        identity.CollectionIdentity, CacheStoreRoot.Shard(identity), this.header.RecoveryGeneration, this.profile, this.transient,
        this.ResidentFor(this.ScopeDefinition(identity.Namespace, identity.Collection)),
        async (id, bytes, token) =>
        {
            var credit = await this.ledger.ReserveAsync(new() { Id = id, OperationId = id, Identity = identity, NodeId = this.node,
                Owner = "stage-reader-" + id, Kind = CacheSpaceCreditKind.Reader }, Math.Min(bytes, this.profile.Native.QuotaCreditBytes), token).ConfigureAwait(false);
            while (credit.ReservedBytes < bytes)
            {
                credit = await this.ledger.GrowAsync(id, this.node, Math.Min(bytes, checked(credit.ReservedBytes + this.profile.Native.QuotaCreditBytes)), token).ConfigureAwait(false);
            }

            return Result.Success();
        }, (id, _, token) => this.ledger.ReleaseReaderAsync(id, identity, token));

    private sealed class CacheFileReadHandle(CachePayloadReader stream, CacheEntryMetadata metadata, IDisposable document,
        IDisposable admission, IDisposable lifetime) : ICacheProviderRangeReadHandle
    {
        private readonly Lock gate = new();
        private Task disposal;

        public Stream Stream => stream;

        public CacheEntryMetadata Metadata => metadata;

        public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { stream.Seek(storedOffset, SeekOrigin.Begin); return ValueTask.FromResult(Result.Success()); }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException) { return ValueTask.FromResult(Result.Failure(Error(CacheErrorCode.Contract))); }
        }

        public ValueTask DisposeAsync()
        {
            lock (this.gate) { return new(this.disposal ??= this.CloseAsync()); }
        }

        private async Task CloseAsync()
        {
            try { await stream.DisposeAsync().ConfigureAwait(false); }
            finally { document.Dispose(); admission.Dispose(); lifetime.Dispose(); }
        }
    }

    /// <summary>Queries a bounded native due-time index without opening payloads or changing foreground activity.</summary>
    /// <example><code>var page = await provider.QueryRefreshAsync(new("catalog", "cards", "1", now, lead, activity), cancellationToken);</code></example>
    public ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(query);
            var identity = new CacheEntryIdentity(query.Namespace, query.Collection, query.ContractVersion, "catalog");
            var definition = this.Definition(identity);
            if (query.Limit is <= 0 or > 1000 || query.RefreshAhead <= TimeSpan.Zero || query.ActivityWindow <= TimeSpan.Zero ||
                query.RefreshAhead != definition.Policies.Refresh.RefreshAhead || query.ActivityWindow != definition.Policies.Refresh.ActivityWindow)
            {
                throw new CacheTransferException(CacheErrorCode.Validation);
            }

            if (!definition.Policies.Refresh.Enabled) { return new CacheRefreshPage([], null, 0); }

            var admitted = await this.metadataQueries.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
            if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            using (admitted.Value)
            {
                if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var scratch, this.transient, this.ResidentFor(definition)))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (scratch)
                {
                    var selection = CacheSelection.Pattern("*");
                    var fingerprint = SelectionHash(selection, token);
                    var purpose = "refresh:" + identity.ContractVersion;
                    CacheFileQueryCursor cursor;
                    if (query.Continuation is null)
                    {
                        var boundary = await this.ledger.CaptureBoundaryAsync(identity, token).ConfigureAwait(false);
                        cursor = new(1, this.header.StoreId, this.header.RecoveryGeneration, identity.CollectionIdentity, fingerprint, query.Now.UtcTicks, purpose,
                            0, null, boundary.Epoch, boundary.MaximumSequence, 0, 0, 100000, false);
                    }
                    else { cursor = this.DecodeCursor(query.Continuation, identity, fingerprint, query.Now, purpose); }

                    var schemas = await this.ledger.ValidateSelectionAsync(identity, selection, token).ConfigureAwait(false);
                    var metadata = new CacheMetadataQuery(identity.Namespace, identity.Collection, selection, query.Now, query.Limit, MaximumCandidates: query.Limit);
                    var plan = new CacheFileSelectionPlan(CacheMetadataIndex.OrderedRange(CacheMetadataIndex.Refresh, 0, checked(query.Now.UtcTicks + 1)), false, 0);
                    var page = await this.ReadMetadataPageAsync(identity, definition, metadata, new(selection, schemas, query.Now), cursor,
                        this.time.GetTimestamp(), token, plan, item => CacheRefreshPolicy.IsEligible(item, query)).ConfigureAwait(false);
                    return new CacheRefreshPage(page.Page.Entries, page.Page.Completed ? null : this.EncodeCursor(page.Cursor), page.Page.CandidatesExamined);
                }
            }
        }, cancellationToken);

    /// <summary>Persists finite scheduler backoff only for the selected current version; replacement cannot inherit an older failure count.</summary>
    /// <example><code>await provider.RecordRefreshAttemptAsync(new(identity, version, true, DateTimeOffset.UtcNow), cancellationToken);</code></example>
    public async ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(attempt);
            var definition = this.Definition(attempt.Identity);
            await using var transaction = await this.AcquireVerifiedAsync(attempt.Identity, false, null, token).ConfigureAwait(false);
            if (transaction is null) { return true; }

            using var document = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(attempt.Identity),
                this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
            if (document is null) { return true; }

            var entry = document.Value;
            entry.Validate(this.header.StoreId, attempt.Identity);
            if (entry.Metadata.Version != attempt.Version) { return true; }

            var failures = attempt.SourceFailed ? Math.Min(1000000, entry.Metadata.RefreshFailureCount + 1) : 0;
            var next = attempt.SourceFailed ? CacheRefreshPolicy.NextAttempt(definition.Policies.Refresh, failures, attempt.CompletedAt) : (DateTimeOffset?)null;
            var changed = entry with { Metadata = entry.Metadata with { RefreshFailureCount = failures, RefreshNotBefore = next }, RefreshBackoff = next };
            await using var mutation = await this.NewMutationAsync(attempt.Identity, definition, token).ConfigureAwait(false);
            var epoch = await this.ledger.GetEpochAsync(attempt.Identity.CollectionIdentity, token).ConfigureAwait(false);
            this.IndexEntry(transaction, entry, changed);
            await this.CommitMetadataAsync(transaction, mutation, epoch, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <summary>Fences one logical key across retained contracts, including a key which has never held a value.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) => this.RemoveAsync(new CacheProviderRemoval(identity, condition), cancellationToken);

    /// <summary>Checks exact conditions before atomically fencing and retiring the bounded current metadata snapshot.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.Definition(request.Identity);
            if (request.Condition.CreateOnly || (request.OnlyExpectedVersion && !request.Condition.ExpectedVersion.HasValue) ||
                (request.Owner.HasValue && !request.Fence.HasValue) ||
                (request.Condition.ExpectedVersion.HasValue && !Guid.TryParseExact(request.Condition.ExpectedVersion.Value.Value, "N", out _)))
            {
                throw new CacheTransferException(CacheErrorCode.Validation);
            }

            if (request.Deadline.HasValue && request.Deadline.Value <= this.time.GetUtcNow()) { throw new CacheTransferException(CacheErrorCode.SourceTimeout); }

            var transaction = await this.AcquireVerifiedAsync(request.Identity, false, null, token).ConfigureAwait(false);
            CacheNativeMutation mutation = null;
            try
            {
                if (transaction is null)
                {
                    if (request.Condition.ExpectedVersion.HasValue || request.Fence.HasValue) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                    mutation = await this.NewMutationAsync(request.Identity, definition, token).ConfigureAwait(false);
                    transaction = await this.AcquireVerifiedAsync(request.Identity, true, mutation.ReserveAsync, token).ConfigureAwait(false);
                }

                CacheNativeFence fence = null;
                if (request.Fence.HasValue)
                {
                    fence = await this.VerifyFenceAsync(transaction, new(request.Identity, request.Fence.Value, request.Condition, 1,
                        request.Owner, Deadline: request.Deadline), definition, token).ConfigureAwait(false);
                }

                using (var expected = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(request.Identity), this.transient,
                    this.ResidentFor(definition), token).ConfigureAwait(false))
                {
                    expected?.Value.Validate(this.header.StoreId, request.Identity);
                    if (request.Condition.ExpectedVersion.HasValue && expected?.Value.Metadata.Version != request.Condition.ExpectedVersion)
                    {
                        throw new CacheTransferException(CacheErrorCode.Conflict);
                    }
                }

                if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var scratch, this.transient, this.ResidentFor(definition)))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (scratch)
                {
                    var pointers = new List<byte[]>();
                    var examined = 0;
                    var range = CacheMetadataIndex.PrefixRange(CacheMetadataIndex.LogicalPrefix(request.Identity));
                    await foreach (var candidate in transaction.ScanAsync(range.Lower, 1025, token).ConfigureAwait(false))
                    {
                        if (!range.Contains(candidate.Key)) { break; }

                        if (++examined > 1024) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                        if (candidate.Deleted) { continue; }

                        if (candidate.Bytes.Length != 48) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (pointers.Count >= 1024) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                        pointers.Add(candidate.Bytes.ToArray());
                    }

                    using var keyDocument = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(request.Identity), this.transient,
                        this.ResidentFor(definition), token).ConfigureAwait(false);
                    var key = keyDocument?.Value ?? new CacheNativeKeyState(1, CacheNativeKeyState.LogicalIdentity(request.Identity), 0, 0);
                    key.Validate(request.Identity);
                    mutation ??= await this.NewMutationAsync(request.Identity, definition, token).ConfigureAwait(false);
                    var rootEpoch = fence?.RootEpoch ?? await this.ledger.GetEpochAsync(request.Identity.CollectionIdentity, token).ConfigureAwait(false);
                    long affected = 0;
                    long bytes = 0;
                    foreach (var pointer in pointers)
                    {
                        using var stored = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(pointer.AsSpan(0, 32)), this.transient,
                            this.ResidentFor(definition), token).ConfigureAwait(false);
                        if (stored is null) { continue; }

                        var entry = stored.Value;
                        entry.Validate(this.header.StoreId);
                        if (entry.Metadata.Identity.Digest != Convert.ToHexStringLower(pointer.AsSpan(0, 32))) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (entry.Metadata.Version.Value != new Guid(pointer.AsSpan(32, 16), bigEndian: true).ToString("N")) { continue; }

                        if (entry.Metadata.Identity.CollectionIdentity != request.Identity.CollectionIdentity || entry.Metadata.Identity.Key != request.Identity.Key)
                        {
                            throw new CacheTransferException(CacheErrorCode.Corruption);
                        }

                        if (request.OnlyExpectedVersion && entry.Metadata.Identity != request.Identity) { continue; }

                        this.RetireEntry(transaction, entry, definition);
                        affected++;
                        bytes = checked(bytes + entry.Metadata.StoredSizeBytes);
                    }

                    CacheNativeRecords.Set(transaction, CacheNativeKeyState.Key(request.Identity), key with { Epoch = checked(key.Epoch + 1),
                        ReadFenceSequence = request.OnlyExpectedVersion ? key.ReadFenceSequence : transaction.NextSequence }, this.transient, this.ResidentFor(definition));
                    await mutation.CommitAsync(transaction, rootEpoch,
                        new(-bytes, bytes, transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId), -affected), token,
                        shardEpoch: Math.Max(rootEpoch, transaction.Manifest.Epoch)).ConfigureAwait(false);
                    return new CacheInvalidationOutcome { Completed = true, AffectedCount = affected, CountIsExact = true };
                }
            }
            finally
            {
                try { if (transaction is not null) { await transaction.DisposeAsync().ConfigureAwait(false); } }
                finally { if (mutation is not null) { await mutation.DisposeAsync().ConfigureAwait(false); } }
            }
        }, cancellationToken);

    // Secondary rows can be reclaimed incrementally. Every query verifies this primary/version,
    // so no stale pointer can return metadata or an old payload after this atomic tombstone.
    private void RetireEntry(CacheShardTransaction transaction, CacheNativeEntry entry, CacheProviderCollection scope)
    {
        transaction.Set(CacheMetadataIndex.Primary(entry.Metadata.Identity), [], deleted: true);
        if (entry.Payload is not null)
        {
            CacheNativeRecords.Set(transaction, CacheNativeRetiredPayload.Key(entry.Payload.Version),
                new CacheNativeRetiredPayload(1, entry.Metadata.Identity, entry.Payload, entry.CreditId, this.time.GetUtcNow()), this.transient, this.ResidentFor(scope));
        }
    }

    private async ValueTask<NativeMaintenanceBatch> MaintainShardAsync(CacheEntryIdentity scope, CacheProviderCollection definition,
        CacheMaintenanceRequest request, int allowance, CancellationToken token)
    {
        await this.TryCompactShardAsync(scope, this.maintenanceShard, definition, token).ConfigureAwait(false);
        CacheNativeMutation mutation = null;
        await using var transaction = await this.AcquireVerifiedAsync(scope, false, null, token, this.maintenanceShard).ConfigureAwait(false);
        if (transaction is null) { this.AdvanceMaintenanceShard(); return new(0, 0, 0); }

        var kind = this.maintenanceKind switch { 0 => CacheMetadataIndex.Purge, 1 => (byte)56, 2 => (byte)41, 3 => (byte)43, 4 => (byte)47, _ => (byte)55 };
        var range = CacheMetadataIndex.PrefixRange(new byte[] { kind });
        var lower = this.maintenanceRow?.After() ?? range.Lower;
        var limit = Math.Min(16, allowance);
        var rows = new List<(CacheIndexKey Key, bool Deleted, byte[] Bytes)>();
        await foreach (var row in transaction.ScanAsync(lower, limit, token, range.Upper).ConfigureAwait(false))
        {
            if (this.time.GetUtcNow() >= request.NewWorkDeadline) { break; }

            rows.Add((row.Key, row.Deleted, row.Bytes.ToArray()));
        }

        long retired = 0;
        long retiredBytes = 0;
        long deleted = 0;
        long released = 0;
        long releasedMetadata = 0;
        var after = this.maintenanceRow;
        try
        {
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                after = row.Key;
                if (row.Deleted) { continue; }

                if (kind == CacheMetadataIndex.Purge)
                {
                    if (row.Bytes.Length != 48) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    using var current = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(row.Bytes.AsSpan(0, 32)),
                        this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                    current?.Value.Validate(this.header.StoreId);
                    var entry = current?.Value;
                    var matches = entry is not null && entry.Metadata.Version.Value == new Guid(row.Bytes.AsSpan(32, 16), bigEndian: true).ToString("N");
                    if (matches)
                    {
                        if (entry.Metadata.Identity.CollectionIdentity != scope.CollectionIdentity || CacheStoreRoot.Shard(entry.Metadata.Identity) != this.maintenanceShard)
                        {
                            throw new CacheTransferException(CacheErrorCode.Corruption);
                        }

                        if (entry.PurgeAfter > request.RetentionCutoff || entry.Metadata.ExpiresAt > this.time.GetUtcNow()) { continue; }

                        mutation ??= await this.NewMutationAsync(entry.Metadata.Identity, definition, token).ConfigureAwait(false);
                        this.RetireEntry(transaction, entry, definition);
                        retired++;
                        retiredBytes += entry.Metadata.StoredSizeBytes;
                    }
                    else
                    {
                        // A stale index pointer never authorizes retirement of a replacement.
                        mutation ??= await this.NewMutationAsync(MaintenanceIdentity(scope, this.maintenanceShard), definition, token).ConfigureAwait(false);
                    }

                    transaction.Set(row.Key, [], deleted: true);
                }
                else if (kind == 56)
                {
                    if (row.Bytes.Length > 131072) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    var entry = JsonSerializer.Deserialize<CacheNativeRetiredPayload>(row.Bytes) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    if (entry.Format != 1 || entry.Identity.CollectionIdentity != scope.CollectionIdentity || CacheStoreRoot.Shard(entry.Identity) != this.maintenanceShard ||
                        !CacheNativeRetiredPayload.Key(entry.Payload.Version).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    entry.Payload.Validate(this.header.StoreId);
                    using var current = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(entry.Identity),
                        this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                    if (current?.Value.Metadata.Version.Value == entry.Payload.Version) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    if (await this.ReaderRegistry(entry.Identity).IsPinnedAsync(transaction.Authority, "payload", entry.Payload.Version, token).ConfigureAwait(false)) { continue; }

                    var values = Path.Combine(this.root.ShardDirectory(entry.Identity), "values");
                    FileSystemPaths.RejectLinks(values);
                    using var owner = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, values, "stage-" + entry.Payload.Version);
                    if (owner is null) { continue; }

                    foreach (var suffix in new[] { ".payload", ".payload.tmp", ".payload.json", ".payload.json.write.tmp" })
                    {
                        DeleteNativeFile(Path.Combine(values, entry.Payload.Version + suffix));
                    }

                    FileSystemDurability.SyncDirectory(values);
                    mutation ??= await this.NewMutationAsync(entry.Identity, definition, token).ConfigureAwait(false);
                    transaction.Set(row.Key, [], deleted: true);
                    deleted++;
                    released += entry.Payload.Length;
                    releasedMetadata += entry.Payload.PhysicalBytes - entry.Payload.Length + 4096;
                }
                else if (kind == 55)
                {
                    var receipt = JsonSerializer.Deserialize<CacheNativePublicationReceipt>(row.Bytes) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    this.ValidateMaintenanceScope(receipt.Identity, scope);
                    if (receipt.Format != 1 || !CacheNativePublicationReceipt.Key(receipt.Version.Value).Equals(row.Key) ||
                        !Guid.TryParseExact(receipt.OperationId, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    if (receipt.PublishedAt > request.RetentionCutoff || request.RetentionCutoff - receipt.PublishedAt < definition.Policies.Expiration.RetentionGrace) { continue; }

                    using var current = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, CacheMetadataIndex.Primary(receipt.Identity),
                        this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                    current?.Value.Validate(this.header.StoreId, receipt.Identity);
                    if (current?.Value.Metadata.Version == receipt.Version) { continue; }

                    using var retained = await transaction.ReadAsync(CacheNativeRetiredPayload.Key(receipt.Version.Value), token).ConfigureAwait(false);
                    if (retained is not null && !retained.Deleted) { continue; }

                    mutation ??= await this.NewMutationAsync(receipt.Identity, definition, token).ConfigureAwait(false);
                    transaction.Set(row.Key, [], deleted: true);
                }
                else
                {
                    async ValueTask EnsureMutationAsync(CacheEntryIdentity identity) =>
                        mutation ??= await this.NewMutationAsync(identity, definition, token).ConfigureAwait(false);
                    await this.CleanSourceMetadataAsync(transaction, row.Key, row.Bytes, scope, definition, request.RetentionCutoff,
                        EnsureMutationAsync, token).ConfigureAwait(false);
                }
            }

            if (mutation is not null)
            {
                var epoch = await this.ledger.GetEpochAsync(scope.CollectionIdentity, token).ConfigureAwait(false);
                await mutation.CommitAsync(transaction, epoch, new(-retiredBytes, retiredBytes - released,
                    transaction.EstimateCommitGrowth(mutation.Commit, mutation.OperationId) - releasedMetadata, -retired), token,
                    shardEpoch: Math.Max(epoch, transaction.Manifest.Epoch)).ConfigureAwait(false);
            }

            this.maintenanceRow = after;
            if (rows.Count < limit)
            {
                this.maintenanceRow = null;
                if (this.maintenanceKind < 5) { this.maintenanceKind++; }
                else { this.AdvanceMaintenanceShard(); }
            }

            return new(deleted + retired, released + releasedMetadata, rows.Count);
        }
        finally { if (mutation is not null) { await mutation.DisposeAsync().ConfigureAwait(false); } }
    }

    private sealed record NativeMaintenanceBatch(long ReclaimedEntries, long ReclaimedBytes, int Examined);

    private static CacheEntryIdentity MaintenanceIdentity(CacheEntryIdentity scope, int shard)
    {
        for (var index = 0; index < 65536; index++)
        {
            var identity = new CacheEntryIdentity(scope.Namespace, scope.Collection, scope.ContractVersion,
                "maintenance:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (CacheStoreRoot.Shard(identity) == shard) { return identity; }
        }

        throw new CacheTransferException(CacheErrorCode.Capacity);
    }

    private async ValueTask CleanSourceMetadataAsync(CacheShardTransaction transaction, CacheIndexKey key, byte[] bytes, CacheEntryIdentity scope,
        CacheProviderCollection definition, DateTimeOffset cutoff, Func<CacheEntryIdentity, ValueTask> ensureMutation, CancellationToken token)
    {
        if (bytes.Length > 131072 || !ResourceCharge.TryReserve(33L * bytes.Length + 65536, out var memory, this.transient, this.ResidentFor(definition)))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        using (memory)
        {
            if (key.Bytes[0] == 41)
            {
                var value = JsonSerializer.Deserialize<CacheNativeFence>(bytes) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                value.Validate(value.Identity, value.Id);
                this.ValidateMaintenanceScope(value.Identity, scope);
                if (!CacheNativeFence.Key(value.Identity, value.Id).Equals(key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                if (value.Deadline > cutoff) { return; }

                using var logical = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(value.Identity), this.transient,
                    this.ResidentFor(definition), token).ConfigureAwait(false);
                using var exact = await CacheNativeRecords.ReadAsync<CacheNativeEntryState>(transaction, CacheNativeEntryState.Key(value.Identity), this.transient,
                    this.ResidentFor(definition), token).ConfigureAwait(false);
                if (logical is null || exact is null || logical.Value.Captures <= 0 || exact.Value.Captures <= 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                logical.Value.Validate(value.Identity);
                exact.Value.Validate(value.Identity);
                await ensureMutation(value.Identity).ConfigureAwait(false);
                CacheNativeRecords.Set(transaction, CacheNativeKeyState.Key(value.Identity), logical.Value with { Captures = logical.Value.Captures - 1 },
                    this.transient, this.ResidentFor(definition));
                CacheNativeRecords.Set(transaction, CacheNativeEntryState.Key(value.Identity), exact.Value with { Captures = exact.Value.Captures - 1 },
                    this.transient, this.ResidentFor(definition));
                transaction.Set(key, [], deleted: true);
            }
            else if (key.Bytes[0] == 43)
            {
                var value = JsonSerializer.Deserialize<CacheNativeOwnership>(bytes) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                value.Validate(value.Identity, value.OwnerToken);
                this.ValidateMaintenanceScope(value.Identity, scope);
                if (!CacheNativeOwnership.Key(value.Identity, value.OwnerToken).Equals(key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                if (value.Deadline.Add(definition.Policies.Expiration.RetentionGrace) > cutoff) { return; }

                await ensureMutation(value.Identity).ConfigureAwait(false);
                transaction.Set(key, [], deleted: true);
                transaction.Set(FlightKey(value.Identity, value.FlightId), [], deleted: true);
            }
            else
            {
                var value = JsonSerializer.Deserialize<CacheNativeInterest>(bytes) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                this.ValidateMaintenanceScope(value.Identity, scope);
                if (value.Format != 1 || string.IsNullOrEmpty(value.InterestId) || value.InterestId.Length > 128 ||
                    !CacheNativeInterest.Key(value.FlightId, value.InterestId).Equals(key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                if (value.Deadline.Add(definition.Policies.Expiration.RetentionGrace) > cutoff) { return; }

                await ensureMutation(value.Identity).ConfigureAwait(false);
                transaction.Set(key, [], deleted: true);
            }
        }
    }

    private void ValidateMaintenanceScope(CacheEntryIdentity identity, CacheEntryIdentity scope)
    {
        if (!identity.IsValid || identity.CollectionIdentity != scope.CollectionIdentity || CacheStoreRoot.Shard(identity) != this.maintenanceShard)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Reserves a finite native candidate after validating exact captured source authority. Source bytes are consumed once.</summary>
    public ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync<ICacheProviderWriteSession>(async token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = this.Definition(request.Identity);
            if (request.MaximumStoredBytes <= 0 || request.MaximumStoredBytes > CacheSizePolicy.MaximumStoredBytes(definition.Policies) ||
                (request.Condition.CreateOnly && request.Condition.ExpectedVersion.HasValue) ||
                (request.Condition.ExpectedVersion.HasValue && !Guid.TryParseExact(request.Condition.ExpectedVersion.Value.Value, "N", out _)) ||
                (request.OperationId is not null && !Guid.TryParseExact(request.OperationId, "N", out _)))
            {
                throw new CacheTransferException(CacheErrorCode.Validation);
            }

            var admitted = await this.stages.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
            if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            var lifetime = this.TryEnter();
            if (lifetime is null) { admitted.Value.Dispose(); throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            ResourceCharge memory = null;
            CacheNativeMutation mutation = null;
            try
            {
                if (!ResourceCharge.TryReserve(16384, out memory, this.transient, this.ResidentFor(definition))) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                await using (var transaction = await this.AcquireVerifiedAsync(request.Identity, false, null, token).ConfigureAwait(false))
                {
                    if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

                    var fence = await this.VerifyFenceAsync(transaction, request, definition, token).ConfigureAwait(false);
                    await this.CheckCapturedConditionAsync(transaction, request, fence, definition, token).ConfigureAwait(false);
                    var deadline = request.Deadline ?? this.time.GetUtcNow().Add(definition.Policies.Streaming.Lifetime);
                    if (deadline > fence.Deadline) { deadline = fence.Deadline; }

                    request = request with { Deadline = deadline };
                }

                var version = CacheEntryVersion.New();
                mutation = await this.NewMutationAsync(request.Identity, definition, token, version.Value, request.OperationId).ConfigureAwait(false);
                return new CacheFileWriteSession(this, request, definition, version, mutation, memory, admitted.Value, lifetime);
            }
            catch
            {
                try { if (mutation is not null) { await mutation.DisposeAsync().ConfigureAwait(false); } }
                finally { memory?.Dispose(); admitted.Value.Dispose(); lifetime.Dispose(); }

                throw;
            }
        }, cancellationToken);

    private async ValueTask CheckCapturedConditionAsync(CacheShardTransaction transaction, CacheProviderWriteRequest request, CacheNativeFence fence,
        CacheProviderCollection definition, CancellationToken token)
    {
        if (!request.Condition.CreateOnly && !request.Condition.ExpectedVersion.HasValue) { return; }

        using var exact = await CacheNativeRecords.ReadAsync<CacheNativeEntryState>(transaction, CacheNativeEntryState.Key(request.Identity), this.transient,
            this.ResidentFor(definition), token).ConfigureAwait(false);
        if (exact is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        exact.Value.Validate(request.Identity);
        if (exact.Value.PublicationSequence != fence.PublicationSequence) { throw new CacheTransferException(CacheErrorCode.Conflict); }
    }

    private async ValueTask<Stream> OpenStagedAsync(CacheFileWriteSession session, CachePayloadReference payload, CancellationToken token)
    {
        var admitted = await this.readers.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
        if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var lifetime = this.TryEnter();
        if (lifetime is null) { admitted.Value.Dispose(); throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        IAsyncDisposable pin = null;
        try
        {
            await using (var transaction = await this.AcquireVerifiedAsync(session.Request.Identity, false, null, token).ConfigureAwait(false))
            {
                if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                pin = await this.ReaderRegistry(session.Request.Identity).RegisterAsync(transaction.Authority, "payload", [payload.Version], token).ConfigureAwait(false);
            }

            var protection = new StagingReadProtection(pin, admitted.Value, lifetime);
            pin = null;
            return await CachePayloadReader.OpenAsync(Path.Combine(this.root.ShardDirectory(session.Request.Identity), "values"), this.header.StoreId,
                payload, protection, this.transient, this.ResidentFor(session.Definition), token).ConfigureAwait(false);
        }
        catch
        {
            try { if (pin is not null) { await pin.DisposeAsync().ConfigureAwait(false); } }
            finally { admitted.Value.Dispose(); lifetime.Dispose(); }

            throw;
        }
    }

    private sealed class StagingReadProtection(IAsyncDisposable pin, IDisposable admission, IDisposable lifetime) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await pin.DisposeAsync().ConfigureAwait(false); }
            finally { admission.Dispose(); lifetime.Dispose(); }
        }
    }

    private sealed class CacheFileWriteSession : ICacheProviderWriteSession
    {
        private readonly FileSystemCacheStoreProvider owner;
        private readonly ResourceCharge memory;
        private readonly IDisposable admission;
        private readonly IDisposable lifetime;
        private readonly SemaphoreSlim gate = new(1);
        private CachePayloadWriter writer;
        private CachePayloadReference payload;
        private bool failed;
        private bool disposed;
        private bool destinationClosed;
        private bool attempted;

        public CacheFileWriteSession(FileSystemCacheStoreProvider owner, CacheProviderWriteRequest request, CacheProviderCollection definition,
            CacheEntryVersion version, CacheNativeMutation mutation, ResourceCharge memory, IDisposable admission, IDisposable lifetime)
        {
            this.owner = owner;
            this.Request = request;
            this.Definition = definition;
            this.Version = version;
            this.Mutation = mutation;
            this.memory = memory;
            this.admission = admission;
            this.lifetime = lifetime;
            this.Stream = new Destination(this);
        }

        public CacheProviderWriteRequest Request { get; }

        public CacheProviderCollection Definition { get; }

        public CacheNativeMutation Mutation { get; }

        public Stream Stream { get; }

        public string OperationId => this.Mutation.OperationId;

        public CacheEntryVersion Version { get; }

        public long Written => this.writer?.Length ?? 0;

        public async ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.CheckOpen();
                var body = await this.SealAsync(cancellationToken).ConfigureAwait(false);
                return Result<Stream>.Success(await this.owner.OpenStagedAsync(this, body, cancellationToken).ConfigureAwait(false));
            }
            catch (CacheTransferException exception) { return Result<Stream>.Failure(Error(exception.Code)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException) { return Result<Stream>.Failure(Error(CacheErrorCode.ProviderUnavailable)); }
            finally { this.gate.Release(); }
        }

        public async ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.CheckOpen();
                this.attempted = true;
                if (metadata is null) { throw new CacheTransferException(CacheErrorCode.Contract); }

                var body = metadata.IsNotFound && this.writer is null ? null : await this.SealAsync(cancellationToken).ConfigureAwait(false);
                this.owner.Fault?.Invoke("payload-staged");
                return await this.owner.CommitSessionAsync(this, metadata, body, cancellationToken).ConfigureAwait(false);
            }
            catch (CacheTransferException exception) { return Result<CacheWriteOutcome>.Failure(Error(exception.Code)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException) { return Result<CacheWriteOutcome>.Failure(Error(CacheErrorCode.ProviderUnavailable)); }
            finally { this.gate.Release(); }
        }

        private async ValueTask<CachePayloadWriter> WriterAsync(CancellationToken token)
        {
            this.writer ??= await CachePayloadWriter.CreateAsync(Path.Combine(this.owner.root.ShardDirectory(this.Request.Identity), "values"),
                this.owner.header.StoreId, this.Version.Value, this.Request.MaximumStoredBytes, this.owner.profile,
                (bytes, ct) => this.Mutation.ReserveAsync(checked(bytes + 8192), ct), this.owner.transient, this.owner.ResidentFor(this.Definition), token).ConfigureAwait(false);
            return this.writer;
        }

        private async ValueTask<CachePayloadReference> SealAsync(CancellationToken token) => this.payload ??=
            await (await this.WriterAsync(token).ConfigureAwait(false)).SealAsync(token).ConfigureAwait(false);

        private void CheckOpen()
        {
            if (this.disposed || this.failed || this.destinationClosed || this.attempted) { throw new CacheTransferException(CacheErrorCode.Contract); }
        }

        private async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await this.gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                this.CheckOpen();
                if (this.payload is not null) { throw new CacheTransferException(CacheErrorCode.Contract); }

                if (!bytes.IsEmpty) { await (await this.WriterAsync(token).ConfigureAwait(false)).WriteAsync(bytes, token).ConfigureAwait(false); }
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        private async Task FlushAsync(CancellationToken token)
        {
            await this.gate.WaitAsync(token).ConfigureAwait(false);
            try { this.CheckOpen(); if (this.writer is not null) { await this.writer.FlushAsync(token).ConfigureAwait(false); } }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.disposed) { return; }

                this.disposed = true;
                try { if (this.writer is not null) { await this.writer.DisposeAsync().ConfigureAwait(false); } }
                finally
                {
                    try { await this.Mutation.DisposeAsync().ConfigureAwait(false); }
                    finally { this.memory.Dispose(); this.admission.Dispose(); this.lifetime.Dispose(); }
                }
            }
            finally { this.gate.Release(); }
        }

        private sealed class Destination(CacheFileWriteSession session) : Stream
        {
            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => !session.disposed && !session.failed && !session.destinationClosed && !session.attempted && session.payload is null;

            public override long Length => session.Written;

            public override long Position { get => session.Written; set => throw new NotSupportedException(); }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => session.WriteAsync(buffer, cancellationToken);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

            public override Task FlushAsync(CancellationToken cancellationToken) => session.FlushAsync(cancellationToken);

            public override void Flush() => this.FlushAsync(default).GetAwaiter().GetResult();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            protected override void Dispose(bool disposing) { if (disposing) { session.destinationClosed = true; } base.Dispose(disposing); }
        }
    }

    /// <summary>Returns a bounded unique-version metadata page with fixed per-shard publication boundaries and a store-signed continuation.</summary>
    public ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) => this.ExecuteAsync(async token =>
    {
        ArgumentNullException.ThrowIfNull(query);
        var definition = this.ScopeDefinition(query.Namespace, query.Collection);
        var limits = definition.Policies.Limits;
        if (query.PageSize <= 0 || query.PageSize > Math.Min(1000, limits.MaxMetadataPageSize) || query.MaximumCandidates <= 0 ||
            query.MaximumCandidates > Math.Min(100000, limits.MaxQueryCandidates)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        var admitted = await this.metadataQueries.AcquireAsync(this.profile.Native.AttemptTimeout, token).ConfigureAwait(false);
        if (admitted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (admitted.Value)
        {
            if (!ResourceCharge.TryReserve(2 * CacheDefaults.MiB, out var scratch, this.transient, this.ResidentFor(definition)))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            using (scratch)
            {
                var started = this.time.GetTimestamp();
                var scope = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                var schemas = await this.ledger.ValidateSelectionAsync(scope, query.Selection, token).ConfigureAwait(false);
                var evaluator = new CacheSelectionEvaluator(query.Selection, schemas, query.Cutoff);
                var fingerprint = SelectionHash(query.Selection, token);
                CacheFileQueryCursor cursor;
                if (query.Continuation is null)
                {
                    var boundary = await this.ledger.CaptureBoundaryAsync(scope, token).ConfigureAwait(false);
                    cursor = new(1, this.header.StoreId, this.header.RecoveryGeneration, scope.CollectionIdentity, fingerprint, query.Cutoff.UtcTicks, "query",
                        0, null, boundary.Epoch, boundary.MaximumSequence, 0, 0, query.MaximumCandidates, false);
                }
                else { cursor = this.DecodeCursor(query.Continuation, scope, fingerprint, query.Cutoff, "query"); }

                var page = await this.ReadMetadataPageAsync(scope, definition, query, evaluator, cursor, started, token).ConfigureAwait(false);
                return page.Page with { Continuation = page.Page.Completed ? null : this.EncodeCursor(page.Cursor) };
            }
        }
    }, cancellationToken);

    private CacheProviderCollection ScopeDefinition(string @namespace, string collection)
    {
        var space = CacheIdentityNormalizer.Namespace(@namespace);
        var name = CacheIdentityNormalizer.Name(collection);
        return this.definitions.Values.FirstOrDefault(d => d.Namespace == space && d.Collection == name && d.Policies.Enabled) ??
            throw new CacheTransferException(CacheErrorCode.Contract);
    }

    private async ValueTask<NativeQueryPage> ReadMetadataPageAsync(CacheEntryIdentity scope, CacheProviderCollection definition, CacheMetadataQuery query,
        CacheSelectionEvaluator evaluator, CacheFileQueryCursor initial, long started, CancellationToken token,
        CacheFileSelectionPlan selectedPlan = null, Func<CacheEntryMetadata, bool> eligibility = null)
    {
        var limits = definition.Policies.Limits;
        var plan = selectedPlan ?? CacheFileSelectionPlan.Build(scope, query.Selection);
        var cursor = initial;
        var output = new List<CacheEntryMetadata>();
        var retained = new List<IDisposable>();
        var examined = 0;
        long outputBytes = 0;
        var allowance = Math.Min(query.MaximumCandidates, initial.MaximumCandidates - initial.Examined);
        string reason = null;
        try
        {
            if (query.Selection.IsEmpty) { cursor = cursor with { Shard = 256, LastKey = null }; }

            while (cursor.Shard < 256)
            {
                token.ThrowIfCancellationRequested();
                if (this.time.GetElapsedTime(started) >= limits.MaxQueryWork) { reason = "WorkLimit"; break; }

                if (examined >= allowance) { reason = "CandidateLimit"; break; }

                if (output.Count >= query.PageSize) { reason = "PageSize"; break; }

                if (cursor.MaximumSequence[cursor.Shard] == 0)
                {
                    cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null };
                    continue;
                }

                await using var transaction = await this.AcquireVerifiedAsync(scope, false, null, token, cursor.Shard).ConfigureAwait(false);
                if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                cursor = cursor with { Changed = cursor.Changed || transaction.Manifest.Sequence != cursor.MaximumSequence[cursor.Shard] };
                var lower = cursor.LastKey is null ? plan.Range.Lower : new CacheIndexKey(Convert.FromBase64String(cursor.LastKey)).After();
                if (lower is null || (plan.Range.Upper is not null && lower.CompareTo(plan.Range.Upper) >= 0))
                {
                    cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null };
                    continue;
                }

                var ended = true;
                await foreach (var candidate in transaction.ScanAsync(lower, allowance - examined, token, plan.Range.Upper).ConfigureAwait(false))
                {
                    if (this.time.GetElapsedTime(started) >= limits.MaxQueryWork) { reason = "WorkLimit"; ended = false; break; }

                    if (output.Count >= query.PageSize) { reason = "PageSize"; ended = false; break; }

                    var consumed = true;
                    CacheMetadataDocument<CacheNativeEntry> document = null;
                    try
                    {
                        if (!candidate.Deleted)
                        {
                            var primary = plan.Primary ? candidate.Key : candidate.Bytes.Length == 48 ? CacheMetadataIndex.Primary(candidate.Bytes.Span[..32]) :
                                throw new CacheTransferException(CacheErrorCode.Corruption);
                            document = await CacheNativeRecords.ReadAsync<CacheNativeEntry>(transaction, primary, this.transient, this.ResidentFor(definition), token).ConfigureAwait(false);
                            if (document is not null)
                            {
                                var entry = document.Value;
                                entry.Validate(this.header.StoreId);
                                if (!CacheMetadataIndex.Primary(entry.Metadata.Identity).Equals(primary) || entry.Metadata.Identity.CollectionIdentity != scope.CollectionIdentity ||
                                    CacheStoreRoot.Shard(entry.Metadata.Identity) != cursor.Shard) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                                var versionMatches = plan.Primary || entry.Metadata.Version.Value == new Guid(candidate.Bytes.Span.Slice(32, 16), bigEndian: true).ToString("N");
                                if (versionMatches && entry.PublicationSequence <= cursor.MaximumSequence[cursor.Shard] && evaluator.Matches(entry.Metadata) &&
                                    (eligibility is null || eligibility(entry.Metadata)))
                                {
                                    using var key = await CacheNativeRecords.ReadAsync<CacheNativeKeyState>(transaction, CacheNativeKeyState.Key(entry.Metadata.Identity), this.transient,
                                        this.ResidentFor(definition), token).ConfigureAwait(false);
                                    key?.Value.Validate(entry.Metadata.Identity);
                                    if (entry.PublicationSequence > (key?.Value.ReadFenceSequence ?? 0))
                                    {
                                        using var buffer = new CacheMetadataBuffer(65536);
                                        JsonSerializer.Serialize(buffer, entry.Metadata);
                                        if (buffer.Length > Math.Min(4 * CacheDefaults.MiB, limits.MaxMetadataPageBytes) - outputBytes)
                                        {
                                            if (output.Count == 0) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                                            consumed = false;
                                            ended = false;
                                            reason = "PageBytes";
                                        }
                                        else
                                        {
                                            output.Add(entry.Metadata);
                                            outputBytes += buffer.Length;
                                            retained.Add(document);
                                            document = null;
                                            cursor = cursor with { Observed = cursor.Observed + 1 };
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally { document?.Dispose(); }

                    if (!consumed) { break; }

                    examined++;
                    cursor = cursor with { LastKey = Convert.ToBase64String(candidate.Key.Bytes) };
                    if (examined >= allowance) { reason = "CandidateLimit"; ended = false; break; }
                }

                if (!ended) { break; }

                cursor = cursor with { Shard = cursor.Shard + 1, LastKey = null };
            }

            var epoch = await this.ledger.GetEpochAsync(scope.CollectionIdentity, token).ConfigureAwait(false);
            cursor = cursor with { Examined = cursor.Examined + examined, Changed = cursor.Changed || epoch != cursor.Epoch };
            var completed = cursor.Shard == 256;
            return new(new(Array.AsReadOnly(output.ToArray()), null, cursor.Observed, completed && !cursor.Changed, examined, completed, reason), cursor);
        }
        finally { foreach (var memory in retained) { memory.Dispose(); } }
    }

    private sealed record NativeQueryPage(CacheMetadataPage Page, CacheFileQueryCursor Cursor);

    /// <summary>Creates or conditionally transitions bounded provider-owned status with one renewable execution claim.</summary>
    /// <example><code>var changed = await provider.SaveOperationAsync(operation, previous.Revision, cancellationToken);</code></example>
    public ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(async token =>
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(operation.Request);
            var definition = this.ScopeDefinition(operation.Request.Namespace, operation.Request.Collection);
            var scope = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
            var selection = operation.Request.Clear ? CacheSelection.Pattern("*") : operation.Request.Selection;
            await this.ledger.ValidateSelectionAsync(scope, selection, token).ConfigureAwait(false);
            var fingerprint = SelectionHash(selection, token);
            return await this.ledger.SaveOperationAsync(operation, fingerprint, this.node, expectedRevision, this.diagnostics.MaximumOperationRecords,
                this.diagnostics.OperationRetention, token).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Reads an exact retained operation only within this provider's declared logical collection scopes.</summary>
    /// <example><code>var status = await provider.GetOperationAsync(operationId, cancellationToken);</code></example>
    public ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) => this.ExecuteAsync(async token =>
    {
        using var document = await this.ledger.GetOperationAsync(operationId, token).ConfigureAwait(false);
        var value = document.Value.Value;
        if (!this.AllowedOperationScope(value.Request.Namespace, value.Request.Collection)) { throw new CacheTransferException(CacheErrorCode.NotFound); }

        return value;
    }, cancellationToken);

    /// <summary>Lists a bounded four-MiB status page without opening cache payloads or exposing another namespace's operations.</summary>
    /// <example><code>var recent = await provider.GetOperationsAsync(20, cancellationToken);</code></example>
    public ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync<IReadOnlyList<CacheProviderOperation>>(async token =>
        {
            if (limit is <= 0 or > 1000) { throw new CacheTransferException(CacheErrorCode.Validation); }

            var scopes = this.definitions.Values.Where(d => d.Policies.Enabled)
                .Select(d => new CacheEntryIdentity(d.Namespace, d.Collection, d.ContractVersion, "catalog").CollectionIdentity).ToHashSet(StringComparer.Ordinal);
            using var page = await this.ledger.GetOperationsAsync(scopes, limit, token).ConfigureAwait(false);
            return Array.AsReadOnly(page.Values.Select(r => r.Value).ToArray());
        }, cancellationToken);

    private bool AllowedOperationScope(string @namespace, string collection) => this.definitions.Values.Any(d =>
        d.Policies.Enabled && d.Namespace == @namespace && d.Collection == collection);
}