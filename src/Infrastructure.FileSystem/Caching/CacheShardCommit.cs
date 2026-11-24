// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Runtime.CompilerServices;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one shard&apos;s native authority, checked manifest snapshot and bounded staged index mutations until commit or disposal.</summary>
/// <example><code>await using var transaction = await shard.AcquireAsync(false, null, null, token); using var value = await transaction.ReadAsync(key, token);</code></example>
public sealed class CacheShardTransaction(CacheShardStore store, FileSystemAuthority authority, CacheMetadataDocument<CacheManifest> snapshot,
    ResourceBudget transient, ResourceBudget resident, CacheIndexBlockCache blocks) : IAsyncDisposable
{
    private readonly Dictionary<string, CacheIndexReader> readers = new(StringComparer.Ordinal);
    private readonly SortedDictionary<CacheIndexKey, Mutation> mutations = [];
    private bool attempted;
    private bool disposed;
    private string confirmedGeneration;

    /// <summary>Gets the checked authoritative manifest snapshot currently held by this transaction.</summary>
    public CacheManifest Manifest => snapshot.Value;

    /// <summary>Gets the native shard authority held until this transaction is disposed.</summary>
    public FileSystemAuthority Authority => authority;

    /// <summary>Gets the next checked publication sequence after the manifest snapshot.</summary>
    public long NextSequence => checked(this.Manifest.Sequence + 1);

    /// <summary>Gets the number of distinct index keys currently staged for publication.</summary>
    public int MutationCount => this.mutations.Count;

    /// <summary>Gets framed manifest, committed journal and immutable segment bytes referenced by this snapshot.</summary>
    public long ReferencedBytes => checked(snapshot.Bytes + this.Manifest.JournalBytes + this.Manifest.Segments.Sum(s => s.IndexBytes + s.RecordsBytes));

    /// <summary>Computes conservative additional index, record, journal and manifest storage for the staged mutation before publication.</summary>
    public long EstimateCommitGrowth(CacheShardCommitIdentity identity, string operationId)
    {
        this.CheckActive();
        var count = this.mutations.Count;
        var indexBytes = CacheSortedSegment.HeaderBytes + (count / CacheSortedSegment.EntriesPerBlock +
            (count % CacheSortedSegment.EntriesPerBlock == 0 ? 0L : 1L)) * CacheSortedSegment.BlockBytes;
        var recordBytes = this.mutations.Values.Sum(m => (long)m.Row.Value.Length);
        var placeholder = new string('0', 64);
        var segment = new CacheSegmentReference(identity.SegmentId, this.NextSequence, indexBytes, recordBytes, placeholder, placeholder);
        using var frame = new CacheMetadataBuffer();
        JsonSerializer.Serialize(frame, new CacheShardJournalRecord(1, this.Manifest.StoreId, this.Manifest.Generation, identity.Generation, operationId, segment));
        var journalBytes = CacheJournal.HeaderBytes + CacheJournal.TrailerBytes + frame.Length;
        using var metadata = new CacheMetadataBuffer();
        JsonSerializer.Serialize(metadata, this.Manifest with { Generation = identity.Generation, Sequence = this.NextSequence,
            JournalBytes = this.Manifest.JournalBytes + journalBytes, Segments = [segment, .. this.Manifest.Segments], OperationId = operationId });
        return checked(indexBytes + recordBytes + journalBytes + metadata.Length + 48 + 8192);
    }

    /// <summary>Reads a key from staged mutations first, then newest committed immutable segments; the disposable result owns copied bytes.</summary>
    public async ValueTask<CacheShardValue> ReadAsync(CacheIndexKey key, CancellationToken cancellationToken)
    {
        this.CheckActive();
        cancellationToken.ThrowIfCancellationRequested();
        if (this.mutations.TryGetValue(key, out var pending))
        {
            return pending.Row.Deleted ? new(this.NextSequence, true, null) : new(this.NextSequence, false, Copy(pending.Row.Value));
        }

        foreach (var segment in this.Manifest.Segments)
        {
            var reader = await this.ReaderAsync(segment, cancellationToken).ConfigureAwait(false);
            var position = await reader.FindAsync(key, cancellationToken).ConfigureAwait(false);
            if (position is not null)
            {
                return new(position.Sequence, position.Deleted, position.Deleted ? null : await reader.ReadAsync(position, cancellationToken).ConfigureAwait(false));
            }
        }

        return null;
    }

    /// <summary>Stages a bounded metadata value or tombstone for one composite key under the transaction&apos;s memory admission.</summary>
    public void Set(CacheIndexKey key, ReadOnlySpan<byte> value, bool deleted = false)
    {
        this.CheckActive();
        if (this.attempted || key is null || (this.mutations.Count >= 8192 && !this.mutations.ContainsKey(key)) || value.Length > FileSystemCacheOptions.MaximumMetadataBytes || (deleted && !value.IsEmpty))
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        if (!ResourceCharge.TryReserve(value.Length + 1536L, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        try
        {
            var change = new Mutation(new(key, this.NextSequence, value.ToArray(), deleted), memory);
            if (this.mutations.TryGetValue(key, out var previous)) { previous.Memory.Dispose(); }

            this.mutations[key] = change;
        }
        catch { memory.Dispose(); throw; }
    }

    /// <summary>Stages checked output and a journal frame, then invokes the caller&apos;s final publication guard before replacing the authoritative manifest once.</summary>
    public async ValueTask<CacheManifest> CommitAsync(string operationId, Func<long, CancellationToken, ValueTask<Result>> reserve,
        Func<Func<CancellationToken, ValueTask<long>>, CancellationToken, ValueTask<long>> publicationGuard, CancellationToken cancellationToken, long? epoch = null,
        CacheShardCommitIdentity identity = null)
    {
        this.CheckActive();
        if (this.attempted || !Guid.TryParseExact(operationId, "N", out _) || this.mutations.Count == 0 || epoch < this.Manifest.Epoch)
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        this.attempted = true;
        if (this.Manifest.Segments.Count >= CacheManifest.MaximumSegments) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var scratch, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using var metadataMemory = scratch;

        var sequence = this.NextSequence;
        identity ??= new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        if (!Guid.TryParseExact(identity.SegmentId, "N", out _) || !Guid.TryParseExact(identity.Generation, "N", out _))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        var metadataCharge = store.ControlLedger ? 0 : checked(this.Manifest.MetadataCharge + this.EstimateCommitGrowth(identity, operationId));

        var segment = await CacheSortedSegment.WriteAsync(store.DirectoryPath, identity.SegmentId, sequence, Rows(), reserve,
            store.Durable, transient, resident, cancellationToken).ConfigureAwait(false);
        store.Fault?.Invoke("index-staged");
        var generation = identity.Generation;
        using var metadata = new CacheMetadataBuffer();
        JsonSerializer.Serialize(metadata, new CacheShardJournalRecord(1, this.Manifest.StoreId, this.Manifest.Generation, generation, operationId, segment));
        var payload = metadata.GetBuffer().AsMemory(0, checked((int)metadata.Length));
        var manifest = this.Manifest with { Generation = generation, Sequence = sequence, Epoch = epoch ?? this.Manifest.Epoch,
            OperationId = operationId, MetadataCharge = metadataCharge, Segments = [segment, .. this.Manifest.Segments] };
        using var estimate = new CacheMetadataBuffer();
        JsonSerializer.Serialize(estimate, manifest);
        var required = checked(segment.IndexBytes + segment.RecordsBytes + CacheJournal.HeaderBytes + CacheJournal.TrailerBytes + payload.Length +
            estimate.Length + 48 + 4096);
        var reserved = await reserve(required, cancellationToken).ConfigureAwait(false);
        if (reserved.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var journalPath = Path.Combine(store.DirectoryPath, this.Manifest.Journal + ".journal");
        FileSystemPaths.RejectLinks(journalPath);
        await using (var journal = FileSystemFileOperations.Open(journalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous))
        {
            var bytes = await CacheJournal.AppendAsync(journal, this.Manifest.JournalBytes, sequence, payload,
                store.Profile.Native.MaximumJournalTailBytes, store.Durable, cancellationToken).ConfigureAwait(false);
            manifest = manifest with { JournalBytes = bytes };
        }

        store.Fault?.Invoke("journal-flushed");
        var publicationAttempted = false;
        var publicationConfirmed = false;
        async ValueTask<long> PublishAsync(CancellationToken token)
        {
            if (publicationAttempted) { throw new CacheTransferException(CacheErrorCode.Contract); }

            publicationAttempted = true;
            var bytes = await CacheCheckedMetadata.WriteAsync(store.DirectoryPath, "manifest.json", manifest, authority,
                store.Durable, transient, resident, token).ConfigureAwait(false);
            store.Fault?.Invoke("manifest-published");
            publicationConfirmed = true;
            return bytes;
        }

        try
        {
            if (publicationGuard is null) { await PublishAsync(cancellationToken).ConfigureAwait(false); }
            else { await publicationGuard(PublishAsync, cancellationToken).ConfigureAwait(false); }

            if (!publicationConfirmed) { throw new CacheTransferException(CacheErrorCode.Contract); }

            this.confirmedGeneration = manifest.Generation;
            return manifest;
        }
        catch (Exception) when (publicationAttempted)
        {
            authority.RetainIfShared();
            throw new CacheTransferException(CacheErrorCode.Indeterminate);
        }

        async IAsyncEnumerable<CacheSegmentRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            foreach (var mutation in this.mutations.Values) { token.ThrowIfCancellationRequested(); yield return mutation.Row; }

            await Task.CompletedTask;
        }
    }

    /// <summary>Reopens the manifest only after this transaction has confirmed its own exact publication generation.</summary>
    public async ValueTask RefreshAfterCommitAsync(CancellationToken cancellationToken)
    {
        this.CheckActive();
        if (this.confirmedGeneration is null || !this.attempted) { throw new CacheTransferException(CacheErrorCode.Contract); }

        foreach (var reader in this.readers.Values) { await reader.DisposeAsync().ConfigureAwait(false); }

        this.readers.Clear();
        var next = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(store.DirectoryPath, "manifest.json"), transient, resident, cancellationToken).ConfigureAwait(false);
        try
        {
            next.Value.Validate(this.Manifest.StoreId, this.Manifest.CollectionIdentity, this.Manifest.Shard);
            if (next.Value.Generation != this.confirmedGeneration) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            snapshot.Dispose();
            snapshot = next;
        }
        catch { next.Dispose(); throw; }

        foreach (var mutation in this.mutations.Values) { mutation.Memory.Dispose(); }

        this.mutations.Clear();
        this.confirmedGeneration = null;
        this.attempted = false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.disposed) { return; }

        this.disposed = true;
        Exception failure = null;
        try
        {
            foreach (var reader in this.readers.Values)
            {
                try { await reader.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { failure ??= exception; authority.RetainIfShared(); }
            }
        }
        finally
        {
            foreach (var mutation in this.mutations.Values) { mutation.Memory.Dispose(); }

            this.mutations.Clear();
            this.readers.Clear();

            snapshot.Dispose();
            authority.Dispose();
        }

        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    private async ValueTask<CacheIndexReader> ReaderAsync(CacheSegmentReference segment, CancellationToken cancellationToken)
    {
        if (!this.readers.TryGetValue(segment.Id, out var reader))
        {
            reader = await CacheIndexReader.OpenAsync(store.DirectoryPath, segment, transient, resident, cancellationToken, blocks).ConfigureAwait(false);
            this.readers.Add(segment.Id, reader);
        }

        return reader;
    }

    private CacheMetadataDocument<byte[]> Copy(ReadOnlyMemory<byte> value)
    {
        if (!ResourceCharge.TryReserve(value.Length + 4096L, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        try { return new(value.ToArray(), value.Length, memory); }
        catch { memory.Dispose(); throw; }
    }

    private void CheckActive()
    {
        if (this.disposed) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        authority.EnsureHeld(store.DirectoryPath);
    }

    private sealed record Mutation(CacheSegmentRow Row, ResourceCharge Memory);

    private sealed record CacheShardJournalRecord(int Format, string StoreId, string PreviousGeneration, string Generation, string OperationId, CacheSegmentReference Segment);

    // The root's reservation-control ledger has no application payload I/O or external snapshot readers.
    // Its metadata checkpoint can therefore remain under its existing short ledger authority. Collection
    // shard compaction uses a separate staged snapshot/pin and generation-CAS path.
    /// <summary>Publishes a complete control-ledger checkpoint under already-held root authority; application payload shards must use the separate staged checkpoint path.</summary>
    public async ValueTask CheckpointControlAsync(CacheSegmentReference merged, CacheShardCommitIdentity identity, string journalId,
        string operationId, CancellationToken cancellationToken)
    {
        this.CheckActive();
        if (!store.ControlLedger || this.mutations.Count != 0 || this.attempted || !Guid.TryParseExact(journalId, "N", out _) ||
            !Guid.TryParseExact(operationId, "N", out _) || merged.Sequence != this.NextSequence || merged.Id != identity.SegmentId)
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        this.attempted = true;
        var temporary = Path.Combine(store.DirectoryPath, journalId + ".journal.tmp");
        var target = Path.Combine(store.DirectoryPath, journalId + ".journal");
        FileSystemPaths.RejectLinks(temporary);
        FileSystemPaths.RejectLinks(target);
        await using (var journal = FileSystemFileOperations.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            FileSystemDurability.Flush(journal);
        }

        FileSystemDurability.Move(temporary, target, false, true);
        var manifest = this.Manifest with { Generation = identity.Generation, Sequence = this.NextSequence, CheckpointSequence = this.NextSequence,
            Journal = journalId, JournalBytes = 0, Segments = [merged], OperationId = operationId };
        await CacheCheckedMetadata.WriteAsync(store.DirectoryPath, "manifest.json", manifest, authority, true, transient, resident, cancellationToken).ConfigureAwait(false);
        this.confirmedGeneration = manifest.Generation;
    }

    /// <summary>Replaces a shard&apos;s complete immutable input set with a staged checkpoint after exact manifest-generation comparison.</summary>
    public async ValueTask<CacheManifest> PublishCheckpointAsync(string expectedGeneration, CacheManifest checkpoint,
        Func<long, CancellationToken, ValueTask<Result>> reserve,
        Func<Func<CancellationToken, ValueTask<long>>, CancellationToken, ValueTask<long>> publicationGuard, CancellationToken cancellationToken)
    {
        this.CheckActive();
        if (this.attempted || this.mutations.Count != 0) { throw new CacheTransferException(CacheErrorCode.Contract); }

        this.attempted = true;
        if (this.Manifest.Generation != expectedGeneration) { throw new CacheTransferException(CacheErrorCode.Conflict); }

        checkpoint.Validate(this.Manifest.StoreId, this.Manifest.CollectionIdentity, this.Manifest.Shard);
        if (checkpoint.Sequence != this.NextSequence || checkpoint.CheckpointSequence != this.NextSequence || checkpoint.Epoch != this.Manifest.Epoch ||
            checkpoint.RecoveryGeneration != this.Manifest.RecoveryGeneration || checkpoint.JournalBytes != 0 || checkpoint.Journal == this.Manifest.Journal ||
            checkpoint.Segments.Count != 1 || checkpoint.Segments[0].Sequence != this.NextSequence)
        {
            throw new CacheTransferException(CacheErrorCode.Contract);
        }

        var segment = checkpoint.Segments[0];
        var reserved = await reserve(checked(segment.IndexBytes + segment.RecordsBytes + 8192), cancellationToken).ConfigureAwait(false);
        if (reserved.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var started = false;
        var confirmed = false;
        async ValueTask<long> PublishAsync(CancellationToken token)
        {
            if (started) { throw new CacheTransferException(CacheErrorCode.Contract); }

            started = true;
            var bytes = await CacheCheckedMetadata.WriteAsync(store.DirectoryPath, "manifest.json", checkpoint, authority,
                store.Durable, transient, resident, token).ConfigureAwait(false);
            store.Fault?.Invoke("checkpoint-published");
            confirmed = true;
            return bytes;
        }

        try
        {
            if (publicationGuard is null) { await PublishAsync(cancellationToken).ConfigureAwait(false); }
            else { await publicationGuard(PublishAsync, cancellationToken).ConfigureAwait(false); }

            if (!confirmed) { throw new CacheTransferException(CacheErrorCode.Contract); }

            this.confirmedGeneration = checkpoint.Generation;
            return checkpoint;
        }
        catch (Exception) when (started)
        {
            authority.RetainIfShared();
            throw new CacheTransferException(CacheErrorCode.Indeterminate);
        }
    }

    /// <summary>Streams a bounded merged committed range in key order; scans cannot begin after mutations have been staged.</summary>
    public async IAsyncEnumerable<CacheShardScanValue> ScanAsync(CacheIndexKey lower, int maximumCandidates,
        [EnumeratorCancellation] CancellationToken cancellationToken, CacheIndexKey upper = null)
    {
        this.CheckActive();
        if (maximumCandidates is <= 0 or > 100000 || this.mutations.Count != 0 || this.attempted)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        if (!ResourceCharge.TryReserve(128 * CacheDefaults.KiB, out var scratch, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (scratch)
        {
            var cursors = new List<ScanCursor>();
            var queue = new PriorityQueue<ScanCursor, CacheIndexKey>();
            try
            {
                foreach (var segment in this.Manifest.Segments)
                {
                    var reader = await this.ReaderAsync(segment, cancellationToken).ConfigureAwait(false);
                    var cursor = new ScanCursor(reader, reader.FromAsync(lower, cancellationToken).GetAsyncEnumerator(cancellationToken));
                    cursors.Add(cursor);
                    if (await cursor.Values.MoveNextAsync().ConfigureAwait(false)) { queue.Enqueue(cursor, cursor.Values.Current.Key); }
                }

                for (var examined = 0; examined < maximumCandidates && queue.Count != 0; examined++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var cursor = queue.Dequeue();
                    var selected = cursor.Values.Current;
                    if (upper is not null && selected.Key.CompareTo(upper) >= 0) { yield break; }

                    var owner = cursor;
                    await AdvanceAsync(cursor).ConfigureAwait(false);
                    while (queue.TryPeek(out var duplicate, out var key) && key.Equals(selected.Key))
                    {
                        queue.Dequeue();
                        var candidate = duplicate.Values.Current;
                        if (candidate.Sequence > selected.Sequence) { selected = candidate; owner = duplicate; }
                        else if (candidate.Sequence == selected.Sequence && (candidate.Deleted != selected.Deleted || candidate.Length != selected.Length ||
                            !candidate.Hash.AsSpan().SequenceEqual(selected.Hash)))
                        {
                            throw new CacheTransferException(CacheErrorCode.Corruption);
                        }

                        await AdvanceAsync(duplicate).ConfigureAwait(false);
                    }

                    using var document = selected.Deleted ? null : await owner.Reader.ReadAsync(selected, cancellationToken).ConfigureAwait(false);
                    yield return new(selected.Key, selected.Sequence, selected.Deleted, document?.Value ?? ReadOnlyMemory<byte>.Empty);
                }

                async ValueTask AdvanceAsync(ScanCursor cursor)
                {
                    if (await cursor.Values.MoveNextAsync().ConfigureAwait(false)) { queue.Enqueue(cursor, cursor.Values.Current.Key); }
                }
            }
            finally
            {
                foreach (var cursor in cursors) { await cursor.Values.DisposeAsync().ConfigureAwait(false); }
            }
        }
    }

    private sealed record ScanCursor(CacheIndexReader Reader, IAsyncEnumerator<CacheRecordPosition> Values);
}

/// <summary>Predeclares the generated immutable segment name and authoritative manifest generation for one attempted commit.</summary>
public sealed record CacheShardCommitIdentity(string SegmentId, string Generation);

/// <summary>Owns one checked shard record or tombstone and releases copied metadata memory on disposal.</summary>
/// <example><code>using var value = await transaction.ReadAsync(key, token); if (value is not null &amp;&amp; !value.Deleted) { /* Consume value.Bytes before disposal. */ }</code></example>
public sealed class CacheShardValue(long sequence, bool deleted, CacheMetadataDocument<byte[]> document) : IDisposable
{
    /// <summary>Gets the publication sequence of the selected staged or committed record.</summary>
    public long Sequence { get; } = sequence;

    /// <summary>Gets whether the selected record is a tombstone rather than a live metadata value.</summary>
    public bool Deleted { get; } = deleted;

    /// <summary>Gets live record bytes whose memory is retained until this value is disposed, or empty bytes for a tombstone.</summary>
    public ReadOnlyMemory<byte> Bytes => document is null ? ReadOnlyMemory<byte>.Empty : document.Value;

    /// <inheritdoc />
    public void Dispose() => document?.Dispose();
}