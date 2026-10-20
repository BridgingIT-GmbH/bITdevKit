// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Collections.Concurrent;
using System.Formats.Cbor;
using System.Numerics;

/// <summary>
///     Serializes object instances directly as Concise Binary Object Representation (CBOR).
/// </summary>
/// <remarks>
///     Public properties and fields use their CLR names by default. Data-contract attributes select and name members,
///     including explicitly annotated nonpublic members. <see cref="CborConstructorAttribute"/> selects a constructor.
///     Type mappings and compiled accessors are cached. Byte arrays and numbers use native CBOR encodings.
///     Byte-array extensions use direct buffers with bounded writer reuse. Returned arrays remain independently owned.
///     Streams remain open. String extensions represent the complete binary payload as Base64.
///     JSON settings and converters do not apply.
/// </remarks>
/// <example>
/// <code>
/// ISerializer serializer = new CborSerializer();
/// var bytes = serializer.SerializeToBytes(new[] { 1, 2, 3 });
/// var values = serializer.Deserialize&lt;int[]&gt;(bytes);
/// </code>
/// </example>
public class CborSerializer : ISerializer, IBufferSerializer
{
    private const int SMALL_WRITER_LIMIT = 64 * 1024;
    private const int LARGE_WRITER_LIMIT = 1024 * 1024;
    private static readonly ConcurrentBag<PooledWriter> SmallWriters = [];
    private static readonly ConcurrentBag<PooledWriter> LargeWriters = [];
    private static readonly ConcurrentBag<CborReader> Readers = [];
    private static int smallWriterCount;
    private static int largeWriterCount;
    private static int readerCount;
    private readonly int maxDepth;

    /// <summary>Initializes a serializer with a limit on nested values.</summary>
    /// <param name="maxDepth">The maximum nesting depth. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">The depth is not positive.</exception>
    /// <example><code>var serializer = new CborSerializer(maxDepth: 64);</code></example>
    public CborSerializer(int maxDepth = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        this.maxDepth = maxDepth;
    }

    /// <summary>Writes the value at the output stream's current position using its runtime type.</summary>
    /// <param name="value">The value. A null value is ignored.</param>
    /// <param name="output">The destination. A null stream is ignored.</param>
    /// <example><code>new CborSerializer().Serialize(new[] { 1, 2, 3 }, stream);</code></example>
    public void Serialize(object value, Stream output)
    {
        if (value is null || output is null)
        {
            return;
        }

        this.SerializeCore(value, output);
    }

    bool IBufferSerializer.SupportsDirectBuffers => this.GetType() == typeof(CborSerializer);

    byte[] IBufferSerializer.SerializeToBytes(object value)
    {
        return value is null ? null : this.SerializeCore(value, null);
    }

    object IBufferSerializer.Deserialize(ReadOnlyMemory<byte> input, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return input.IsEmpty ? null : this.DeserializeCore(input, type);
    }

