// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Separates logical freshness from physical reclamation.</summary>
/// <example><code>var options = new CacheExpirationOptions();</code></example>
public sealed record CacheExpirationOptions
{
    /// <summary>Gets finite TTL measured from confirmed publication.</summary>
    public TimeSpan AbsoluteTtl { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets whether authoritative sliding renewal is enabled.</summary>
    public bool SlidingEnabled { get; init; } = false;

    /// <summary>Gets idle expiry when sliding is enabled.</summary>
    public TimeSpan SlidingIdleTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets hard publication age cap for sliding entries.</summary>
    public TimeSpan AbsoluteCap { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Gets physical purge grace after logical expiration.</summary>
    public TimeSpan RetentionGrace { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets one-time downward lifetime jitter, at least zero and below one.</summary>
    public double JitterRatio { get; init; } = 0;
}