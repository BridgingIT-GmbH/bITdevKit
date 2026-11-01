// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A safe stream failure without payload-bearing exception details.</summary>
/// <example><code>catch (NdjsonStreamException e) { ShowCode(e.ReasonCode); }</code></example>
public sealed class NdjsonStreamException : IOException
{
    /// <summary>Creates a safe stream failure. Remote text is never used as the message.</summary>
    /// <example><code>throw new NdjsonStreamException("stream_invalid_record");</code></example>
    public NdjsonStreamException(string reasonCode, long? recordNumber = null, string remoteErrorCode = null, string traceId = null)
        : base(reasonCode == "stream_terminal_error" ? "NDJSON stream terminated with an error." : "NDJSON stream failed.")
    {
        this.ReasonCode = reasonCode;
        this.RecordNumber = recordNumber;
        this.RemoteErrorCode = remoteErrorCode;
        this.TraceId = traceId;
    }

    /// <summary>The local stable reason code.</summary>
    /// <example><code>ShowCode(exception.ReasonCode);</code></example>
    public string ReasonCode { get; }

    /// <summary>Optional one-based record position.</summary>
    /// <example><code>var position = exception.RecordNumber;</code></example>
    public long? RecordNumber { get; }

    /// <summary>Optional untrusted remote error code, distinct from the local reason.</summary>
    /// <example><code>var remoteCode = exception.RemoteErrorCode;</code></example>
    public string RemoteErrorCode { get; }

    /// <summary>Optional untrusted remote correlation identifier.</summary>
    /// <example><code>var trace = exception.TraceId;</code></example>
    public string TraceId { get; }
}
