// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Retains verified immutable index blocks within a byte budget and pins them until each reader releases its lease.</summary>
/// <example><code>using var blocks = new CacheIndexBlockCache(ByteSize.Megabytes(4));</code></example>
public sealed class CacheIndexBlockCache(long maximumBytes) : IDisposable
{
    private const int ChargeBytes = CacheSortedSegment.BlockBytes + 1024;
    private readonly Lock gate = new();
    private readonly ResourceBudget budget = new("filesystem-index-blocks", maximumBytes);
    private readonly Dictionary<BlockKey, BlockEntry> entries = [];
    private readonly LinkedList<BlockEntry> recent = [];
    private bool disposed;

    /// <summary>Gets bytes currently charged to retained index blocks, including blocks pinned by readers.</summary>
    public long Used => this.budget.Used;

    /// <summary>Returns a pinned verified block; bounded eviction precedes an uncached, still-charged fallback when retention cannot be admitted.</summary>
    public async ValueTask<CacheIndexBlockLease> ReadAsync(CacheSegmentReference segment, long ordinal,
        Func<Memory<byte>, CancellationToken, ValueTask> read, ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = new BlockKey(segment.Id, segment.IndexHash, ordinal);
        ResourceCharge charge = null;
        var retain = false;
        lock (this.gate)
        {
            if (!this.disposed && this.entries.TryGetValue(key, out var found)) { return this.Pin(found); }

            if (!this.disposed)
            {
                var node = this.recent.Last;
                for (var inspected = 0; inspected < 64 && !ResourceCharge.TryReserve(ChargeBytes, out charge, this.budget, resident) && node is not null; inspected++)
                {
                    var previous = node.Previous;
                    if (node.Value.Readers == 0) { this.Retire(node.Value); }

                    node = previous;
                }

                retain = charge is not null;
            }
        }

        if (charge is null && !ResourceCharge.TryReserve(ChargeBytes, out charge, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var handedOff = false;
        try
        {
            var block = await ReadBlockAsync(read, transient, cancellationToken).ConfigureAwait(false);
            if (retain)
            {
                lock (this.gate)
                {
                    if (!this.disposed)
                    {
                        if (this.entries.TryGetValue(key, out var found)) { return this.Pin(found); }

                        var entry = new BlockEntry(key, block, charge);
                        entry.Node = this.recent.AddFirst(entry);
                        this.entries.Add(key, entry);
                        handedOff = true;
                        return this.Pin(entry);
                    }
                }
            }

            handedOff = true;
            return new(block, charge.Dispose);
        }
        finally { if (!handedOff) { charge.Dispose(); } }
    }

    /// <summary>Reads and verifies one index block under transient and resident admission without retaining it in the block cache.</summary>
    public static async ValueTask<CacheIndexBlockLease> ReadUncachedAsync(Func<Memory<byte>, CancellationToken, ValueTask> read,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ResourceCharge.TryReserve(ChargeBytes, out var memory, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        try { return new(await ReadBlockAsync(read, transient, cancellationToken).ConfigureAwait(false), memory.Dispose); }
        catch { memory.Dispose(); throw; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            while (this.recent.Last is { } node) { this.Retire(node.Value); }
        }
    }

    private CacheIndexBlockLease Pin(BlockEntry entry)
    {
        entry.Readers++;
        this.recent.Remove(entry.Node);
        this.recent.AddFirst(entry.Node);
        return new(entry.Bytes, () =>
        {
            lock (this.gate)
            {
                entry.Readers--;
                if (entry.Retired && entry.Readers == 0) { entry.Memory.Dispose(); }
            }
        });
    }

    private void Retire(BlockEntry entry)
    {
        this.entries.Remove(entry.Key);
        this.recent.Remove(entry.Node);
        entry.Retired = true;
        if (entry.Readers == 0) { entry.Memory.Dispose(); }
    }

    private static async ValueTask<byte[]> ReadBlockAsync(Func<Memory<byte>, CancellationToken, ValueTask> read,
        ResourceBudget transient, CancellationToken cancellationToken)
    {
        if (!transient.TryReserve(CacheSortedSegment.BlockBytes, out var transfer)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (transfer)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(CacheSortedSegment.BlockBytes);
            await read(bytes, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0, CacheSortedSegment.BlockBytes - 32)), bytes.AsSpan(CacheSortedSegment.BlockBytes - 32)))
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            return bytes;
        }
    }

    private readonly record struct BlockKey(string Segment, string Hash, long Ordinal);

    private sealed class BlockEntry(BlockKey key, byte[] bytes, ResourceCharge memory)
    {
        public BlockKey Key { get; } = key;

        public byte[] Bytes { get; } = bytes;

        public ResourceCharge Memory { get; } = memory;

        public int Readers { get; set; }

        public bool Retired { get; set; }

        public LinkedListNode<BlockEntry> Node { get; set; }
    }
}

/// <summary>Owns access to a verified index block and invokes its release callback once when disposed.</summary>
/// <example><code>using var block = await blocks.ReadAsync(segment, ordinal, readExactly, transient, resident, token);</code></example>
public sealed class CacheIndexBlockLease(byte[] bytes, Action release) : IDisposable
{
    private Action release = release;

    /// <summary>Gets the verified block bytes; callers must treat them as immutable and stop using them before disposing this lease.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref this.release, null)?.Invoke();
}