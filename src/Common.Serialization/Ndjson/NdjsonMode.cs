// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Declares the endpoint wire contract.</summary>
/// <example><code>var mode = NdjsonMode.Raw;</code></example>
public enum NdjsonMode
{
    /// <summary>Application values without injected control frames.</summary>
    /// <example><code>var mode = NdjsonMode.Raw;</code></example>
    Raw,

    /// <summary>Version-one data and lifecycle envelopes.</summary>
    /// <example><code>var mode = NdjsonMode.Envelope;</code></example>
    Envelope
}
