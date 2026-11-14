// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes source-produced metadata and collection-bounded overrides for a new value.</summary>
/// <example><code>var value = new CacheEntryDescriptor();</code></example>
public sealed record CacheEntryDescriptor
{
    /// <summary>Gets additional dependencies. The engine snapshots them before publication.</summary>
    public IReadOnlyList<CacheDimension> Dimensions { get; init; } = [];

    /// <summary>Gets an optional absolute lifetime, bounded by collection policy.</summary>
    public TimeSpan? AbsoluteTtl { get; init; }

    /// <summary>Gets an optional sliding idle timeout within the collection's hard lifetime.</summary>
    public TimeSpan? SlidingIdleTimeout { get; init; }

    /// <summary>Gets an explicit conservative decoded graph estimate when known.</summary>
    public long? DecodedSizeBytes { get; init; }

    /// <summary>Gets a diagnostic domain item count, independent of cache-entry count.</summary>
    public long? ItemCount { get; init; }

    /// <summary>Gets retention priority, which never bypasses admission limits.</summary>
    public CacheEvictionPriority Priority { get; init; } = CacheEvictionPriority.Normal;

    /// <summary>Gets whether this typed source outcome must remain unretained.</summary>
    public bool SuppressStorage { get; init; }

    /// <summary>Gets an optional logical content type for binary/HTTP reads.</summary>
    public string ContentType { get; init; }

    /// <summary>Returns a descriptor carrying an additional source-discovered dependency.</summary>
    public CacheEntryDescriptor WithDimension(CacheDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);
        if (this.Dimensions is null || this.Dimensions.Count >= 16)
        {
            throw new ArgumentException("An entry permits at most sixteen dimensions.", nameof(dimension));
        }

        return this with { Dimensions = Array.AsReadOnly<CacheDimension>([.. this.Dimensions, dimension]) };
    }

    /// <summary>Returns a descriptor with an additional typed set dependency and a defensive member snapshot.</summary>
    /// <typeparam name="T">A supported portable scalar type, including nullable signed integer, Boolean, Guid, or DateTimeOffset types.</typeparam>
    /// <param name="name">The declared set dimension name.</param>
    /// <param name="values">At most 256 members. An empty array retains the kind declared by <typeparamref name="T"/>.</param>
    /// <returns>A new descriptor preserving existing dimensions and entry overrides.</returns>
    /// <remarks>
    /// Supports String, Boolean, signed integer aliases, Guid, and DateTimeOffset. Duplicate members normalize through
    /// the existing dimension factory. Null members retain the declared kind and require the collection schema's null
    /// permission at admission. The descriptor permits at most sixteen dimensions.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">The type, name, members, or dimension count is invalid.</exception>
    /// <example><code>var descriptor = new CacheEntryDescriptor().WithSetDimension("regions", "west", "east");</code></example>
    public CacheEntryDescriptor WithSetDimension<T>(string name, params T[] values) =>
        this.WithDimension(CacheDimension.Set(name, CacheCollectionBuilder.Kind(typeof(T)),
            values?.Take(257).Select(value => (object)value).ToArray()));

    /// <summary>Returns a descriptor with an additional half-open Integer or UTC Timestamp interval dependency.</summary>
    /// <typeparam name="T">A signed integer alias or DateTimeOffset, including their nullable forms with non-null endpoints.</typeparam>
    /// <param name="name">The declared interval dimension name.</param>
    /// <param name="lower">The inclusive non-null lower endpoint.</param>
    /// <param name="upper">The exclusive non-null upper endpoint, strictly greater than <paramref name="lower"/>.</param>
    /// <returns>A new descriptor preserving existing dimensions and entry overrides.</returns>
    /// <remarks>Endpoints use the existing canonical comparison and snapshot rules. The descriptor permits at most sixteen dimensions.</remarks>
    /// <exception cref="ArgumentException">The type, name, endpoints, or dimension count is invalid.</exception>
    /// <example><code>var descriptor = new CacheEntryDescriptor().WithIntervalDimension("sequence", 10L, 20L);</code></example>
    public CacheEntryDescriptor WithIntervalDimension<T>(string name, T lower, T upper)
    {
        _ = CacheCollectionBuilder.Kind(typeof(T));
        return this.WithDimension(CacheDimension.Interval(name, lower, upper));
    }
}

/// <summary>Controls relative retention preference without granting infinite retention or bypassing limits.</summary>
public enum CacheEvictionPriority
{
    /// <summary>Reclaim first when otherwise eligible; the default for absence markers.</summary>
    Low,

    /// <summary>Normal retention preference.</summary>
    Normal,

    /// <summary>Prefer retention over otherwise comparable lower-priority entries.</summary>
    High
}