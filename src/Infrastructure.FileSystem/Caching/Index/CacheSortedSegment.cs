// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Writes format-one immutable sorted indexes with checked fixed-size blocks and separately hashed metadata records.</summary>
/// <example><code>var headerCount = CacheSortedSegment.ValidateHeader(header, reference);</code></example>
public static class CacheSortedSegment
{
    /// <summary>The fixed 128-byte immutable index header size.</summary>
    public const int HeaderBytes = 128;

    /// <summary>The fixed checked block width used by binary search and range reads.</summary>
    public const int BlockBytes = 65536;

    /// <summary>The fixed byte width of one composite-key index entry and its record pointer.</summary>
    public const int EntryBytes = 256;

    /// <summary>The maximum number of fixed-width entries fitting before a block&apos;s SHA-256 trailer.</summary>
    public const int EntriesPerBlock = (BlockBytes - 32) / EntryBytes;
    private static readonly byte[] Magic = "BDKIDX01"u8.ToArray();

    /// <summary>Streams strictly ordered rows into predeclared immutable files under incremental storage and memory admission, returning their exact lengths and full hashes.</summary>
    public static async ValueTask<CacheSegmentReference> WriteAsync(string directory, string id, long sequence, IAsyncEnumerable<CacheSegmentRow> rows,
        Func<long, CancellationToken, ValueTask<Result>> reserve, bool durable, ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(id, "N", out _) || sequence < 0) { throw new CacheTransferException(CacheErrorCode.Validation); }

