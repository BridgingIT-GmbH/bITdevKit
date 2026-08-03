// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents the provider-agnostic continuation token envelope.
/// </summary>
public sealed class DocumentContinuationToken
{
    /// <summary>
    /// Gets or sets the provider identifier.
    /// </summary>
    public string Provider { get; init; }

    /// <summary>Gets or sets the normalized named-client identity.</summary>
    public string ClientName { get; init; }

    /// <summary>Gets or sets the stable document type identity.</summary>
    public string DocumentType { get; init; }

    /// <summary>Gets or sets the page operation represented by the token.</summary>
    public string Operation { get; init; }

    /// <summary>
    /// Gets or sets the token envelope version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets or sets the logical query hash.
    /// </summary>
    public string QueryHash { get; init; }

    /// <summary>
    /// Gets or sets the fixed visibility cutoff used by every page in this sequence.
    /// </summary>
    public DateTimeOffset VisibilityCutoff { get; init; }

    /// <summary>
    /// Gets or sets the provider-native continuation state.
    /// </summary>
    public string NativeToken { get; init; }
}
