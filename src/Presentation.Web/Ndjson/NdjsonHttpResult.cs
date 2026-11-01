// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>Creates typed streaming HTTP results without owning application retrieval.</summary>
/// <example><code>return NdjsonHttpResult.From(values);</code></example>
public static class NdjsonHttpResult
{
    /// <summary>Creates a result over an asynchronous DTO sequence.</summary>
    /// <example><code>return NdjsonHttpResult.From(values, new() { Mode = NdjsonMode.Envelope });</code></example>
    public static NdjsonHttpResult<T> From<T>(IAsyncEnumerable<T> records, NdjsonEndpointOptions options = null) => new(records, null, options);

    /// <summary>Creates a result over an already bounded synchronous DTO collection.</summary>
    /// <example><code>return NdjsonHttpResult.From(new[] { weather });</code></example>
    public static NdjsonHttpResult<T> From<T>(IEnumerable<T> records, NdjsonEndpointOptions options = null) => new(NdjsonWriter.Adapt(records), null, options);

    /// <summary>Transfers ownership of each yielded encoded frame for exact-byte raw output.</summary>
    /// <example><code>return NdjsonHttpResult.FromEncodedRecords&lt;ExportRecord&gt;(frames);</code></example>
    public static NdjsonHttpResult<TRecord> FromEncodedRecords<TRecord>(IAsyncEnumerable<NdjsonEncodedRecord> records, NdjsonEndpointOptions options = null) => new(null, records, options);
}
