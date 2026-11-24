// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Ranks only a caller-supplied bounded candidate set, without sorting the complete cache or forcing collection.</summary>
/// <example><code>var victims = CacheEvictionPolicy.Select(candidates, timeProvider.GetUtcNow(), 16);</code></example>
public static class CacheEvictionPolicy
{
    /// <summary>Prefers invalid/expired entries, then lower priority and approximate older activity.</summary>
    public static IReadOnlyList<CacheEntryMetadata> Select(IReadOnlyList<CacheEntryMetadata> candidates, DateTimeOffset now, int count)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count > 256 || count is < 0 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(candidates), "Eviction ranks at most 256 admitted metadata candidates.");
        }

        return Array.AsReadOnly(candidates.OrderBy(m => m.ExpiresAt <= now || m.AbsoluteExpiresAt <= now ? 0 : 1)
            .ThenBy(m => m.Priority).ThenBy(m => m.LastAccessAt).ThenBy(m => m.PublishedAt).Take(count).ToArray());
    }
}