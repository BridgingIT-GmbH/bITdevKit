// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines a provider-infrastructure breaker independent of source dependency resilience.</summary>
/// <example><code>var options = new CacheProviderBreakerOptions { Enabled = true };</code></example>
public sealed record CacheProviderBreakerOptions
{
    /// <summary>Gets whether infrastructure failure breaking is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets consecutive infrastructure failures required to open.</summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>Gets the open cooldown before one nonmutating probe.</summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(10);
}