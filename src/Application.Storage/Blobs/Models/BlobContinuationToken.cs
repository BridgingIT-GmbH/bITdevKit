// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents the provider-agnostic blob continuation token envelope.
/// </summary>
/// <example>
/// <code>
/// var token = new BlobContinuationToken
/// {
///     Provider = "inmemory",
///     QueryHash = queryHash,
///     Container = "reports",
///     Name = "2026/06/report.pdf"
/// };
/// </code>
/// </example>
public sealed class BlobContinuationToken
{
    /// <summary>
    /// Gets the provider discriminator.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = token.Provider;
    /// </code>
    /// </example>
    public string Provider { get; init; }

    /// <summary>
    /// Gets the token envelope version.
    /// </summary>
    /// <example>
    /// <code>
    /// var version = token.Version;
    /// </code>
    /// </example>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the logical query hash bound to this token.
    /// </summary>
    /// <example>
    /// <code>
    /// var hash = token.QueryHash;
    /// </code>
    /// </example>
    public string QueryHash { get; init; }

    /// <summary>
    /// Gets the token container context.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = token.Container;
    /// </code>
    /// </example>
    public string Container { get; init; }

    /// <summary>
    /// Gets the last returned blob name for keyset-based paging.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = token.Name;
    /// </code>
    /// </example>
    public string Name { get; init; }

    /// <summary>
    /// Gets provider-native continuation state.
    /// </summary>
    /// <example>
    /// <code>
    /// var nativeToken = token.NativeToken;
    /// </code>
    /// </example>
    public string NativeToken { get; init; }

    /// <summary>
    /// Gets optional provider-specific continuation metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var shard = token.Properties?["shard"];
    /// </code>
    /// </example>
    public Dictionary<string, string> Properties { get; init; }
}
