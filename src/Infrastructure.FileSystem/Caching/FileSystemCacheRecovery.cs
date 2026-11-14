// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;
using System.Text.Json;
using System.Security.Cryptography;

/// <summary>Performs explicit offline recovery and disposable-cache reset under an operator-established maintenance window.</summary>
/// <param name="options">The same dedicated root and native access/durability profile used by every participant.</param>
/// <remarks>Stop or independently fence all readers, writers, sweepers and recovery processes on every host, and drain server-side replays first. This API cannot establish that external proof and never infers it from a timeout.</remarks>
/// <example><code>var recovery = new FileSystemCacheRecovery(options); var result = await recovery.RecoverQuiescentAsync(storeId, allParticipantsStopped: true, cancellationToken);</code></example>
public sealed class FileSystemCacheRecovery(CacheProviderOptions options)
{
    private readonly FileSystemCacheOptions profile = new(options);

    /// <summary>Reads only the checked root identity/profile without opening payloads, replaying journals, or creating a store.</summary>
    /// <param name="cancellationToken">Cancels the bounded header read.</param>
    /// <returns>The identity to inspect before stopping or independently fencing every participant.</returns>
    /// <example><code>var state = await recovery.GetStateAsync(token); var inspectedStoreId = state.Value.StoreId;</code></example>
    public async ValueTask<Result<CacheFileSystemRecoveryState>> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var transient = new ResourceBudget("cache-recovery-inspect", 4 * CacheDefaults.MiB);
        var root = new CacheStoreRoot(this.profile, transient, transient);
        try
        {
            var path = root.Resolve("cache-root.json");
            if (!File.Exists(path)) { return Result<CacheFileSystemRecoveryState>.Failure(new CacheError(CacheErrorCode.NotFound, "The cache root has not been initialized.")); }

            using var document = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(path, transient, transient, cancellationToken).ConfigureAwait(false);
            var value = document.Value;
            value.Validate(this.profile);
            return new CacheFileSystemRecoveryState(value.StoreId, value.RecoveryGeneration, value.RecoveryInProgress, value.AccessKind, value.Durability);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CacheTransferException exception) { return Result<CacheFileSystemRecoveryState>.Failure(new CacheError(exception.Code, "The checked cache root identity is unavailable.")); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            return Result<CacheFileSystemRecoveryState>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The checked cache root identity is unavailable."));
        }
    }

    /// <summary>Discards all cache data and control metadata while preserving a durable root fence until a new empty store identity is ready.</summary>
    /// <param name="expectedStoreId">The exact identity independently inspected before the maintenance window.</param>
    /// <param name="allParticipantsStopped">Explicit proof that all application, native and recovery work has actually stopped or been independently fenced.</param>
    /// <param name="cancellationToken">Cancels between bounded filesystem steps; the persisted fence remains after partial cleanup.</param>
    /// <returns>The new empty store identity and explicitly discarded scope.</returns>
    /// <example><code>var reset = await recovery.ResetQuiescentAsync(inspectedStoreId, true, cancellationToken);</code></example>
    public ValueTask<Result<CacheFileSystemRecoveryOutcome>> ResetQuiescentAsync(string expectedStoreId, bool allParticipantsStopped,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(expectedStoreId, allParticipantsStopped, true, cancellationToken);

    /// <summary>Verifies committed metadata, resolves exact prepared publications, and reopens only provably consistent data under a new durable generation.</summary>
    /// <param name="expectedStoreId">The exact identity independently inspected before the maintenance window.</param>
    /// <param name="allParticipantsStopped">Explicit proof covering every reader, writer, sweeper and recovery process on every host.</param>
    /// <param name="cancellationToken">Cancels between recovery units without reopening a partially verified root.</param>
    /// <returns>Verified scope or a still-fenced outcome requiring explicit reset; uncertain older content is never adopted.</returns>
    /// <example><code>var recovered = await recovery.RecoverQuiescentAsync(inspectedStoreId, true, cancellationToken);</code></example>
    public ValueTask<Result<CacheFileSystemRecoveryOutcome>> RecoverQuiescentAsync(string expectedStoreId, bool allParticipantsStopped,
        CancellationToken cancellationToken = default) => this.ExecuteAsync(expectedStoreId, allParticipantsStopped, false, cancellationToken);

    private async ValueTask<Result<CacheFileSystemRecoveryOutcome>> ExecuteAsync(string expectedStoreId, bool stopped, bool reset, CancellationToken token)
    {
        if (!stopped || !Guid.TryParseExact(expectedStoreId, "N", out _) || !Directory.Exists(this.profile.RootPath) ||
            Path.TrimEndingDirectorySeparator(Path.GetPathRoot(this.profile.RootPath)) == this.profile.RootPath)
        {
            return Result<CacheFileSystemRecoveryOutcome>.Failure(new CacheError(CacheErrorCode.Validation, "Offline cache recovery requires an inspected dedicated root and explicit global quiescence."));
        }

        var transient = new ResourceBudget("cache-recovery-transient", 64 * CacheDefaults.MiB);
        var resident = new ResourceBudget("cache-recovery-resident", 64 * CacheDefaults.MiB);
        using var blocks = new CacheIndexBlockCache(4 * CacheDefaults.MiB);
        var root = new CacheStoreRoot(this.profile, transient, resident);
        try
        {
            using var initial = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(root.Resolve("cache-root.json"), transient, resident, token).ConfigureAwait(false);
            initial.Value.Validate(this.profile);
            if (initial.Value.StoreId != expectedStoreId) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            // A retained shared recovery marker is cleared only by this explicit, externally proven stopped-participant procedure.
            if (this.profile.Shared && initial.Value.RecoveryInProgress) { DeleteChecked(root, root.Resolve("recovery")); }

            using var authority = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, root.Path, "recovery");
            if (authority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            var header = initial.Value.RecoveryInProgress ? initial.Value : initial.Value with { RecoveryInProgress = true,
                RecoveryBaseGeneration = initial.Value.RecoveryGeneration, RecoveryGeneration = checked(initial.Value.RecoveryGeneration + 1) };
            await CacheCheckedMetadata.WriteAsync(root.Path, "cache-root.json", header, authority, true, transient, resident, token).ConfigureAwait(false);
            // The generation above is durable before any orphan marker or uncertain data can be removed.
            if (reset)
            {
                var discarded = this.CountShards(root, token);
                foreach (var component in new[] { "collections", "ledger" })
                {
                    var directory = root.Resolve(component);
                    if (Directory.Exists(directory)) { DeleteTree(root, directory, token); }
                }

                DeleteChecked(root, root.Resolve("authority"));
                var empty = new CacheStoreHeader { StoreId = Guid.NewGuid().ToString("N"), AccessKind = header.AccessKind, Durability = header.Durability,
                    MaximumBytes = header.MaximumBytes, MaximumEntries = header.MaximumEntries, RecoveryGeneration = header.RecoveryGeneration };
                await CacheCheckedMetadata.WriteAsync(root.Path, "cache-root.json", empty, authority, true, transient, resident, token).ConfigureAwait(false);
                return new CacheFileSystemRecoveryOutcome(empty.StoreId, empty.RecoveryGeneration, true, 0, discarded, false);
            }

            return await this.RecoverAsync(root, header, authority, transient, resident, blocks, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (CacheTransferException exception) { return Result<CacheFileSystemRecoveryOutcome>.Failure(new CacheError(exception.Code, "The offline cache recovery boundary could not complete.")); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            return Result<CacheFileSystemRecoveryOutcome>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The offline cache root remains unavailable; inspect its durable recovery fence."));
        }
    }

    private int CountShards(CacheStoreRoot root, CancellationToken token)
    {
        var collections = root.Resolve("collections");
        var count = 0;
        if (!Directory.Exists(collections)) { return 0; }

        foreach (var collection in Directory.EnumerateDirectories(collections))
        {
            VerifyInside(root, collection);
            var shards = Path.Combine(collection, "shards");
            FileSystemPaths.RejectLinks(shards);
            if (!Directory.Exists(shards)) { continue; }

            foreach (var shard in Directory.EnumerateDirectories(shards)) { token.ThrowIfCancellationRequested(); VerifyInside(root, shard); count = checked(count + 1); }
        }

        return count;
    }

    private static void VerifyInside(CacheStoreRoot root, string path)
    {
        var absolute = Path.GetFullPath(path);
        var boundary = Path.TrimEndingDirectorySeparator(root.Path) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(boundary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        FileSystemPaths.RejectLinks(absolute);
    }

    private static void DeleteChecked(CacheStoreRoot root, string path)
    {
        VerifyInside(root, path);
        File.Delete(path);
    }

    private static void DeleteTree(CacheStoreRoot root, string path, CancellationToken token)
    {
        VerifyInside(root, path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            token.ThrowIfCancellationRequested();
            VerifyInside(root, entry);
            if (Directory.Exists(entry)) { DeleteTree(root, entry, token); }
            else { File.Delete(entry); }
        }

        Directory.Delete(path);
        FileSystemDurability.SyncDirectory(Path.GetDirectoryName(path));
    }

    private async ValueTask VerifyCurrentPayloadsAsync(CacheStoreRoot root, CacheEntryIdentity scope, int shard, CacheStoreHeader header,
        ResourceBudget transient, ResourceBudget resident, CacheIndexBlockCache blocks, CancellationToken token)
    {
        var store = new CacheShardStore(root, root.ShardDirectory(scope, shard), header.StoreId, scope.CollectionIdentity, shard,
            header.RecoveryGeneration, transient, resident, blocks);
        await using var transaction = await store.AcquireAsync(false, null, null, token).ConfigureAwait(false);
        if (transaction is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var range = CacheMetadataIndex.PrefixRange(new byte[] { CacheMetadataIndex.Entry });
        var lower = range.Lower;
        // Recovery may walk the committed metadata offline, but each scanner/page remains bounded.
        while (lower is not null && (range.Upper is null || lower.CompareTo(range.Upper) < 0))
        {
            var count = 0;
            CacheIndexKey last = null;
            await foreach (var row in transaction.ScanAsync(lower, 128, token, range.Upper).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                count++;
                last = row.Key;
                if (row.Deleted) { continue; }

                if (row.Bytes.Length > 131072 || !ResourceCharge.TryReserve(33L * row.Bytes.Length + 65536, out var memory, transient, resident))
                {
                    throw new CacheTransferException(CacheErrorCode.Capacity);
                }

                using (memory)
                {
                    var entry = JsonSerializer.Deserialize<CacheNativeEntry>(row.Bytes.Span) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    entry.Validate(header.StoreId);
                    if (entry.Metadata.Identity.CollectionIdentity != scope.CollectionIdentity || CacheStoreRoot.Shard(entry.Metadata.Identity) != shard ||
                        !CacheMetadataIndex.Primary(entry.Metadata.Identity).Equals(row.Key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    if (entry.Payload is not null)
                    {
                        using var descriptor = await CacheCheckedMetadata.ReadAsync<CachePayloadReference>(
                            Path.Combine(store.DirectoryPath, "values", entry.Payload.Version + ".payload.json"), transient, resident, token).ConfigureAwait(false);
                        if (descriptor.Value != entry.Payload) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        if (!ResourceCharge.TryReserve(65536, out var copying, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                        using (copying)
                        await using (var payload = await CachePayloadReader.OpenAsync(Path.Combine(store.DirectoryPath, "values"), header.StoreId,
                            entry.Payload, new RecoveryPin(), transient, resident, token).ConfigureAwait(false))
                        {
                            var buffer = new byte[65536];
                            while (await payload.ReadAsync(buffer, token).ConfigureAwait(false) != 0) { }
                        }
                    }
                }
            }

            if (count < 128) { break; }

            lower = last?.After();
        }
    }

    private async ValueTask ReconcileStoppedCreditsAsync(CacheStoreRoot root, CacheRootQuotaLedger ledger, CacheStoreHeader header,
        ResourceBudget transient, ResourceBudget resident, CancellationToken token)
    {
        CacheIndexKey after = null;
        do
        {
            using var page = await ledger.GetCreditsAsync(after, 16, token).ConfigureAwait(false);
            foreach (var credit in page.Credits)
            {
                token.ThrowIfCancellationRequested();
                if (credit.State == CacheSpaceCreditState.Prepared) { throw new CacheTransferException(CacheErrorCode.Indeterminate); }

                if (credit.Kind == CacheSpaceCreditKind.Reader && credit.State == CacheSpaceCreditState.Reserved)
                {
                    var pins = Path.Combine(root.ShardDirectory(credit.Identity), "pins");
                    foreach (var suffix in new[] { ".pin", ".pin.write.tmp" }) { DeleteChecked(root, Path.Combine(pins, credit.Id + suffix)); }

                    DeleteChecked(root, Path.Combine(pins, "stage-reader-" + credit.Id));
                    if (Directory.Exists(pins)) { FileSystemDurability.SyncDirectory(pins); }

                    await ledger.ReleaseReaderAsync(credit.Id, credit.Identity, token).ConfigureAwait(false);
                }
                else if (credit.State != CacheSpaceCreditState.Released && !credit.NativeEnded)
                {
                    // The explicit maintenance-window proof covers actual termination, not a lease expiry.
                    await ledger.MarkNativeEndedAsync(credit.Id, credit.NodeId, token).ConfigureAwait(false);
                }
            }

            after = page.Continuation;
        }
        while (after is not null);
    }

    private sealed class RecoveryPin : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private async ValueTask<Result<CacheFileSystemRecoveryOutcome>> RecoverAsync(CacheStoreRoot root, CacheStoreHeader header,
        FileSystemAuthority authority, ResourceBudget transient, ResourceBudget resident, CacheIndexBlockCache blocks, CancellationToken token)
    {
        var verified = 0;
        try
        {
            var ledgerDirectory = root.Resolve("ledger");
            if (!Directory.Exists(ledgerDirectory))
            {
                if (header.LedgerInitialized || Directory.Exists(root.Resolve("collections"))) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                var empty = header with { RecoveryInProgress = false, RecoveryBaseGeneration = null };
                await CacheCheckedMetadata.WriteAsync(root.Path, "cache-root.json", empty, authority, true, transient, resident, token).ConfigureAwait(false);
                return new CacheFileSystemRecoveryOutcome(header.StoreId, header.RecoveryGeneration, true, 0, 0, false);
            }

            await this.VerifyAndStampAsync(root, ledgerDirectory, "root-ledger", 0, header, transient, resident, token).ConfigureAwait(false);
            using var current = await CacheCheckedMetadata.ReadAsync<CacheStoreHeader>(root.Resolve("cache-root.json"), transient, resident, token).ConfigureAwait(false);
            var ledger = new CacheRootQuotaLedger(root, header, current.Bytes + 4096, transient, resident, blocks, TimeProvider.System, header);
            _ = await ledger.GetQuotaAsync(token).ConfigureAwait(false); // Resolves a checked predeclared root intent before data recovery.
            var scopes = await ledger.GetRecoveryScopesAsync(token).ConfigureAwait(false);
            foreach (var scope in scopes)
            {
                token.ThrowIfCancellationRequested();
                // New and already captured source work cannot cross this durable collection boundary.
                await ledger.AdvanceEpochAsync(scope.CollectionIdentity, token).ConfigureAwait(false);
                for (var shard = 0; shard < 256; shard++)
                {
                    token.ThrowIfCancellationRequested();
                    var directory = root.ShardDirectory(scope, shard);
                    var receipt = await ledger.GetReceiptAsync(scope, shard, token).ConfigureAwait(false);
                    if (!Directory.Exists(directory))
                    {
                        if (receipt is not null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                        continue;
                    }

                    var manifest = await this.VerifyAndStampAsync(root, directory, scope.CollectionIdentity, shard, header, transient, resident, token).ConfigureAwait(false);
                    if (receipt is null)
                    {
                        if (manifest.Sequence != 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }
                    }
                    else
                    {
                        if (receipt.State == CacheShardReceiptState.Prepared && receipt.Generation == manifest.Generation && receipt.Sequence == manifest.Sequence &&
                            receipt.CreditId == manifest.OperationId)
                        {
                            var credit = await ledger.GetCreditAsync(receipt.CreditId, token).ConfigureAwait(false);
                            await ledger.ConfirmPreparedAsync(credit.Id, credit.Identity, credit.Version, manifest.Generation, token).ConfigureAwait(false);
                            receipt = receipt with { State = CacheShardReceiptState.Committed };
                        }

                        receipt.Verify(manifest); // Never accept an older internally valid Buffered generation.
                    }

                    await this.VerifyCurrentPayloadsAsync(root, scope, shard, header, transient, resident, blocks, token).ConfigureAwait(false);
                    verified++;
                }
            }

            await this.ReconcileStoppedCreditsAsync(root, ledger, header, transient, resident, token).ConfigureAwait(false);
            await ledger.RebindRecoveryOperationsAsync(header, await ledger.GetCursorKeyAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            DeleteChecked(root, root.Resolve("authority"));
            var ready = header with { RecoveryInProgress = false, RecoveryBaseGeneration = null };
            await CacheCheckedMetadata.WriteAsync(root.Path, "cache-root.json", ready, authority, true, transient, resident, token).ConfigureAwait(false);
            return new CacheFileSystemRecoveryOutcome(header.StoreId, header.RecoveryGeneration, true, verified, 0, false);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException ||
            exception is CacheTransferException { Code: CacheErrorCode.Corruption or CacheErrorCode.ProviderUnavailable or CacheErrorCode.Indeterminate })
        {
            return new CacheFileSystemRecoveryOutcome(header.StoreId, header.RecoveryGeneration, false, verified, 0, true,
                "Committed metadata or its durable publication boundary is uncertain. The root remains fenced; explicitly reset the disposable store before rewarming.");
        }
    }

    private async ValueTask<CacheManifest> VerifyAndStampAsync(CacheStoreRoot root, string directory, string collection, int shard,
        CacheStoreHeader header, ResourceBudget transient, ResourceBudget resident, CancellationToken token)
    {
        VerifyInside(root, directory);
        if (this.profile.Shared) { DeleteChecked(root, Path.Combine(directory, "authority")); }

        using var authority = FileSystemAuthority.TryAcquire(this.profile.AuthorityOptions, directory, "authority");
        if (authority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        using var metadata = await CacheCheckedMetadata.ReadAsync<RecoveryShardHeader>(Path.Combine(directory, "header.json"), transient, resident, token).ConfigureAwait(false);
        var shardHeader = metadata.Value;
        if (shardHeader != new RecoveryShardHeader(1, header.StoreId, collection, shard, shardHeader.RecoveryGeneration, true) ||
            (shardHeader.RecoveryGeneration != header.RecoveryBaseGeneration && shardHeader.RecoveryGeneration != header.RecoveryGeneration))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        using var document = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(directory, "manifest.json"), transient, resident, token).ConfigureAwait(false);
        var manifest = document.Value;
        manifest.Validate(header.StoreId, collection, shard);
        if (manifest.RecoveryGeneration != header.RecoveryBaseGeneration && manifest.RecoveryGeneration != header.RecoveryGeneration)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        foreach (var segment in manifest.Segments)
        {
            await VerifyFileAsync(root, Path.Combine(directory, segment.Id + ".idx"), segment.IndexBytes, segment.IndexHash, transient, resident, token).ConfigureAwait(false);
            await VerifyFileAsync(root, Path.Combine(directory, segment.Id + ".records"), segment.RecordsBytes, segment.RecordsHash, transient, resident, token).ConfigureAwait(false);
        }

        var journalPath = Path.Combine(directory, manifest.Journal + ".journal");
        VerifyInside(root, journalPath);
        await using (var journal = FileSystemFileOperations.Open(journalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            var sequence = manifest.CheckpointSequence;
            RecoveryJournalRecord last = null;
            await foreach (var frame in CacheJournal.ReadCommittedAsync(journal, manifest.JournalBytes, checked(manifest.CheckpointSequence + 1), transient, resident, token).ConfigureAwait(false))
            {
                var record = JsonSerializer.Deserialize<RecoveryJournalRecord>(frame.Payload.Span) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                if (record.Format != 1 || record.StoreId != header.StoreId || !Guid.TryParseExact(record.PreviousGeneration, "N", out _) ||
                    !Guid.TryParseExact(record.Generation, "N", out _) || !Guid.TryParseExact(record.OperationId, "N", out _) || record.Segment is null ||
                    !record.Segment.Valid(frame.Sequence) || record.Segment.Sequence != frame.Sequence ||
                    (last is not null && record.PreviousGeneration != last.Generation)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                last = record;
                sequence = frame.Sequence;
            }

            if (sequence != manifest.Sequence || (last is not null && (last.Generation != manifest.Generation || last.OperationId != manifest.OperationId)))
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            if (journal.Length != manifest.JournalBytes) { journal.SetLength(manifest.JournalBytes); FileSystemDurability.Flush(journal); }
        }

        manifest = manifest with { RecoveryGeneration = header.RecoveryGeneration };
        await CacheCheckedMetadata.WriteAsync(directory, "manifest.json", manifest, authority, true, transient, resident, token).ConfigureAwait(false);
        await CacheCheckedMetadata.WriteAsync(directory, "header.json", shardHeader with { RecoveryGeneration = header.RecoveryGeneration }, authority, true, transient, resident, token).ConfigureAwait(false);
        this.ReleaseStoppedMarkers(root, directory, "stages", token);
        this.ReleaseStoppedMarkers(root, directory, "values", token);
        return manifest;
    }

    private static async ValueTask VerifyFileAsync(CacheStoreRoot root, string path, long length, string expectedHash,
        ResourceBudget transient, ResourceBudget resident, CancellationToken token)
    {
        VerifyInside(root, path);
        if (length < 0 || length > root.Profile.Native.MaxStorageBytes || !ResourceCharge.TryReserve(65536, out var memory, transient, resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        using (memory)
        await using (var file = FileSystemFileOperations.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (file.Length != length) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            int read;
            while ((read = await file.ReadAsync(buffer, token).ConfigureAwait(false)) != 0) { hash.AppendData(buffer.AsSpan(0, read)); }

            if (Convert.ToHexStringLower(hash.GetHashAndReset()) != expectedHash) { throw new CacheTransferException(CacheErrorCode.Corruption); }
        }
    }

    private void ReleaseStoppedMarkers(CacheStoreRoot root, string shard, string component, CancellationToken token)
    {
        var directory = Path.Combine(shard, component);
        VerifyInside(root, directory);
        if (!Directory.Exists(directory)) { return; }

        foreach (var path in Directory.EnumerateFiles(directory, "stage-*")) { token.ThrowIfCancellationRequested(); DeleteChecked(root, path); }

        foreach (var path in Directory.EnumerateFiles(directory, "owner-*.tmp")) { token.ThrowIfCancellationRequested(); DeleteChecked(root, path); }
    }

    private sealed record RecoveryShardHeader(int Format, string StoreId, string CollectionIdentity, int Shard, long RecoveryGeneration, bool Initialized);

    private sealed record RecoveryJournalRecord(int Format, string StoreId, string PreviousGeneration, string Generation, string OperationId, CacheSegmentReference Segment);
}