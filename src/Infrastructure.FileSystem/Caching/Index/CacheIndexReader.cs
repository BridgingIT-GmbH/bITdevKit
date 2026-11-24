// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Opens an immutable sorted segment for checked binary searches and streamed range reads with bounded block caching.</summary>
/// <example><code>await using var reader = await CacheIndexReader.OpenAsync(directory, segment, transient, resident, token); var position = await reader.FindAsync(key, token);</code></example>
public sealed class CacheIndexReader : IAsyncDisposable
{
    private readonly FileStream index;
    private readonly FileStream records;
    private readonly CacheSegmentReference reference;
    private readonly ResourceBudget transient;
    private readonly ResourceBudget resident;
    private readonly ResourceCharge memory;
    private readonly CacheIndexBlockCache cache;
    private CacheIndexBlockLease block;
    private long loadedBlock = -1;

    private CacheIndexReader(FileStream index, FileStream records, CacheSegmentReference reference, long count,
        ResourceBudget transient, ResourceBudget resident, ResourceCharge memory, CacheIndexBlockCache cache)
    {
        this.index = index;
        this.records = records;
        this.reference = reference;
        this.Count = count;
        this.transient = transient;
        this.resident = resident;
        this.memory = memory;
        this.cache = cache;
    }

    /// <summary>Gets the number of entries declared by the verified segment header.</summary>
    public long Count { get; }

    /// <summary>Gets the number of index block loads performed by this reader.</summary>
    public int BlockReads { get; private set; }

