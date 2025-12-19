// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Text.Json;

public static class StreamExtensions
{
    /// <summary>
    ///     Read the contents of a stream into a byte array.
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
    ///     Read the contents of a stream into a byte array.
    /// </summary>
    public static Task<int> TryReadAllAsync(
        this Stream source,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default)
    {
        return TryReadAllAsync(source, buffer.AsMemory(offset, count), cancellationToken);
    }

    /// <summary>
    ///     Read the contents of a stream into a byte array.
    /// </summary>
    public static async Task<int> TryReadAllAsync(
        this Stream source,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
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
    ///     Read the contents of a stream as a byte array.
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
    ///     Read the contents of a stream as a byte array.
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

    /// <summary>
    /// Converts a string to a UTF-8 encoded stream.
    /// </summary>
    /// <param name="source">The source string to convert.</param>
    /// <returns>A memory stream containing the UTF-8 encoded string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream(this string source)
    {
        return source.ToStream(Encoding.UTF8);
    }

    /// <summary>
    /// Converts a string to a stream using the specified encoding.
    /// </summary>
    /// <param name="source">The source string to convert.</param>
    /// <param name="encoding">The encoding to use for conversion.</param>
    /// <returns>A memory stream containing the encoded string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or encoding is null.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream(this string source, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(encoding);

        var stream = new MemoryStream(encoding.GetBytes(source))
        {
            Position = 0
        };

        return stream;
    }

    /// <summary>
    /// Converts an object to a JSON stream using default serialization options.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="source">The source object to serialize.</param>
    /// <returns>A memory stream containing the serialized JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream<T>(this T source)
    {
        return source.ToStream(new JsonSerializerOptions());
    }

    /// <summary>
    /// Converts an object to a JSON stream using specified serialization options.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="source">The source object to serialize.</param>
    /// <param name="options">The JSON serialization options to use.</param>
    /// <returns>A memory stream containing the serialized JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or options is null.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream<T>(this T source, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        var json = JsonSerializer.Serialize(source, options);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))
        {
            Position = 0
        };

        return stream;
    }

    /// <summary>
    /// Converts a byte array to a stream.
    /// </summary>
    /// <param name="source">The source byte array to convert.</param>
    /// <returns>A memory stream containing the byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream(this byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var stream = new MemoryStream(source)
        {
            Position = 0
        };

        return stream;
    }

    /// <summary>
    /// Asynchronously converts a byte array to a stream.
    /// </summary>
    /// <param name="source">The source byte array to convert.</param>
    /// <returns>A task representing the asynchronous operation that returns a memory stream containing the byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    [DebuggerStepThrough]
    public static async Task<Stream> ToStreamAsync(this byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var memoryStream = new MemoryStream(source.Length);
        try
        {
            await memoryStream.WriteAsync(source);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Converts a stream to a memory stream with optional buffer size.
    /// </summary>
    /// <param name="source">The source stream to convert.</param>
    /// <param name="bufferSize">The size of the buffer for copying.</param>
    /// <returns>A memory stream containing the copied data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize is negative or zero.</exception>
    [DebuggerStepThrough]
    public static Stream ToStream(this Stream source, int bufferSize = 81920)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        var memoryStream = new MemoryStream();
        try
        {
            source.CopyTo(memoryStream, bufferSize);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Asynchronously converts an object to a JSON stream using default serialization options.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="source">The source object to serialize.</param>
    /// <returns>A task representing the asynchronous operation that returns a memory stream containing the serialized JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    [DebuggerStepThrough]
    public static Task<Stream> ToStreamAsync<T>(this T source)
    {
        return source.ToStreamAsync(new JsonSerializerOptions());
    }

    /// <summary>
    /// Asynchronously converts an object to a JSON stream using specified serialization options.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="source">The source object to serialize.</param>
    /// <param name="options">The JSON serialization options to use.</param>
    /// <returns>A task representing the asynchronous operation that returns a memory stream containing the serialized JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or options is null.</exception>
    [DebuggerStepThrough]
    public static async Task<Stream> ToStreamAsync<T>(this T source, JsonSerializerOptions options) // TODO: use an ISerializer for serialization
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, source, options);
        memoryStream.Position = 0;

        return memoryStream;
    }

    /// <summary>
    /// Asynchronously converts a stream to a memory stream with optional buffer size.
    /// </summary>
    /// <param name="source">The source stream to convert.</param>
    /// <param name="bufferSize">The size of the buffer for copying.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation that returns a memory stream containing the copied data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize is negative or zero.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    [DebuggerStepThrough]
    public static async Task<Stream> ToStreamAsync(
        this Stream source,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        var memoryStream = new MemoryStream();
        try
        {
            await source.CopyToAsync(memoryStream, bufferSize, cancellationToken);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();

            throw;
        }
    }
}