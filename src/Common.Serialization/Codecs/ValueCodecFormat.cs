// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Identifies a persisted representation independently of application contract versions.</summary>
/// <param name="Id">The stable format identifier.</param>
/// <param name="Version">The positive format version.</param>
/// <example><code>var format = new ValueCodecFormat("json", 1);</code></example>
public readonly record struct ValueCodecFormat(string Id, int Version);

/// <summary>Reports an encoding, decoding, or codec capability failure without exposing payload content.</summary>
/// <param name="code">The stable failure classification.</param>
/// <param name="message">A non-sensitive explanation.</param>
/// <example><code>var error = new ValueCodecError("InvalidPayload", "Input is not valid for the declared format.");</code></example>
public sealed class ValueCodecError(string code, string message) : ResultErrorBase(message)
{
    /// <summary>Gets the stable error classification.</summary>
    public string Code { get; } = code;
}