// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

/// <summary>Records external cache endpoint settings before the startup configuration overlay is applied.</summary>
/// <example><code>cache.WithEndpoints(options => options.WithPath("/api/resource-cache"));</code></example>
public sealed class CacheEndpointsOptionsBuilder
{
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) { ["Enabled"] = "true" };

    /// <summary>Sets the external group prefix.</summary>
    public CacheEndpointsOptionsBuilder WithPath(string path) => this.Set("GroupPath", path);

    /// <summary>Replaces default host authorization for read actions only.</summary>
    public CacheEndpointsOptionsBuilder WithReadPolicy(string policy) => this.Set("ReadPolicy", policy);

    /// <summary>Replaces default host authorization for publication only.</summary>
    public CacheEndpointsOptionsBuilder WithWritePolicy(string policy) => this.Set("WritePolicy", policy);

    /// <summary>Replaces default host authorization for invalidation/control only.</summary>
    public CacheEndpointsOptionsBuilder WithInvalidationPolicy(string policy) => this.Set("InvalidationPolicy", policy);

    /// <summary>Selects NoStore or private mandatory revalidation.</summary>
    public CacheEndpointsOptionsBuilder WithCachePolicy(CacheHttpCachePolicy policy) => this.Set("CachePolicy", policy.ToString());

    /// <summary>Sets the bounded structured operation request limit.</summary>
    public CacheEndpointsOptionsBuilder WithMaximumMetadataBodyBytes(int maximum) => this.Set("MaximumMetadataBodyBytes", maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Explicitly permits sensitive management metadata after normal host read authorization.</summary>
    public CacheEndpointsOptionsBuilder WithSensitiveMetadata(bool enabled = true) => this.Set("RevealSensitiveMetadata", enabled.ToString());

    /// <summary>Disables mapping while preserving available presentation configuration capability.</summary>
    public CacheEndpointsOptionsBuilder WithEnabled(bool enabled) => this.Set("Enabled", enabled.ToString());

    /// <summary>Gets a defensive snapshot of explicitly supplied fields.</summary>
    public IReadOnlyDictionary<string, string> Build() => new Dictionary<string, string>(this.values, StringComparer.OrdinalIgnoreCase);

    private CacheEndpointsOptionsBuilder Set(string name, string value)
    {
        this.values[name] = value;
        return this;
    }
}