// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

/// <summary>Configures the independently enabled external cache API and action-specific host authorization.</summary>
/// <example><code>cache.WithEndpoints(options => options.WithReadPolicy("Cache.Read").WithWritePolicy("Cache.Write"));</code></example>
public sealed class CacheEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>Creates safe external defaults; action routes apply the host default authorization policy individually.</summary>
    public CacheEndpointsOptions()
    {
        this.GroupPath = "/api/caches";
        this.GroupTag = "Application Caching";
    }

    /// <summary>Gets or sets the read/metadata/preview override; null selects the host default policy.</summary>
    public string ReadPolicy { get; set; }

    /// <summary>Gets or sets the publication override; null selects the host default policy.</summary>
    public string WritePolicy { get; set; }

    /// <summary>Gets or sets the removal/invalidation/control override; null selects the host default policy.</summary>
    public string InvalidationPolicy { get; set; }

    /// <summary>Gets or sets successful value response policy. Every other response remains no-store.</summary>
    public CacheHttpCachePolicy CachePolicy { get; set; } = CacheHttpCachePolicy.NoStore;

    /// <summary>Gets or sets a finite structured-operation request body limit, independent of collection payload limits.</summary>
    public int MaximumMetadataBodyBytes { get; set; } = 64 * 1024;

    /// <summary>Gets or sets whether the host explicitly exposes unredacted management keys and dimensions.</summary>
    public bool RevealSensitiveMetadata { get; set; }
}

/// <summary>Defines cache-resource response policy without enabling a positive client freshness lifetime.</summary>
public enum CacheHttpCachePolicy
{
    /// <summary>Forbids storing resource responses.</summary>
    NoStore,

    /// <summary>Permits private storage only when every reuse revalidates.</summary>
    Revalidate
}