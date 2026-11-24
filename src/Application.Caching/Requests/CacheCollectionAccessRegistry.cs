// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Builds transport-neutral collection adapters once from trusted code-registered contract types.</summary>
/// <example><code>var access = services.GetRequiredService&lt;CacheCollectionAccessRegistry&gt;().Get("cards");</code></example>
public sealed class CacheCollectionAccessRegistry
{
    private readonly FrozenDictionary<string, ICacheCollectionAccess> collections;

    /// <summary>Creates adapters without performing native I/O or executing sources.</summary>
    public CacheCollectionAccessRegistry(CacheCatalog catalog, IServiceProvider services)
    {
        this.collections = catalog.Collections.Values.ToFrozenDictionary(binding => binding.Name, binding =>
            binding.Contract.IsBinary ? (ICacheCollectionAccess)ActivatorUtilities.CreateInstance<CacheBinaryCollectionAccess>(services, binding) :
            (ICacheCollectionAccess)ActivatorUtilities.CreateInstance(services,
                typeof(CacheTypedCollectionAccess<>).MakeGenericType(binding.Contract.ValueType), binding), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves one declared collection; unknown names cannot create collections or activate arbitrary types.</summary>
    public ICacheCollectionAccess Get(string name) => this.collections.TryGetValue(CacheIdentityNormalizer.Name(name), out var result) ?
        result : throw new ArgumentException("The cache collection is not declared.", nameof(name));
}