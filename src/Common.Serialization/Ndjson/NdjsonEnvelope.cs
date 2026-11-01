// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>A version-one record kind independent of application naming settings.</summary>
/// <example><code>var kind = NdjsonEnvelopeKind.Data;</code></example>
public enum NdjsonEnvelopeKind
{
    /// <summary>An application data value, including null.</summary>
    /// <example><code>var kind = NdjsonEnvelopeKind.Data;</code></example>
    Data,
    /// <summary>Transport activity without business data.</summary>
    /// <example><code>var kind = NdjsonEnvelopeKind.Heartbeat;</code></example>
    Heartbeat,
    /// <summary>Successful completion with a data-record count.</summary>
    /// <example><code>var kind = NdjsonEnvelopeKind.Completed;</code></example>
    Completed,
    /// <summary>A terminal safe error.</summary>
    /// <example><code>var kind = NdjsonEnvelopeKind.Error;</code></example>
    Error
}

/// <summary>Safe terminal error fields. Remote values are untrusted diagnostic input.</summary>
/// <param name="Code">Stable application or transport code.</param>
/// <param name="Message">Safe wire message without exception details.</param>
/// <param name="TraceId">Optional correlation identifier.</param>
/// <example><code>var error = new NdjsonError("stream_source_failed", "Stream failed.");</code></example>
public sealed record NdjsonError(string Code, string Message, string TraceId = null);

/// <summary>The optional fixed version-one stream envelope.</summary>
/// <typeparam name="T">The application data type.</typeparam>
/// <example><code>var frame = new NdjsonEnvelope&lt;Weather&gt; { Kind = NdjsonEnvelopeKind.Data, Data = weather };</code></example>
[JsonConverter(typeof(NdjsonEnvelopeJsonConverter))]
public sealed record NdjsonEnvelope<T>
{
    /// <summary>The supported wire version.</summary>
    /// <example><code>var version = frame.Version;</code></example>
    public int Version { get; init; } = 1;

    /// <summary>The lifecycle or data kind.</summary>
    /// <example><code>frame = frame with { Kind = NdjsonEnvelopeKind.Heartbeat };</code></example>
    public NdjsonEnvelopeKind Kind { get; init; }

    /// <summary>The application value. A null value is still emitted on data frames.</summary>
    /// <example><code>var value = frame.Data;</code></example>
    public T Data { get; init; }

    /// <summary>The successful terminal count of data frames only.</summary>
    /// <example><code>frame = frame with { RecordCount = 42 };</code></example>
    public long? RecordCount { get; init; }

    /// <summary>The required terminal error details for an error kind.</summary>
    /// <example><code>var code = frame.Error?.Code;</code></example>
    public NdjsonError Error { get; init; }
}