        ArgumentNullException.ThrowIfNull(reserve);
        if (!ResourceCharge.TryReserve(BlockBytes + 8192L, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using (memory)
        {
            var indexPath = Path.Combine(directory, id + ".idx");
            var recordsPath = Path.Combine(directory, id + ".records");
            var indexStage = indexPath + ".tmp";
            var recordsStage = recordsPath + ".tmp";
            foreach (var path in new[] { indexPath, recordsPath, indexStage, recordsStage }) { FileSystemPaths.RejectLinks(path); }

            var indexOwned = false;
            var recordsOwned = false;
            try
            {
                await ReserveAsync(HeaderBytes).ConfigureAwait(false);
                long count = 0;
                long recordBytes = 0;
                var block = new byte[BlockBytes];
                var buffered = 0;
                CacheIndexKey previous = null;
                string indexHash;
                string recordsHash;
                long indexBytes;
                await using (var index = OpenStage(indexStage, out indexOwned))
                await using (var records = OpenStage(recordsStage, out recordsOwned))
                {
                    await index.WriteAsync(new byte[HeaderBytes], cancellationToken).ConfigureAwait(false);
                    using var recordHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        if (row is null || row.Key is null || row.Sequence < 0 || row.Sequence > sequence || row.Value.Length > FileSystemCacheOptions.MaximumMetadataBytes ||
                            (row.Deleted && !row.Value.IsEmpty) || (previous is not null && previous.CompareTo(row.Key) >= 0))
                        {
                            throw new CacheTransferException(CacheErrorCode.Contract);
                        }

                        var total = checked(HeaderBytes + ((count / EntriesPerBlock) + 1) * BlockBytes + recordBytes + row.Value.Length);
                        await ReserveAsync(total).ConfigureAwait(false);
                        var offset = buffered * EntryBytes;
                        row.Key.Bytes.CopyTo(block.AsSpan(offset, CacheIndexKey.Size));
                        BinaryPrimitives.WriteInt64LittleEndian(block.AsSpan(offset + 192), row.Sequence);
                        BinaryPrimitives.WriteInt64LittleEndian(block.AsSpan(offset + 200), recordBytes);
                        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(offset + 208), row.Value.Length);
                        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(offset + 212), row.Deleted ? 1 : 0);
                        SHA256.HashData(row.Value.Span, block.AsSpan(offset + 216, 32));
                        for (var position = 0; position < row.Value.Length; position += BlockBytes)
                        {
                            var part = row.Value.Slice(position, Math.Min(BlockBytes, row.Value.Length - position));
                            await records.WriteAsync(part, cancellationToken).ConfigureAwait(false);
                            recordHash.AppendData(part.Span);
                        }

                        recordBytes += row.Value.Length;
                        previous = row.Key;
                        count++;
                        if (++buffered == EntriesPerBlock)
                        {
                            await WriteBlockAsync(index, block, cancellationToken).ConfigureAwait(false);
                            Array.Clear(block);
                            buffered = 0;
                        }
                    }

                    if (buffered != 0) { await WriteBlockAsync(index, block, cancellationToken).ConfigureAwait(false); }

                    var header = Header(count, sequence, recordBytes);
                    index.Position = 0;
                    await index.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await records.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await index.FlushAsync(cancellationToken).ConfigureAwait(false);
                    if (durable) { FileSystemDurability.Flush(records); FileSystemDurability.Flush(index); }

                    indexBytes = index.Length;
                    index.Position = 0;
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    int read;
                    while ((read = await index.ReadAsync(block, cancellationToken).ConfigureAwait(false)) != 0) { hash.AppendData(block.AsSpan(0, read)); }

                    indexHash = Convert.ToHexStringLower(hash.GetHashAndReset());
                    recordsHash = Convert.ToHexStringLower(recordHash.GetHashAndReset());
                }

                FileSystemDurability.Move(recordsStage, recordsPath, false, durable);
                FileSystemDurability.Move(indexStage, indexPath, false, durable);
                return new(id, sequence, indexBytes, recordBytes, indexHash, recordsHash);
            }
            finally
            {
                foreach (var path in new[] { indexOwned ? indexStage : null, recordsOwned ? recordsStage : null }.Where(p => p is not null))
                {
                    try { File.Delete(path); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* The native owner retains unresolved staging credits. */ }
                }
            }
        }

        async ValueTask ReserveAsync(long bytes)
        {
            var result = await reserve(bytes, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) { throw new CacheTransferException(result.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.Capacity); }
        }
    }

    /// <summary>Builds the checked immutable-segment header from the entry count, sequence, record length and record digest.</summary>
    public static byte[] Header(long count, long sequence, long recordsBytes)
    {
        var bytes = new byte[HeaderBytes];
        Magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), CacheIndexKey.Size);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), EntryBytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), BlockBytes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(24), count);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(32), sequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(40), recordsBytes);
        SHA256.HashData(bytes.AsSpan(0, 64), bytes.AsSpan(64, 32));
        return bytes;
    }

    /// <summary>Verifies segment header framing, count, sequence and record attribution against the supplied immutable reference.</summary>
    public static long ValidateHeader(byte[] bytes, CacheSegmentReference reference)
    {
        if (reference.IndexBytes < HeaderBytes || (reference.IndexBytes - HeaderBytes) % BlockBytes != 0 || reference.RecordsBytes < 0 ||
            !bytes.AsSpan(0, 8).SequenceEqual(Magic) || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)) != 1 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)) != CacheIndexKey.Size || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)) != EntryBytes ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20)) != BlockBytes || BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(32)) != reference.Sequence ||
            BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(40)) != reference.RecordsBytes ||
            bytes.AsSpan(96).IndexOfAnyExcept((byte)0) >= 0 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0, 64)), bytes.AsSpan(64, 32)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        var count = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(24));
        if (count < 0 || count > (reference.IndexBytes - HeaderBytes) / BlockBytes * EntriesPerBlock ||
            HeaderBytes + (count / EntriesPerBlock + (count % EntriesPerBlock == 0 ? 0 : 1)) * BlockBytes != reference.IndexBytes)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        return count;
    }

    private static async ValueTask WriteBlockAsync(FileStream file, byte[] block, CancellationToken cancellationToken)
    {
        SHA256.HashData(block.AsSpan(0, BlockBytes - 32), block.AsSpan(BlockBytes - 32));
        await file.WriteAsync(block, cancellationToken).ConfigureAwait(false);
    }

    private static FileStream OpenStage(string path, out bool owned)
    {
        owned = false;
        var file = FileSystemFileOperations.Open(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);
        owned = true;
        return file;
    }
}