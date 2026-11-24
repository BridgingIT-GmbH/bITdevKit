// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Globalization;

/// <summary>Builds opaque keys with named, deterministically ordered, length-prefixed components.</summary>
/// <example><code>var key = new CacheKeyBuilder().With("tenant", tenantId).With("product", 42).Build();</code></example>
public sealed class CacheKeyBuilder
{
    private readonly SortedDictionary<string, string> components = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds one uniquely named invariant component.</summary>
    public CacheKeyBuilder With(string name, object value)
    {
        var canonicalName = CacheIdentityNormalizer.Name(name);
        if (!this.components.TryAdd(canonicalName, CacheIdentityNormalizer.Component(value)))
        {
            throw new ArgumentException("A component with this name is already present.", nameof(name));
        }

        return this;
    }

    /// <summary>Builds the bounded exact key; literal wildcard characters retain their literal meaning.</summary>
    public string Build()
    {
        if (this.components.Count == 0)
        {
            throw new InvalidOperationException("At least one named component is required.");
        }

        var key = string.Concat(this.components.Select(p =>
            p.Key.Length.ToString(CultureInfo.InvariantCulture) + ":" + p.Key +
            p.Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + p.Value));
        CacheIdentityNormalizer.Key(key);
        return key;
    }
}