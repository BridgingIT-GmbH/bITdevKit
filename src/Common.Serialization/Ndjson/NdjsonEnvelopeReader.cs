// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;
using System.Text.Json;

/// <summary>Validates shared envelope lifecycle, counts, and typed terminal failures.</summary>
/// <example><code>await foreach (var item in new NdjsonEnvelopeReader().ReadDataAsync&lt;Weather&gt;(stream)) Process(item);</code></example>
public sealed class NdjsonEnvelopeReader
{
    private readonly NdjsonReader reader;

    /// <summary>Captures a strict framing reader with application JSON settings.</summary>
    /// <example><code>var reader = new NdjsonEnvelopeReader(options: new() { MaxRecordBytes = 65536 });</code></example>
    public NdjsonEnvelopeReader(JsonSerializerOptions jsonOptions = null, NdjsonReaderOptions options = null) => this.reader = new NdjsonReader(jsonOptions, options);

    /// <summary>Yields controls and data. Errors throw on their parsing pull and are never yielded.</summary>
    /// <example><code>await foreach (var frame in reader.ReadAsync&lt;Weather&gt;(stream, ct)) Show(frame.Kind);</code></example>
    public async IAsyncEnumerable<NdjsonEnvelope<T>> ReadAsync<T>(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long count = 0;
        var initial = true;
        await foreach (var frame in this.reader.ReadAsync<NdjsonEnvelope<T>>(source, cancellationToken).ConfigureAwait(false))
        {
            if (frame is null || (initial && frame.Kind != NdjsonEnvelopeKind.Heartbeat))
            {
                throw new NdjsonStreamException("stream_invalid_record");
            }

            initial = false;
            if (frame.Kind == NdjsonEnvelopeKind.Error)
            {
                throw new NdjsonStreamException("stream_terminal_error", remoteErrorCode: frame.Error.Code, traceId: frame.Error.TraceId);
            }

            if (frame.Kind == NdjsonEnvelopeKind.Data)
            {
                count = checked(count + 1);
            }
            else if (frame.Kind == NdjsonEnvelopeKind.Completed && frame.RecordCount != count)
            {
                throw new NdjsonStreamException("stream_invalid_record");
            }

            yield return frame;
            if (frame.Kind == NdjsonEnvelopeKind.Completed)
            {
                yield break;
            }
        }

        throw new NdjsonStreamException("stream_incomplete");
    }

    /// <summary>Yields only data, preserving terminal errors and incomplete output failures.</summary>
    /// <example><code>await foreach (var value in reader.ReadDataAsync&lt;Weather&gt;(stream, ct)) Process(value);</code></example>
    public async IAsyncEnumerable<T> ReadDataAsync<T>(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in this.ReadAsync<T>(source, cancellationToken).ConfigureAwait(false))
        {
            if (frame.Kind == NdjsonEnvelopeKind.Data)
            {
                yield return frame.Data;
            }
        }
    }
}
