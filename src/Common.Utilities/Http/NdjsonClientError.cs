// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>A safe recoverable response-opening error without response bodies or credentials.</summary>
/// <example><code>var error = result.Errors.OfType&lt;NdjsonClientError&gt;().First();</code></example>
public sealed class NdjsonClientError : ResultErrorBase
{
    /// <summary>Creates a safe initial failure.</summary>
    /// <example><code>var error = new NdjsonClientError("stream_invalid_record", 400);</code></example>
    public NdjsonClientError(string reasonCode, int? statusCode = null) : base("NDJSON response could not be opened.")
    {
        this.ReasonCode = reasonCode;
        this.StatusCode = statusCode;
    }

    /// <summary>The stable local error code.</summary>
    /// <example><code>ShowCode(error.ReasonCode);</code></example>
    public string ReasonCode { get; }

    /// <summary>The HTTP status when a response was received.</summary>
    /// <example><code>var status = error.StatusCode;</code></example>
    public int? StatusCode { get; }
}
