// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Captures an exact key and dependencies known before a source load.</summary>
/// <example><code>var item = new CacheItem("product:42").WithScalarDimension("catalog", catalogId);</code></example>
public sealed record CacheItem
{
    /// <summary>Creates an immutable item with a defensive metadata snapshot.</summary>
    public CacheItem(string key, IReadOnlyList<CacheDimension> dimensions = null)
    {
        CacheIdentityNormalizer.Key(key);
        this.Key = key;
        this.Dimensions = CacheBoundedSnapshot.Copy(dimensions, 16, nameof(dimensions));
    }

    /// <summary>Gets the caller's original exact key.</summary>
    public string Key { get; }

    /// <summary>Gets dependencies known before source execution.</summary>
    public IReadOnlyList<CacheDimension> Dimensions { get; }

    /// <summary>Returns an item with an additional explicit dimension.</summary>
    public CacheItem WithDimension(CacheDimension dimension) => new(this.Key, [.. this.Dimensions, dimension]);

    /// <summary>Returns an item with an additional typed scalar dimension.</summary>
    public CacheItem WithScalarDimension(string name, object value, CacheDimensionKind? nullKind = null) =>
        this.WithDimension(CacheDimension.Scalar(name, value, nullKind));

    /// <summary>Returns an item with an additional typed set dependency and a defensive member snapshot.</summary>
    /// <typeparam name="T">A supported portable scalar type, including nullable signed integer, Boolean, Guid, or DateTimeOffset types.</typeparam>
    /// <param name="name">The declared set dimension name.</param>
    /// <param name="values">At most 256 members. An empty array retains the kind declared by <typeparamref name="T"/>.</param>
    /// <returns>A new item preserving its key and existing dimensions.</returns>
    /// <remarks>
    /// Supports String, Boolean, signed integer aliases, Guid, and DateTimeOffset. Duplicate members normalize through
    /// the existing dimension factory. Null members retain the declared kind and require the collection schema's null
    /// permission at admission. The item permits at most sixteen dimensions.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">The type, name, members, or dimension count is invalid.</exception>
    /// <example><code>var item = new CacheItem("report:42").WithSetDimension("regions", "west", "east");</code></example>
    public CacheItem WithSetDimension<T>(string name, params T[] values) =>
        this.WithDimension(CacheDimension.Set(name, CacheCollectionBuilder.Kind(typeof(T)),
            values?.Take(257).Select(value => (object)value).ToArray()));

    /// <summary>Returns an item with an additional half-open Integer or UTC Timestamp interval dependency.</summary>
    /// <typeparam name="T">A signed integer alias or DateTimeOffset, including their nullable forms with non-null endpoints.</typeparam>
    /// <param name="name">The declared interval dimension name.</param>
    /// <param name="lower">The inclusive non-null lower endpoint.</param>
    /// <param name="upper">The exclusive non-null upper endpoint, strictly greater than <paramref name="lower"/>.</param>
    /// <returns>A new item preserving its key and existing dimensions.</returns>
    /// <remarks>Endpoints use the existing canonical comparison and snapshot rules. The item permits at most sixteen dimensions.</remarks>
    /// <exception cref="ArgumentException">The type, name, endpoints, or dimension count is invalid.</exception>
    /// <example><code>var item = new CacheItem("report:42").WithIntervalDimension("sequence", 10L, 20L);</code></example>
    public CacheItem WithIntervalDimension<T>(string name, T lower, T upper)
    {
        _ = CacheCollectionBuilder.Kind(typeof(T));
        return this.WithDimension(CacheDimension.Interval(name, lower, upper));
    }
}