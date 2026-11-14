// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Retains explicit source absence only when enabled.</summary>
/// <example><code>var options = new CacheNegativeCachingOptions();</code></example>
public sealed record CacheNegativeCachingOptions
{
    /// <summary>Gets whether to retain metadata-only source-absence markers.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Gets marker lifetime bounded by the effective freshness policy.</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromSeconds(30);
}