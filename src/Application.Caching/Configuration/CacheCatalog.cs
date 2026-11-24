// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;

/// <summary>Contains the validated immutable startup collection/provider catalog for one advanced-cache host lifetime.</summary>
/// <example><code>var policy = catalog.Collections["PRODUCT-CARDS"].Options;</code></example>
public sealed class CacheCatalog
{
    /// <summary>Freezes finalized options, declared collection/provider bindings, trusted component registrations and provenance into one startup catalog.</summary>
    public CacheCatalog(ApplicationCachingOptions options, IEnumerable<CacheCollectionBinding> collections,
        IEnumerable<CacheProviderBinding> providers, CacheComponentRegistry registry,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> integrations)
    {
        ArgumentNullException.ThrowIfNull(registry);
        this.Options = options;
        this.Collections = collections.ToFrozenDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        this.Providers = providers.ToFrozenDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        this.Registry = registry.Freeze();
        this.Integrations = integrations;
    }

    /// <summary>Gets finalized global options. No live reload is subscribed.</summary>
    public ApplicationCachingOptions Options { get; }

    /// <summary>Gets finalized named collection bindings.</summary>
    public IReadOnlyDictionary<string, CacheCollectionBinding> Collections { get; }

    /// <summary>Gets finalized named complete provider bindings.</summary>
    public IReadOnlyDictionary<string, CacheProviderBinding> Providers { get; }

    /// <summary>Gets neutral optional integration settings supplied by registered presentation capabilities.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Integrations { get; }

    /// <summary>Gets the independent frozen trusted registrations selected for this host lifetime; mutation is rejected.</summary>
    public CacheComponentRegistry Registry { get; }
}

/// <summary>Describes one validated collection binding, without performing provider I/O.</summary>
/// <param name="Name">Canonical registered name.</param>
/// <param name="Options">Immutable effective collection settings.</param>
/// <param name="Contract">Trusted typed or binary value contract.</param>
/// <param name="Provider">The one complete named provider.</param>
/// <param name="Provenance">Explicit setting sources, never secret values.</param>
/// <example><code>var providerName = binding.Provider.Name;</code></example>
public sealed record CacheCollectionBinding(string Name, CacheCollectionOptions Options, CacheValueContract Contract,
    CacheProviderBinding Provider, IReadOnlyDictionary<string, string> Provenance);

/// <summary>Describes one complete named native provider and excludes connection secrets from public policy inspection.</summary>
/// <example><code>var capabilities = binding.Capabilities;</code></example>
public sealed class CacheProviderBinding
{
    /// <summary>Binds a normalized provider alias to its finalized native options and truthful profile capabilities.</summary>
    public CacheProviderBinding(string name, CacheProviderOptions options, CacheProviderKind kind)
    {
        this.Name = name;
        this.NativeOptions = options;
        this.Kind = kind;
        this.Capabilities = kind.CapabilityResolver?.Invoke(options) ?? kind.Capabilities ??
            throw new ArgumentException("A native provider profile did not declare its final capabilities.");
        this.Options = options with { ConnectionString = options.ConnectionString is null ? null : "[redacted]" };
    }

    /// <summary>Gets the canonical registered provider name.</summary>
    public string Name { get; }

    /// <summary>Gets redacted immutable native policy settings.</summary>
    public CacheProviderOptions Options { get; }

    /// <summary>Gets the trusted provider factory/capabilities registration.</summary>
    public CacheProviderKind Kind { get; }

    /// <summary>Gets capabilities resolved from this binding's final engine/access/durability profile.</summary>
    public CacheProviderCapabilities Capabilities { get; }

    /// <summary>Gets finalized native connection and root settings for trusted provider construction; do not expose these settings to management clients.</summary>
    public CacheProviderOptions NativeOptions { get; }
}