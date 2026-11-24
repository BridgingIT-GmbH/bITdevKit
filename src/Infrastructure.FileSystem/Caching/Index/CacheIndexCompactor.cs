// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Runtime.CompilerServices;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Merges immutable sorted segments without publishing them; the caller must validate the input manifest generation before publication.</summary>
/// <example><code>var merged = await CacheIndexCompactor.CompactAsync(directory, inputs, sequence, reserve, true, transient, resident, token); // Compare the input generation before publication.</code></example>
public static class CacheIndexCompactor
{
    // Immutable output is staged without shard authority. The caller must compare the complete input
    // manifest generation under native authority before publishing this output or retiring any inputs.
    /// <summary>Streams the newest row for each key into one admitted segment, rejects conflicting equal sequences, and optionally drops tombstones for a complete checkpoint.</summary>
    public static ValueTask<CacheSegmentReference> CompactAsync(string directory, IReadOnlyList<CacheSegmentReference> inputs, long sequence,
        Func<long, CancellationToken, ValueTask<Result>> reserve, bool durable, ResourceBudget transient, ResourceBudget resident,
        CancellationToken cancellationToken, bool dropTombstones = false, string outputId = null, bool pruneSecondaryIndexes = false)
    {
        if (inputs is null || inputs.Count is <= 0 or > CacheManifest.MaximumSegments || inputs.Any(i => i.Sequence >= sequence))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        return CacheSortedSegment.WriteAsync(directory, outputId ?? Guid.NewGuid().ToString("N"), sequence,
            MergeAsync(directory, inputs, transient, resident, dropTombstones, pruneSecondaryIndexes, cancellationToken), reserve, durable, transient, resident, cancellationToken);
    }

