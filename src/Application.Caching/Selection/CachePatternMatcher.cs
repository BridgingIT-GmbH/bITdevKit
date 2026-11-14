// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Applies cache pattern bounds before matching through the shared whole-text glob engine.</summary>
/// <example><code>var matches = new CachePatternMatcher(@"product:\?*").IsMatch("product:?42");</code></example>
public sealed class CachePatternMatcher
{
    private readonly GlobPatternMatcher matcher;

    /// <summary>Validates a bounded nonempty cache pattern and parses its supported escapes.</summary>
    /// <param name="pattern">The cache-owned key pattern.</param>
    /// <example><code>var matcher = new CachePatternMatcher("product:*");</code></example>
    public CachePatternMatcher(string pattern)
    {
        CacheIdentityNormalizer.Key(pattern);
        this.matcher = new(pattern);
    }

    /// <summary>Matches the complete key with invariant case-insensitive literals and Unicode-scalar wildcards.</summary>
    /// <param name="key">The complete key text to match.</param>
    /// <returns>Whether the complete key satisfies this pattern.</returns>
    /// <example><code>var matches = matcher.IsMatch("PRODUCT:42");</code></example>
    public bool IsMatch(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return this.matcher.IsMatch(key);
    }
}