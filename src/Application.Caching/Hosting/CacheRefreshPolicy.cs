// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines portable refresh eligibility and finite version-scoped scheduler backoff.</summary>
/// <example><code>if (CacheRefreshPolicy.IsEligible(metadata, query)) { Schedule(metadata); }</code></example>
public static class CacheRefreshPolicy
{
    /// <summary>Requires a current-contract positive version that remains valid, recently active, due, and outside scheduler backoff.</summary>
    /// <param name="metadata">The provider's authoritative version metadata.</param>
    /// <param name="query">The bounded query policy and fixed observation time.</param>
    /// <returns>True when this version can be considered for a conditional background refresh.</returns>
    public static bool IsEligible(CacheEntryMetadata metadata, CacheRefreshQuery query)
    {
        if (metadata is null || metadata.IsNotFound || metadata.Identity.ContractVersion != CacheIdentityNormalizer.ContractVersion(query.ContractVersion) ||
            metadata.ExpiresAt <= query.Now || metadata.AbsoluteExpiresAt <= query.Now || !metadata.HasForegroundAccess ||
            metadata.LastAccessAt < query.Now - query.ActivityWindow || metadata.RefreshNotBefore > query.Now)
        {
            return false;
        }

        var lifetime = metadata.AbsoluteExpiresAt - metadata.PublishedAt;
        var lead = TimeSpan.FromTicks(Math.Min(query.RefreshAhead.Ticks, Math.Max(0, lifetime.Ticks / 5)));
        return metadata.AbsoluteExpiresAt - query.Now <= lead;
    }

    /// <summary>Computes bounded retry delay, pausing a repeatedly failing version after its configured failure threshold.</summary>
    /// <param name="options">The finalized collection refresh policy.</param>
    /// <param name="failures">The positive consecutive failure count for this exact version.</param>
    /// <param name="now">The source-attempt completion time.</param>
    /// <returns>The earliest next automatic attempt, with bounded downward jitter on ordinary backoff.</returns>
    public static DateTimeOffset NextAttempt(CacheRefreshOptions options, int failures, DateTimeOffset now)
    {
        if (failures >= options.FailuresBeforePause)
        {
            return now + options.FailurePause;
        }

        var factor = 1L << Math.Min(20, Math.Max(0, failures - 1));
        var ticks = Math.Min(options.MaximumFailureBackoff.Ticks, options.FailureBackoff.Ticks * (double)factor);
        return now + TimeSpan.FromTicks((long)(ticks * (0.9 + Random.Shared.NextDouble() * 0.1)));
    }
}