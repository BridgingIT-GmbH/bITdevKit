// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records publication-relative freshness independently of physical cleanup.</summary>
/// <example><code>expiration.Absolute(TimeSpan.FromMinutes(5)).Jitter(0.1);</code></example>
public sealed class CacheExpirationBuilder : PatchOptionsBuilder<CacheExpirationOptions, CacheExpirationBuilder>
{
    /// <summary>Selects fixed expiration and disables sliding renewal.</summary>
    /// <param name="ttl">The finite publication-relative lifetime.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>expiration.Absolute(TimeSpan.FromMinutes(5));</code></example>
    public CacheExpirationBuilder Absolute(TimeSpan ttl) => this.With(options => options.AbsoluteTtl, ttl).With(options => options.SlidingEnabled, false);

    /// <summary>Enables authoritative sliding expiration within a finite publication age cap.</summary>
    /// <param name="idle">The idle interval renewed by valid foreground access.</param>
    /// <param name="absoluteCap">The publication age beyond which renewal is impossible.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>expiration.Sliding(TimeSpan.FromMinutes(2), TimeSpan.FromHours(1));</code></example>
    public CacheExpirationBuilder Sliding(TimeSpan idle, TimeSpan absoluteCap) => this.With(options => options.SlidingEnabled, true)
        .With(options => options.SlidingIdleTimeout, idle).With(options => options.AbsoluteCap, absoluteCap);

    /// <summary>Sets one-time downward lifetime jitter without resampling on access.</summary>
    /// <param name="ratio">A finite fraction at least zero and below one.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>expiration.Jitter(0.1);</code></example>
    public CacheExpirationBuilder Jitter(double ratio) => this.With(options => options.JitterRatio, ratio);

    /// <summary>Sets physical purge grace after logical expiration.</summary>
    /// <param name="grace">The nonnegative cleanup grace, validated at finalization.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>expiration.RetentionGrace(TimeSpan.FromMinutes(5));</code></example>
    public CacheExpirationBuilder RetentionGrace(TimeSpan grace) => this.With(options => options.RetentionGrace, grace);
}