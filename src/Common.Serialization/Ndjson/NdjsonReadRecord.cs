// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A borrowed original frame, valid until the next pull or reader disposal.</summary>
/// <example><code>await foreach (var frame in reader.ReadRecordsAsync(stream)) hash.AppendData(frame.FramedBytes.Span);</code></example>
public sealed class NdjsonReadRecord
{
    private readonly NdjsonEncodedRecord record;
    internal NdjsonReadRecord(NdjsonEncodedRecord record) => this.record = record;

    /// <summary>Original JSON spelling and permitted whitespace.</summary>
    /// <example><code>hash.AppendData(frame.JsonBytes.Span);</code></example>
    public ReadOnlyMemory<byte> JsonBytes => this.record.JsonBytes;

    /// <summary>Original LF, CRLF, or empty compatibility delimiter.</summary>
    /// <example><code>var length = frame.DelimiterBytes.Length;</code></example>
    public ReadOnlyMemory<byte> DelimiterBytes => this.record.DelimiterBytes;

    /// <summary>Original contiguous representation bytes before normalization.</summary>
    /// <example><code>var boundedCopy = frame.FramedBytes.ToArray();</code></example>
    public ReadOnlyMemory<byte> FramedBytes => this.record.FramedBytes;

    internal void Release() => this.record.Dispose();
}
