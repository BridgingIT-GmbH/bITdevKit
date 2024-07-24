// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
using System;
using System.Threading.Tasks;

public static class StreamExtensions
{
    /// <summary>
    /// Read the contents of a stream into a byte array.
    /// </summary>
    public static int TryReadAll(this Stream source, byte[] buffer, int offset, int count)
    {
        return TryReadAll(source, buffer.AsSpan(offset, count));
    }

    public static int TryReadAll(this Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (!buffer.IsEmpty)
        {
            var read = stream.Read(buffer);
            if (read == 0)
            {
                return total;
            }

            total += read;
            buffer = buffer[read..];
        }

        return total;
    }

    /// <summary>
    /// Read the contents of a stream into a byte array.
    /// </summary>
    public static Task<int> TryReadAllAsync(this Stream source, byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        return TryReadAllAsync(source, buffer.AsMemory(offset, count), cancellationToken);
    }

    /// <summary>
    /// Read the contents of a stream into a byte array.
    /// </summary>
    public static async Task<int> TryReadAllAsync(this Stream source, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var total = 0;
        while (!buffer.IsEmpty)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return total;
            }

            total += read;
            buffer = buffer[read..];
        }

        return total;
    }

    /// <summary>
    /// Read the contents of a stream as a byte array.
    /// </summary>
    public static byte[] ReadToEnd(this Stream source)
    {
        if (source.CanSeek)
        {
            var length = source.Length - source.Position;
            if (length == 0)
            {
                return [];
            }

            var buffer = new byte[length];
            var actualLength = TryReadAll(source, buffer, 0, buffer.Length);
            Array.Resize(ref buffer, actualLength);

            return buffer;
        }

        using var memoryStream = new MemoryStream();
        source.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Read the contents of a stream as a byte array.
    /// </summary>
    public static async Task<byte[]> ReadToEndAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        if (source.CanSeek)
        {
            var length = source.Length - source.Position;
            if (length == 0)
            {
                return [];
            }

            var buffer = new byte[length];
            var actualLength = await TryReadAllAsync(source, buffer, cancellationToken).ConfigureAwait(false);
            Array.Resize(ref buffer, actualLength);

            return buffer;
        }

        using var memoryStream = new MemoryStream();
        await source.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

        return memoryStream.ToArray();
    }

    public static async Task<MemoryStream> ToMemoryStreamAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        var result = new MemoryStream();
        try
        {
            await source.CopyToAsync(result, cancellationToken).ConfigureAwait(false);
            result.Seek(0, SeekOrigin.Begin);

            return result;
        }
        catch
        {
            await result.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}