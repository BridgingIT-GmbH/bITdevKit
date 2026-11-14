// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves declared typed/binary collection handles from one immutable startup catalog.</summary>
/// <example><code>var cards = cache.GetCollection&lt;ProductCard&gt;("product-cards");</code></example>
public sealed class ApplicationCache : IApplicationCache
{
    private readonly FrozenDictionary<string, object> handles;

    /// <summary>Builds each collection engine/behavior chain once without source execution or provider I/O.</summary>
    public ApplicationCache(CacheCatalog catalog, ICacheCollectionFactory factory, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factory);
        var providers = services.GetRequiredService<CacheProviderRegistry>();
        this.handles = catalog.Collections.Values.ToFrozenDictionary(c => c.Name,
            c => c.Contract.CreateHandle(factory, c, new CacheDeferredStoreProvider(c.Provider, () => providers.Get(c.Provider.Name))), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ICacheCollection<T> GetCollection<T>(string name)
    {
        if (!this.handles.TryGetValue(CacheIdentityNormalizer.Name(name), out var handle))
        {
            throw new ArgumentException("The cache collection was not declared at startup.", nameof(name));
        }

        return handle as ICacheCollection<T> ??
            throw new ArgumentException("The requested typed handle does not match the registered value contract.", nameof(name));
    }

    /// <inheritdoc />
    public ICacheBinaryCollection GetBinaryCollection(string name)
    {
        if (!this.handles.TryGetValue(CacheIdentityNormalizer.Name(name), out var handle))
        {
            throw new ArgumentException("The cache collection was not declared at startup.", nameof(name));
        }

        return handle as ICacheBinaryCollection ??
            throw new ArgumentException("The requested collection is not a declared raw binary contract.", nameof(name));
    }
}