    private byte[] SerializeCore(object value, Stream output)
    {
        var pooled = RentWriter();
        var writer = pooled.Writer;
        var completed = false;
        try
        {
            this.WriteValue(writer, value, 0);
            if (output is null)
            {
                var bytes = writer.Encode();
                completed = true;
                return bytes;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(writer.BytesWritten);
            try
            {
                var length = writer.Encode(buffer);
                output.Write(buffer.AsSpan(0, length));
                completed = true;
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            if (completed)
            {
                ReturnWriter(pooled);
            }
            else
            {
                // Failed writes may roll back BytesWritten after growing the internal buffer.
                writer.Reset();
            }
        }
    }

    private static PooledWriter RentWriter()
    {
        // Prefer an already-grown writer when available rather than growing another small one.
        if (LargeWriters.TryTake(out var large))
        {
            Interlocked.Decrement(ref largeWriterCount);
            return large;
        }

        if (SmallWriters.TryTake(out var small))
        {
            Interlocked.Decrement(ref smallWriterCount);
            return small;
        }

        return new PooledWriter();
    }

    private static void ReturnWriter(PooledWriter pooled)
    {
        pooled.MaximumBytesWritten = Math.Max(pooled.MaximumBytesWritten, pooled.Writer.BytesWritten);
        pooled.Writer.Reset();
        if (pooled.MaximumBytesWritten <= SMALL_WRITER_LIMIT)
        {
            ReturnWriter(SmallWriters, pooled, ref smallWriterCount, 6);
        }
        else if (pooled.MaximumBytesWritten <= LARGE_WRITER_LIMIT)
        {
            ReturnWriter(LargeWriters, pooled, ref largeWriterCount, 2);
        }
    }

    private static void ReturnWriter(ConcurrentBag<PooledWriter> pool, PooledWriter writer, ref int count, int limit)
    {
        if (Interlocked.Increment(ref count) <= limit)
        {
            pool.Add(writer);
        }
        else
        {
            Interlocked.Decrement(ref count);
        }
    }

    /// <summary>Reads an object, rewinding seekable streams first.</summary>
    /// <param name="input">The input. Non-seekable streams are read from their current position.</param>
    /// <param name="type">The target type.</param>
    /// <returns>The object, or null for a null or empty input.</returns>
    /// <exception cref="ArgumentNullException">The target type is null.</exception>
    /// <exception cref="CborContentException">The input is malformed, incompatible, or has trailing data.</exception>
    /// <exception cref="NotSupportedException">The type or nesting depth is unsupported.</exception>
    /// <example><code>var values = (int[])new CborSerializer().Deserialize(stream, typeof(int[]));</code></example>
    public object Deserialize(Stream input, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (input is null)
        {
            return null;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
            if (input is MemoryStream memory && memory.TryGetBuffer(out var segment))
            {
                input.Position = input.Length;
                return segment.Count == 0 ? null : this.DeserializeCore(segment.AsMemory(), type);
            }

            var length = checked((int)input.Length);
            if (length == 0)
            {
                return null;
            }

            var bytes = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                input.ReadExactly(bytes.AsSpan(0, length));
                return this.DeserializeCore(bytes.AsMemory(0, length), type);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        return buffer.Length == 0 ? null : this.DeserializeCore(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), type);
    }

    /// <summary>Reads a typed object, rewinding seekable streams first.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="input">The source stream.</param>
    /// <returns>The object, or the default value for a null or empty input.</returns>
    /// <example><code>var values = new CborSerializer().Deserialize&lt;int[]&gt;(stream);</code></example>
    public T Deserialize<T>(Stream input)
    {
        var value = this.Deserialize(input, typeof(T));
        return value is null ? default : (T)value;
    }

    internal void WriteValue(CborWriter writer, object value, int depth)
    {
        this.CheckDepth(depth);
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        CborTypeMapping.Get(value.GetType()).Write(this, writer, value, depth);
    }

    internal object ReadValue(CborReader reader, Type type, int depth)
    {
        this.CheckDepth(depth);
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            {
                throw new CborContentException($"CBOR null cannot be assigned to {type}.");
            }

            return null;
        }

        return type == typeof(object) ? this.ReadUntyped(reader, depth) : CborTypeMapping.Get(type).Read(this, reader, depth);
    }

    internal void SkipValue(CborReader reader, int depth)
    {
        this.CheckDepth(depth);
        switch (reader.PeekState())
        {
            case CborReaderState.StartArray:
                reader.ReadStartArray();
                while (reader.PeekState() != CborReaderState.EndArray)
                {
                    this.SkipValue(reader, depth + 1);
                }

                reader.ReadEndArray();
                break;
            case CborReaderState.StartMap:
                reader.ReadStartMap();
                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    this.SkipValue(reader, depth + 1);
                    this.SkipValue(reader, depth + 1);
                }

                reader.ReadEndMap();
                break;
            case CborReaderState.Tag:
                reader.ReadTag();
                this.SkipValue(reader, depth + 1);
                break;
            default:
                reader.SkipValue();
                break;
        }
    }

    internal static BigInteger ReadInteger(CborReader reader)
    {
        return reader.PeekState() switch
        {
            CborReaderState.UnsignedInteger => reader.ReadUInt64(),
            CborReaderState.NegativeInteger => -BigInteger.One - reader.ReadCborNegativeIntegerRepresentation(),
            CborReaderState.Tag when reader.PeekTag() is CborTag.UnsignedBigNum or CborTag.NegativeBigNum => reader.ReadBigInteger(),
            _ => throw new CborContentException("Expected a CBOR integer.")
        };
    }

