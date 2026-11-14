// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Appends checked commit frames and reads only the authoritative committed journal prefix.</summary>
/// <example><code>await foreach (var frame in CacheJournal.ReadCommittedAsync(file, manifest.JournalBytes, manifest.CheckpointSequence + 1, transient, resident, token)) { /* Consume before advancing. */ }</code></example>
public static class CacheJournal
{
    /// <summary>The 24-byte frame header containing the format, sequence and payload length.</summary>
    public const int HeaderBytes = 24;

    /// <summary>The 44-byte frame trailer containing the digest, repeated sequence and commit marker.</summary>
    public const int TrailerBytes = 44;
    private const uint Magic = 0x4A4B4442; // BDKJ
    private const uint Committed = 0x54494D43; // CMIT

    // The caller holds this shard's native authority and charges the supplied JSON bytes.
    // Appending and flushing a journal frame alone never makes its mutation visible.
    /// <summary>Truncates only an uncommitted suffix and appends a checked frame; visibility still requires the caller&apos;s authoritative manifest replacement.</summary>
    public static async ValueTask<long> AppendAsync(FileStream file, long committedBytes, long sequence, ReadOnlyMemory<byte> payload,
        long maximumTailBytes, bool durable, CancellationToken cancellationToken)
    {
        if (sequence <= 0 || committedBytes < 0 || payload.IsEmpty || payload.Length > FileSystemCacheOptions.MaximumMetadataBytes ||
            maximumTailBytes is <= 0 or > 32 * CacheDefaults.MiB || committedBytes > maximumTailBytes - HeaderBytes - TrailerBytes - payload.Length)
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        if (file.Length < committedBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        // Only an uncommitted suffix is discarded, under the caller's already proven native authority.
        if (file.Length != committedBytes) { file.SetLength(committedBytes); }

        file.Position = committedBytes;
        var header = new byte[HeaderBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 1);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(8), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), payload.Length);
        var trailer = new byte[TrailerBytes];
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            hash.AppendData(header);
            hash.AppendData(payload.Span);
            hash.GetHashAndReset(trailer.AsSpan(0, 32));
        }

        BinaryPrimitives.WriteInt64LittleEndian(trailer.AsSpan(32), sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(40), Committed);
        await file.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await file.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await file.WriteAsync(trailer, cancellationToken).ConfigureAwait(false);
        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (durable) { FileSystemDurability.Flush(file); }

        return file.Position;
    }

    /// <summary>Streams checked frames with consecutive sequences through the committed byte boundary; each frame is valid only until enumeration advances or it is disposed.</summary>
    public static async IAsyncEnumerable<CacheJournalFrame> ReadCommittedAsync(FileStream file, long committedBytes, long firstSequence,
        ResourceBudget transient, ResourceBudget resident, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (committedBytes < 0 || committedBytes > 32 * CacheDefaults.MiB || file.Length < committedBytes || firstSequence <= 0)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        file.Position = 0;
        var header = new byte[HeaderBytes];
        var trailer = new byte[TrailerBytes];
        var sequence = firstSequence;
        while (file.Position < committedBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (committedBytes - file.Position < HeaderBytes + TrailerBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            await ReadExactlyAsync(file, header, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)) != 1 ||
                BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)) != sequence || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(20)) != 0 ||
                length is <= 0 or > FileSystemCacheOptions.MaximumMetadataBytes || length > committedBytes - file.Position - TrailerBytes)
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            if (!ResourceCharge.TryReserve(length + 4096L, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            using (memory)
            {
                var payload = GC.AllocateUninitializedArray<byte>(length);
                await ReadExactlyAsync(file, payload, cancellationToken).ConfigureAwait(false);
                await ReadExactlyAsync(file, trailer, cancellationToken).ConfigureAwait(false);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                hash.AppendData(header);
                hash.AppendData(payload);
                var digest = hash.GetHashAndReset();
                if (!CryptographicOperations.FixedTimeEquals(digest, trailer.AsSpan(0, 32)) ||
                    BinaryPrimitives.ReadInt64LittleEndian(trailer.AsSpan(32)) != sequence || BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan(40)) != Committed)
                {
                    throw new CacheTransferException(CacheErrorCode.Corruption);
                }

                using var frame = new CacheJournalFrame(sequence, payload, memory);
                yield return frame;
            }

            sequence = checked(sequence + 1);
        }
    }

    private static async ValueTask ReadExactlyAsync(FileStream file, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try { await file.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false); }
        catch (EndOfStreamException) { throw new CacheTransferException(CacheErrorCode.Corruption); }
    }
}