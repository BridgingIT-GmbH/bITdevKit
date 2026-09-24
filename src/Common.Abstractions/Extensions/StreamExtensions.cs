// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Provides complete-buffer reads and conversions between streams, memory, text, bytes, and JSON values.
/// </summary>
public static class StreamExtensions
{
    /// <param name="source">The stream to read from its current position.</param>
    extension(Stream source)
    {
        /// <summary>
        ///     Read the contents of a stream into a byte array.
        /// </summary>
        public int TryReadAll(byte[] buffer, int offset, int count)
        {
            return TryReadAll(source, buffer.AsSpan(offset, count));
        }

        /// <summary>Reads repeatedly until a span is full or the stream reaches its end.</summary>
        /// <param name="buffer">The destination span to fill.</param>
        /// <returns>The number of bytes read, which may be smaller than the span length at end of stream.</returns>
        public int TryReadAll(Span<byte> buffer)
        {
            var total = 0;
            while (!buffer.IsEmpty)
            {
                var read = source.Read(buffer);
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
        public Task<int> TryReadAllAsync(
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
        public async Task<int> TryReadAllAsync(
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
        public byte[] ReadToEnd()
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
        public async Task<byte[]> ReadToEndAsync(CancellationToken cancellationToken = default)
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

        /// <summary>Copies the remaining source content into a new memory stream and rewinds the result.</summary>
        /// <param name="cancellationToken">A token that cancels the copy operation.</param>
        /// <returns>A readable memory stream positioned at the beginning.</returns>
        /// <remarks>The partially populated result is disposed when copying fails.</remarks>
        public async Task<MemoryStream> ToMemoryStreamAsync(CancellationToken cancellationToken = default)
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
        /// Converts a stream to a memory stream with optional buffer size.
        /// </summary>
        /// <param name="bufferSize">The size of the buffer for copying.</param>
        /// <returns>A memory stream containing the copied data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize is negative or zero.</exception>
        [DebuggerStepThrough]
        public Stream ToStream(int bufferSize = 81920)
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
        /// Asynchronously converts a stream to a memory stream with optional buffer size.
        /// </summary>
        /// <param name="bufferSize">The size of the buffer for copying.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation that returns a memory stream containing the copied data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize is negative or zero.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
        [DebuggerStepThrough]
        public async Task<Stream> ToStreamAsync(
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

    /// <param name="source">The source string to convert.</param>
    extension(string source)
    {
        /// <summary>
        /// Converts a string to a UTF-8 encoded stream.
        /// </summary>
        /// <returns>A memory stream containing the UTF-8 encoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        [DebuggerStepThrough]
        public Stream ToStream()
        {
            return source.ToStream(Encoding.UTF8);
        }

        /// <summary>
        /// Converts a string to a stream using the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding to use for conversion.</param>
        /// <returns>A memory stream containing the encoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source or encoding is null.</exception>
        [DebuggerStepThrough]
        public Stream ToStream(Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(encoding);

            var stream = new MemoryStream(encoding.GetBytes(source))
            {
                Position = 0
            };

            return stream;
        }
    }

    /// <param name="source">The source object to serialize.</param>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    extension<T>(T source)
    {
        /// <summary>
        /// Converts an object to a JSON stream using default serialization options.
        /// </summary>
        /// <returns>A memory stream containing the serialized JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        [DebuggerStepThrough]
        public Stream ToStream()
        {
            return source.ToStream(new JsonSerializerOptions());
        }

        /// <summary>
        /// Converts an object to a JSON stream using specified serialization options.
        /// </summary>
        /// <param name="options">The JSON serialization options to use.</param>
        /// <returns>A memory stream containing the serialized JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source or options is null.</exception>
        [DebuggerStepThrough]
        public Stream ToStream(JsonSerializerOptions options)
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
    }

    /// <param name="source">The source byte array to convert.</param>
    extension(byte[] source)
    {
        /// <summary>
        /// Converts a byte array to a stream.
        /// </summary>
        /// <returns>A memory stream containing the byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        [DebuggerStepThrough]
        public Stream ToStream()
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
        /// <returns>A task representing the asynchronous operation that returns a memory stream containing the byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        [DebuggerStepThrough]
        public async Task<Stream> ToStreamAsync()
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
    }

    /// <param name="source">The source object to serialize.</param>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    extension<T>(T source)
    {
        /// <summary>
        /// Asynchronously converts an object to a JSON stream using default serialization options.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that returns a memory stream containing the serialized JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        [DebuggerStepThrough]
        public Task<Stream> ToStreamAsync()
        {
            return source.ToStreamAsync(new JsonSerializerOptions());
        }

        /// <summary>
        /// Asynchronously converts an object to a JSON stream using specified serialization options.
        /// </summary>
        /// <param name="options">The JSON serialization options to use.</param>
        /// <returns>A task representing the asynchronous operation that returns a memory stream containing the serialized JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source or options is null.</exception>
        [DebuggerStepThrough]
        public async Task<Stream> ToStreamAsync(JsonSerializerOptions options) // TODO: use an ISerializer for serialization
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(options);

            var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, source, options);
            memoryStream.Position = 0;

            return memoryStream;
        }
    }
}