    /// <summary>Opens the exact index and record files, verifies their lengths and header, and retains its memory admission until disposal.</summary>
    public static async ValueTask<CacheIndexReader> OpenAsync(string directory, CacheSegmentReference reference,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken, CacheIndexBlockCache cache = null)
    {
        if (reference is null || !reference.Valid(long.MaxValue)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        if (!ResourceCharge.TryReserve(16384L, out var memory, transient, resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        FileStream index = null;
        FileStream records = null;
        var handedOff = false;
        try
        {
            index = Open(directory, reference.Id + ".idx");
            records = Open(directory, reference.Id + ".records");
            if (index.Length != reference.IndexBytes || records.Length != reference.RecordsBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var header = new byte[CacheSortedSegment.HeaderBytes];
            await ReadExactlyAsync(index, header, cancellationToken).ConfigureAwait(false);
            var count = CacheSortedSegment.ValidateHeader(header, reference);
            var reader = new CacheIndexReader(index, records, reference, count, transient, resident, memory, cache);
            handedOff = true;
            return reader;
        }
        catch (FileNotFoundException) { throw new CacheTransferException(CacheErrorCode.Corruption); }
        finally
        {
            if (!handedOff)
            {
                try
                {
                    if (index is not null) { await index.DisposeAsync().ConfigureAwait(false); }
                }
                finally
                {
                    try { if (records is not null) { await records.DisposeAsync().ConfigureAwait(false); } }
                    finally { memory.Dispose(); }
                }
            }
        }
    }

    /// <summary>Binary-searches for an exact key and returns its record position, or null when absent.</summary>
    public async ValueTask<CacheRecordPosition> FindAsync(CacheIndexKey key, CancellationToken cancellationToken)
    {
        var position = await this.LowerBoundAsync(key, cancellationToken).ConfigureAwait(false);
        if (position == this.Count) { return null; }

        var found = await this.AtAsync(position, cancellationToken).ConfigureAwait(false);
        return found.Key.Equals(key) ? found : null;
    }

    /// <summary>Streams positions in key order starting at the inclusive lower bound, or at the first key when null.</summary>
    public async IAsyncEnumerable<CacheRecordPosition> FromAsync(CacheIndexKey lower,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var position = lower is null ? 0 : await this.LowerBoundAsync(lower, cancellationToken).ConfigureAwait(false);
        while (position < this.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await this.AtAsync(position++, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Checks the record bounds and SHA-256 digest, returning bytes whose memory admission belongs to the disposable result.</summary>
    public async ValueTask<CacheMetadataDocument<byte[]>> ReadAsync(CacheRecordPosition position, CancellationToken cancellationToken)
    {
        if (position.Length < 0 || position.Length > FileSystemCacheOptions.MaximumMetadataBytes || position.Offset < 0 ||
            position.Offset > this.reference.RecordsBytes - position.Length || (position.Deleted && position.Length != 0))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        if (!ResourceCharge.TryReserve(position.Length + 4096L, out var memory, this.transient, this.resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        var handedOff = false;
        try
        {
            var bytes = GC.AllocateUninitializedArray<byte>(position.Length);
            this.records.Position = position.Offset;
            for (var offset = 0; offset < bytes.Length; offset += CacheSortedSegment.BlockBytes)
            {
                await ReadExactlyAsync(this.records, bytes.AsMemory(offset, Math.Min(CacheSortedSegment.BlockBytes, bytes.Length - offset)), cancellationToken).ConfigureAwait(false);
            }

            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), position.Hash)) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var document = new CacheMetadataDocument<byte[]>(bytes, bytes.Length, memory);
            handedOff = true;
            return document;
        }
        finally { if (!handedOff) { memory.Dispose(); } }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try { await this.index.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { await this.records.DisposeAsync().ConfigureAwait(false); }
            finally { this.block?.Dispose(); this.memory.Dispose(); }
        }
    }

    private async ValueTask<long> LowerBoundAsync(CacheIndexKey key, CancellationToken cancellationToken)
    {
        long low = 0;
        var high = this.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var row = await this.AtAsync(middle, cancellationToken).ConfigureAwait(false);
            if (row.Key.CompareTo(key) < 0) { low = middle + 1; }
            else { high = middle; }
        }

        return low;
    }

    private async ValueTask<CacheRecordPosition> AtAsync(long ordinal, CancellationToken cancellationToken)
    {
        var blockOrdinal = ordinal / CacheSortedSegment.EntriesPerBlock;
        if (this.loadedBlock != blockOrdinal)
        {
            this.block?.Dispose();
            this.block = null;
            this.loadedBlock = -1;
            async ValueTask ReadAsync(Memory<byte> bytes, CancellationToken token)
            {
                this.index.Position = CacheSortedSegment.HeaderBytes + blockOrdinal * CacheSortedSegment.BlockBytes;
                await ReadExactlyAsync(this.index, bytes, token).ConfigureAwait(false);
                this.BlockReads++;
            }

            this.block = this.cache is null ? await CacheIndexBlockCache.ReadUncachedAsync(ReadAsync, this.transient, this.resident, cancellationToken).ConfigureAwait(false) :
                await this.cache.ReadAsync(this.reference, blockOrdinal, ReadAsync, this.transient, this.resident, cancellationToken).ConfigureAwait(false);
            this.loadedBlock = blockOrdinal;
        }

        var block = this.block.Bytes;
        var offset = (int)(ordinal % CacheSortedSegment.EntriesPerBlock) * CacheSortedSegment.EntryBytes;
        var sequence = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(offset + 192));
        var length = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset + 208));
        var flags = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset + 212));
        var recordOffset = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(offset + 200));
        if (sequence < 0 || sequence > this.reference.Sequence || length < 0 || length > FileSystemCacheOptions.MaximumMetadataBytes || flags is < 0 or > 1 ||
            block[offset] == 0 || (flags == 1 && length != 0) || recordOffset < 0 || recordOffset > this.reference.RecordsBytes - length ||
            BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(offset + 248)) != 0)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        return new(new CacheIndexKey(block.AsSpan(offset, CacheIndexKey.Size)), sequence,
            recordOffset, length, block.AsSpan(offset + 216, 32).ToArray(), flags == 1);
    }

    private static FileStream Open(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        FileSystemPaths.RejectLinks(path);
        return FileSystemFileOperations.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
    }

    private static async ValueTask ReadExactlyAsync(FileStream file, Memory<byte> destination, CancellationToken cancellationToken)
    {
        try { await file.ReadExactlyAsync(destination, cancellationToken).ConfigureAwait(false); }
        catch (EndOfStreamException) { throw new CacheTransferException(CacheErrorCode.Corruption); }
    }
}