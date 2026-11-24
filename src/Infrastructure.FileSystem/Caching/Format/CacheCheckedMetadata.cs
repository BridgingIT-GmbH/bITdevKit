// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Reads and atomically publishes length-bounded JSON records protected by a format header and SHA-256 digest.</summary>
/// <example><code>using var metadata = await CacheCheckedMetadata.ReadAsync&lt;CacheManifest&gt;(path, transient, resident, token);</code></example>
public static class CacheCheckedMetadata
{
    private const int HeaderBytes = 48;
    private static readonly byte[] Magic = "BDKMETA1"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32 };

    /// <summary>Verifies framing, length and hash before deserializing JSON; the returned document retains the admitted memory charge until disposal.</summary>
    /// <typeparam name="T">The decoded checked metadata type.</typeparam>
    public static async ValueTask<CacheMetadataDocument<T>> ReadAsync<T>(string path, ResourceBudget transient,
        ResourceBudget resident, CancellationToken cancellationToken)
    {
        FileSystemPaths.RejectLinks(path);
        await using var file = FileSystemFileOperations.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length is <= HeaderBytes or > HeaderBytes + FileSystemCacheOptions.MaximumMetadataBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var header = new byte[HeaderBytes];
        try { await file.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false); }
        catch (EndOfStreamException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
        if (!header.AsSpan(0, 8).SequenceEqual(Magic) || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12)) != 1 ||
            length is <= 0 or > FileSystemCacheOptions.MaximumMetadataBytes || file.Length != HeaderBytes + length)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        // Includes the byte buffer, bounded JSON graph and parser/native-file scratch. The returned document owns this charge.
        if (!ResourceCharge.TryReserve(33L * length + 65536, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        var handedOff = false;
        try
        {
            var bytes = GC.AllocateUninitializedArray<byte>(length);
            try { await file.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false); }
            catch (EndOfStreamException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), header.AsSpan(16, 32))) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            T value;
            try { value = JsonSerializer.Deserialize<T>(bytes, Json); }
            catch (JsonException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (value is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            var document = new CacheMetadataDocument<T>(value, HeaderBytes + length, memory);
            handedOff = true;
            return document;
        }
        finally { if (!handedOff) { memory.Dispose(); } }
    }

    /// <summary>Writes one bounded, predeclared temporary record under native authority and replaces the authoritative name; an uncertain replacement reports Indeterminate.</summary>
    /// <typeparam name="T">The decoded checked metadata type.</typeparam>
    public static async ValueTask<long> WriteAsync<T>(string directory, string name, T value, FileSystemAuthority authority,
        bool durable, ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        authority.EnsureHeld(directory);
        if (string.IsNullOrEmpty(name) || name.Length > 128 || name is "." or ".." ||
            name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-')))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        if (!ResourceCharge.TryReserve(3 * FileSystemCacheOptions.MaximumMetadataBytes, out var memory, transient, resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        using (memory)
        {
            using var buffer = new CacheMetadataBuffer();
            JsonSerializer.Serialize(buffer, value, Json);
            var bytes = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));
            var header = new byte[HeaderBytes];
            Magic.CopyTo(header, 0);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), bytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), 1);
            SHA256.HashData(bytes.Span, header.AsSpan(16));
            var path = Path.Combine(directory, name);
            // One predeclared temporary name per authoritative record bounds abandoned metadata.
            // An unresolved previous temporary blocks a new attempt instead of accumulating GUID files.
            var staging = Path.Combine(directory, name + ".write.tmp");
            FileSystemPaths.RejectLinks(path);
            FileSystemPaths.RejectLinks(staging);
            var publicationStarted = false;
            var stagingOwned = false;
            try
            {
                await using (var file = FileSystemFileOperations.Open(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    stagingOwned = true;
                    await file.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await file.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                    if (durable) { FileSystemDurability.Flush(file); }
                }

                cancellationToken.ThrowIfCancellationRequested();
                publicationStarted = true;
                FileSystemDurability.Move(staging, path, true, durable);
                return HeaderBytes + bytes.Length;
            }
            catch (Exception exception) when (publicationStarted && exception is IOException or UnauthorizedAccessException)
            {
                authority.RetainIfShared();
                throw new CacheTransferException(CacheErrorCode.Indeterminate);
            }
            finally
            {
                try { if (stagingOwned) { File.Delete(staging); } }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* The owning credit remains charged for native cleanup. */ }
            }
        }
    }
}

/// <summary>Owns a decoded metadata value and its memory charge; dispose the document after the value is no longer retained.</summary>
/// <typeparam name="T">The decoded checked metadata type.</typeparam>
/// <example><code>using var document = await CacheCheckedMetadata.ReadAsync&lt;CacheManifest&gt;(path, transient, resident, token); var generation = document.Value.Generation;</code></example>
public sealed class CacheMetadataDocument<T>(T value, long bytes, ResourceCharge memory) : IDisposable
{
    /// <summary>Gets the decoded value whose lifetime is covered by this document&apos;s memory charge.</summary>
    public T Value { get; } = value;

    /// <summary>Gets the physical framed byte count associated with the decoded record.</summary>
    public long Bytes { get; } = bytes;

    /// <inheritdoc />
    public void Dispose() => memory.Dispose();
}

/// <summary>Rejects synchronous JSON writes that would exceed the configured metadata limit before growing its memory buffer.</summary>
/// <example><code>using var buffer = new CacheMetadataBuffer(65536); JsonSerializer.Serialize(buffer, value);</code></example>
public sealed class CacheMetadataBuffer(int maximumBytes = FileSystemCacheOptions.MaximumMetadataBytes) : MemoryStream
{
    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > maximumBytes - this.Length) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        base.Write(buffer);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count > maximumBytes - this.Length) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        base.Write(buffer, offset, count);
    }
}