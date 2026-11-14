// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines collection policies, independently of its registered immutable value contract.</summary>
/// <example><code>var options = new CacheCollectionOptions();</code></example>
public sealed record CacheCollectionOptions
{
    /// <summary>Gets whether provider access and retention are enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets registered stable value-contract alias.</summary>
    public string Contract { get; init; } = null;

    /// <summary>Gets stable logical contract version included in identity.</summary>
    public string ContractVersion { get; init; } = "1";

    /// <summary>Gets named complete store provider.</summary>
    public string Provider { get; init; } = "memory";

    /// <summary>Gets registered codec profile, unused for declared raw binary contracts.</summary>
    public string Codec { get; init; } = "json";

    /// <summary>Gets declared dimension kinds, shapes, and explicit-null policies.</summary>
    public IReadOnlyList<CacheDimensionSchema> Dimensions { get; init; } = [];

    /// <summary>Gets logical freshness and physical retention policies.</summary>
    public CacheExpirationOptions Expiration { get; init; } = new();

    /// <summary>Gets finite collection resource and metadata ceilings.</summary>
    public CacheCollectionLimits Limits { get; init; } = new();

    /// <summary>Gets source strategy, coordination, and bounded wait/deadline policies.</summary>
    public CacheLoadingOptions Loading { get; init; } = new();

    /// <summary>Gets default-off strictly validated local retained representations.</summary>
    public CacheHotEntryOptions HotEntries { get; init; } = new();

    /// <summary>Gets explicit default-off bounded compression.</summary>
    public CacheCompressionOptions Compression { get; init; } = new();

    /// <summary>Gets default-off retention of confirmed source absence.</summary>
    public CacheNegativeCachingOptions NegativeCaching { get; init; } = new();

    /// <summary>Gets default-off refresh-ahead through a registered trusted source alias.</summary>
    public CacheRefreshOptions Refresh { get; init; } = new();

    /// <summary>Gets default-off bounded predefined warmup selection.</summary>
    public CacheWarmupOptions Warmup { get; init; } = new();

    /// <summary>Gets ordered optional behavior aliases per stage.</summary>
    public CacheBehaviorOptions Behaviors { get; init; } = new();

    /// <summary>Gets occupied stream lifetime, idle, and transfer buffer limits.</summary>
    public CacheStreamOptions Streaming { get; init; } = new();

    /// <summary>Gets whether bounded pressure reclaim may evict eligible entries.</summary>
    public CacheEvictionMode Eviction { get; init; } = CacheEvictionMode.Evict;

    /// <summary>Gets neutral optional integration settings registered by the presentation package.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Extensions { get; init; } = new Dictionary<string, IReadOnlyDictionary<string, string>>();
}