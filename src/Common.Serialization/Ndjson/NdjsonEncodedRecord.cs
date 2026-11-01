// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>An owned, bounded encoded frame. Dispose after its awaited write.</summary>
/// <example><code>using var frame = await writer.EncodeRecordAsync(value); await writer.WriteEncodedRecordAsync(frame, stream);</code></example>
public sealed class NdjsonEncodedRecord : IDisposable
{
    private readonly NdjsonRecordBuffer buffer;
    private readonly int jsonLength;
    private readonly int delimiterLength;

    internal NdjsonEncodedRecord(NdjsonRecordBuffer buffer, int jsonLength, int delimiterLength)
    {
        this.buffer = buffer;
        this.jsonLength = jsonLength;
        this.delimiterLength = delimiterLength;
    }

    /// <summary>The original JSON bytes. Captured views must not outlive this lease.</summary>
    /// <example><code>hash.AppendData(frame.JsonBytes.Span);</code></example>
    public ReadOnlyMemory<byte> JsonBytes => this.buffer.Memory[..this.jsonLength];

    /// <summary>The actual line delimiter, normally LF for encoded records.</summary>
    /// <example><code>var delimiterLength = frame.DelimiterBytes.Length;</code></example>
    public ReadOnlyMemory<byte> DelimiterBytes => this.buffer.Memory.Slice(this.jsonLength, this.delimiterLength);

    /// <summary>Contiguous JSON and delimiter bytes without another encoding.</summary>
    /// <example><code>var byteBudget = frame.FramedBytes.Length;</code></example>
    public ReadOnlyMemory<byte> FramedBytes => this.buffer.Memory[..(this.jsonLength + this.delimiterLength)];

    /// <summary>Idempotently releases pooled storage. Previously captured memory cannot be revoked.</summary>
    /// <example><code>frame.Dispose();</code></example>
    public void Dispose() => this.buffer.Dispose();
}
