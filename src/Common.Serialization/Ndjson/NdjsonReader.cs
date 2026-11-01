// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;

/// <summary>Asynchronously frames bounded UTF-8 NDJSON without owning its input stream.</summary>
/// <example><code>await foreach (var value in new NdjsonReader().ReadAsync&lt;Weather&gt;(stream)) Process(value);</code></example>
public sealed class NdjsonReader
{
    private readonly JsonSerializerOptions json;
    private readonly NdjsonReaderOptions options;

    /// <summary>Captures JSON settings and validated framing options.</summary>
    /// <example><code>var reader = new NdjsonReader(options: new() { MaxRecordBytes = 65536 });</code></example>
    public NdjsonReader(JsonSerializerOptions jsonOptions = null, NdjsonReaderOptions options = null)
    {
        this.json = new JsonSerializerOptions(jsonOptions ?? DefaultJsonSerializerOptions.Create());
        this.json.Converters.Insert(0, new NdjsonEnvelopeJsonConverter());
        this.options = (options ?? new NdjsonReaderOptions()).Capture();
    }

    /// <summary>Yields independent typed values, releasing each frame before yielding.</summary>
    /// <example><code>await foreach (var item in reader.ReadAsync&lt;Weather&gt;(stream, ct)) Process(item);</code></example>
    public async IAsyncEnumerable<T> ReadAsync<T>(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var record in this.ReadRecordsAsync(source, cancellationToken).ConfigureAwait(false))
        {
            T value;
            try
            {
                value = this.Deserialize<T>(record);
            }
            finally
            {
                record.Release();
            }

            yield return value;
        }
    }

    /// <summary>Deserializes the current borrowed frame without advancing or altering it.</summary>
    /// <example><code>var value = reader.Deserialize&lt;Weather&gt;(frame);</code></example>
    public T Deserialize<T>(NdjsonReadRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var bytes = record.JsonBytes;
        try
        {
            return JsonSerializer.Deserialize<T>(bytes.Span, this.json);
        }
        catch (NdjsonStreamException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new NdjsonStreamException("stream_invalid_record");
        }
    }

    /// <summary>Yields validated borrowed frames. Advance invalidates the previous lease.</summary>
    /// <example><code>await foreach (var frame in reader.ReadRecordsAsync(stream, ct)) Hash(frame.FramedBytes);</code></example>
    public async IAsyncEnumerable<NdjsonReadRecord> ReadRecordsAsync(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var chunk = ArrayPool<byte>.Shared.Rent(4096);
        NdjsonRecordBuffer line = null;
        try
        {
            int count;
            while ((count = await source.ReadAsync(chunk.AsMemory(0, 4096), cancellationToken).ConfigureAwait(false)) != 0)
            {
                var offset = 0;
                while (offset < count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    line ??= new NdjsonRecordBuffer(this.options.MaxRecordBytes);
                    var index = chunk.AsSpan(offset, count - offset).IndexOf((byte)'\n');
                    var take = index < 0 ? count - offset : index;
                    line.AppendReceived(chunk.AsSpan(offset, take));
                    offset += take;
                    if (index < 0)
                    {
                        if (line.Length > this.options.MaxRecordBytes && (line.Length != this.options.MaxRecordBytes + 1 || line.Memory.Span[^1] != (byte)'\r'))
                        {
                            throw new NdjsonStreamException("stream_record_too_large");
                        }

                        continue;
                    }

                    offset++;
                    var delimiterLength = line.Length > 0 && line.Memory.Span[^1] == (byte)'\r' ? 2 : 1;
                    var jsonLength = line.Length - (delimiterLength - 1);
                    if (jsonLength == 0 && this.options.SkipEmptyLines)
                    {
                        line.Dispose();
                        line = null;
                        continue;
                    }

                    if (jsonLength > this.options.MaxRecordBytes)
                    {
                        throw new NdjsonStreamException("stream_record_too_large");
                    }

                    NdjsonRecordValidation.Validate(line.Memory.Span[..jsonLength]);
                    line.AppendReceived("\n"u8);
                    using (var owned = new NdjsonEncodedRecord(line, jsonLength, delimiterLength))
                    {
                        line = null;
                        yield return new NdjsonReadRecord(owned);
                    }
                }
            }

            if (line is { Length: > 0 })
            {
                if (!this.options.AllowUnterminatedFinalRecord)
                {
                    throw new NdjsonStreamException("stream_invalid_record");
                }

                if (line.Length > this.options.MaxRecordBytes)
                {
                    throw new NdjsonStreamException("stream_record_too_large");
                }

                NdjsonRecordValidation.Validate(line.Memory.Span);
                using var owned = new NdjsonEncodedRecord(line, line.Length, 0);
                line = null;
                yield return new NdjsonReadRecord(owned);
            }
        }
        finally
        {
            line?.Dispose();
            ArrayPool<byte>.Shared.Return(chunk, clearArray: true);
        }
    }
}