    internal static void WriteInteger(CborWriter writer, BigInteger value)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            writer.WriteInt64((long)value);
        }
        else if (value >= 0 && value <= ulong.MaxValue)
        {
            writer.WriteUInt64((ulong)value);
        }
        else if (value < 0 && value >= -BigInteger.One - ulong.MaxValue)
        {
            writer.WriteCborNegativeIntegerRepresentation((ulong)(-BigInteger.One - value));
        }
        else
        {
            writer.WriteBigInteger(value);
        }
    }

    private object DeserializeCore(ReadOnlyMemory<byte> bytes, Type type)
    {
        var reused = Readers.TryTake(out var cached);
        if (reused)
        {
            Interlocked.Decrement(ref readerCount);
        }

        var reader = reused ? cached : new CborReader(ReadOnlyMemory<byte>.Empty, CborConformanceMode.Strict);
        reader.Reset(bytes);
        try
        {
            var value = this.ReadValue(reader, type, 0);
            if (reader.BytesRemaining != 0)
            {
                throw new CborContentException("Expected a single CBOR value without trailing data.");
            }

            return value;
        }
        finally
        {
            reader.Reset(ReadOnlyMemory<byte>.Empty);
            if (bytes.Length <= 65536)
            {
                if (Interlocked.Increment(ref readerCount) <= 8)
                {
                    Readers.Add(reader);
                }
                else
                {
                    Interlocked.Decrement(ref readerCount);
                }
            }
        }
    }

    private object ReadUntyped(CborReader reader, int depth)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.UnsignedInteger:
                var unsigned = reader.ReadUInt64();
                return unsigned <= long.MaxValue ? (object)(long)unsigned : unsigned;
            case CborReaderState.NegativeInteger:
                var signed = ReadInteger(reader);
                return signed >= long.MinValue ? (object)(long)signed : signed;
            case CborReaderState.ByteString:
            case CborReaderState.StartIndefiniteLengthByteString:
                return reader.ReadByteString();
            case CborReaderState.TextString:
            case CborReaderState.StartIndefiniteLengthTextString:
                return reader.ReadTextString();
            case CborReaderState.Boolean:
                return reader.ReadBoolean();
            case CborReaderState.HalfPrecisionFloat:
            case CborReaderState.SinglePrecisionFloat:
            case CborReaderState.DoublePrecisionFloat:
                return reader.ReadDouble();
            case CborReaderState.StartArray:
                return CborTypeMapping.Get(typeof(List<object>)).Read(this, reader, depth);
            case CborReaderState.StartMap:
                return CborTypeMapping.Get(typeof(Dictionary<object, object>)).Read(this, reader, depth);
            case CborReaderState.Tag:
                return reader.PeekTag() switch
                {
                    CborTag.UnsignedBigNum or CborTag.NegativeBigNum => reader.ReadBigInteger(),
                    CborTag.DecimalFraction => reader.ReadDecimal(),
                    CborTag.DateTimeString => reader.ReadDateTimeOffset(),
                    CborTag.UnixTimeSeconds => reader.ReadUnixTimeSeconds(),
                    (CborTag)37 => CborTypeMapping.Get(typeof(Guid)).Read(this, reader, depth),
                    _ => throw new NotSupportedException($"CBOR tag {reader.PeekTag()} has no supported CLR mapping.")
                };
            default:
                throw new CborContentException($"Unexpected CBOR state {reader.PeekState()}.");
        }
    }

    private void CheckDepth(int depth)
    {
        if (depth >= this.maxDepth)
        {
            throw new NotSupportedException($"CBOR nesting exceeds {this.maxDepth}. Cyclic object graphs are not supported.");
        }
    }

    private sealed class PooledWriter
    {
        public CborWriter Writer { get; } = new(CborConformanceMode.Strict, convertIndefiniteLengthEncodings: true);

        public int MaximumBytesWritten { get; set; }
    }

}