    private static async IAsyncEnumerable<CacheSegmentRow> MergeAsync(string directory, IReadOnlyList<CacheSegmentReference> inputs,
        ResourceBudget transient, ResourceBudget resident, bool dropTombstones, bool pruneSecondaryIndexes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cursors = new List<Cursor>();
        var queue = new PriorityQueue<Cursor, CacheIndexKey>();
        try
        {
            foreach (var input in inputs)
            {
                var reader = await CacheIndexReader.OpenAsync(directory, input, transient, resident, cancellationToken).ConfigureAwait(false);
                var cursor = new Cursor(reader, reader.FromAsync(null, cancellationToken).GetAsyncEnumerator(cancellationToken));
                cursors.Add(cursor);
                if (await cursor.Values.MoveNextAsync().ConfigureAwait(false)) { queue.Enqueue(cursor, cursor.Values.Current.Key); }
            }

            while (queue.Count != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cursor = queue.Dequeue();
                var selected = cursor.Values.Current;
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

                if (selected.Deleted)
                {
                    if (!dropTombstones) { yield return new(selected.Key, selected.Sequence, ReadOnlyMemory<byte>.Empty, true); }
                }
                else
                {
                    using var value = await owner.Reader.ReadAsync(selected, cancellationToken).ConfigureAwait(false);
                    if (pruneSecondaryIndexes && !await IsCurrentSecondaryAsync(selected.Key, value.Value, cursors, transient, resident, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    yield return new(selected.Key, selected.Sequence, value.Value);
                }
            }

            async ValueTask AdvanceAsync(Cursor value)
            {
                if (await value.Values.MoveNextAsync().ConfigureAwait(false)) { queue.Enqueue(value, value.Values.Current.Key); }
            }
        }
        finally
        {
            Exception failure = null;
            foreach (var cursor in cursors)
            {
                try { await cursor.Values.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { failure ??= exception; }

                try { await cursor.Reader.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { failure ??= exception; }
            }

            if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
        }
    }

    private static async ValueTask<bool> IsCurrentSecondaryAsync(CacheIndexKey key, ReadOnlyMemory<byte> pointer, IReadOnlyList<Cursor> cursors,
        ResourceBudget transient, ResourceBudget resident, CancellationToken token)
    {
        if (key.Bytes[0] is 40 or 42)
        {
            return await RetainSourceStateAsync(key, pointer, cursors, transient, resident, token).ConfigureAwait(false);
        }

        if (key.Bytes[0] is not (>= CacheMetadataIndex.LogicalKey and <= CacheMetadataIndex.Refresh or >= CacheMetadataIndex.Present and <= CacheMetadataIndex.Interval))
        {
            return true;
        }

        if (pointer.Length != 48) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var primary = CacheMetadataIndex.Primary(pointer.Span[..32]);
        CacheRecordPosition latest = null;
        CacheIndexReader owner = null;
        foreach (var cursor in cursors)
        {
            var found = await cursor.Reader.FindAsync(primary, token).ConfigureAwait(false);
            if (found is null) { continue; }

            if (latest is null || found.Sequence > latest.Sequence) { latest = found; owner = cursor.Reader; }
            else if (found.Sequence == latest.Sequence && (found.Deleted != latest.Deleted || found.Length != latest.Length || !found.Hash.AsSpan().SequenceEqual(latest.Hash)))
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }
        }

        if (latest is null || latest.Deleted) { return false; }

        if (latest.Length > 131072 || !ResourceCharge.TryReserve(33L * latest.Length + 65536, out var memory, transient, resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        using (memory)
        using (var document = await owner.ReadAsync(latest, token).ConfigureAwait(false))
        {
            CacheNativeEntry entry;
            try { entry = JsonSerializer.Deserialize<CacheNativeEntry>(document.Value) ?? throw new CacheTransferException(CacheErrorCode.Corruption); }
            catch (JsonException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            entry.Validate(entry.Payload?.StoreId);
            if (!CacheMetadataIndex.Primary(entry.Metadata.Identity).Equals(primary)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (entry.Metadata.Version.Value != new Guid(pointer.Span[32..], bigEndian: true).ToString("N")) { return false; }

            // Touch and refresh can change indexes without changing the immutable payload version.
            return CacheMetadataIndex.Keys(entry.Metadata, entry.PurgeAfter, entry.RefreshDue).Any(candidate => candidate.Equals(key));
        }
    }

    private static async ValueTask<bool> RetainSourceStateAsync(CacheIndexKey key, ReadOnlyMemory<byte> bytes, IReadOnlyList<Cursor> cursors,
        ResourceBudget transient, ResourceBudget resident, CancellationToken token)
    {
        if (bytes.Length > 131072) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        if (!ResourceCharge.TryReserve(33L * bytes.Length + 65536, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (memory)
        {
            CacheEntryIdentity logicalIdentity;
            try
            {
                if (key.Bytes[0] == 42)
                {
                    var state = JsonSerializer.Deserialize<CacheNativeEntryState>(bytes.Span) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                    state.Validate(state.Identity);
                    if (!CacheNativeEntryState.Key(state.Identity).Equals(key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    return state.Captures != 0 || await HasPrimaryAsync(CacheMetadataIndex.Primary(state.Identity), cursors, token).ConfigureAwait(false);
                }

                var logical = JsonSerializer.Deserialize<CacheNativeKeyState>(bytes.Span) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
                logical.Validate(logical.Identity);
                if (!CacheNativeKeyState.Key(logical.Identity).Equals(key)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                if (logical.Captures != 0) { return true; }

                logicalIdentity = logical.Identity;
            }
            catch (JsonException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var range = CacheMetadataIndex.PrefixRange(CacheMetadataIndex.LogicalPrefix(logicalIdentity));
            var examined = 0;
            foreach (var cursor in cursors)
            {
                await foreach (var candidate in cursor.Reader.FromAsync(range.Lower, token).ConfigureAwait(false))
                {
                    if (!range.Contains(candidate.Key)) { break; }

                    // A dense old range is retained conservatively until a later checkpoint prunes its stale pointers.
                    if (++examined > 128) { return true; }

                    if (candidate.Deleted) { continue; }

                    using var pointer = await cursor.Reader.ReadAsync(candidate, token).ConfigureAwait(false);
                    if (pointer.Value.Length != 48) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    if (await HasPrimaryAsync(CacheMetadataIndex.Primary(pointer.Value.AsSpan(0, 32)), cursors, token).ConfigureAwait(false)) { return true; }
                }
            }

            return false;
        }
    }

    private static async ValueTask<bool> HasPrimaryAsync(CacheIndexKey key, IReadOnlyList<Cursor> cursors, CancellationToken token)
    {
        CacheRecordPosition latest = null;
        foreach (var cursor in cursors)
        {
            var found = await cursor.Reader.FindAsync(key, token).ConfigureAwait(false);
            if (found is null) { continue; }

            if (latest is null || found.Sequence > latest.Sequence) { latest = found; }
            else if (found.Sequence == latest.Sequence && (found.Deleted != latest.Deleted || found.Length != latest.Length || !found.Hash.AsSpan().SequenceEqual(latest.Hash)))
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }
        }

        return latest is not null && !latest.Deleted;
    }

    private sealed record Cursor(CacheIndexReader Reader, IAsyncEnumerator<CacheRecordPosition> Values);
}