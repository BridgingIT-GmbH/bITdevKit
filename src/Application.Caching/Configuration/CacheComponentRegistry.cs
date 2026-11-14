// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;

/// <summary>Stores trusted startup registrations and independent frozen catalog snapshots. Configuration selects stable aliases.</summary>
/// <example><code>var contract = registry.ValueContracts["PRODUCT-CARD"];</code></example>
public sealed class CacheComponentRegistry
{
    private readonly bool frozen;

    /// <summary>Creates mutable trusted registrations for startup composition.</summary>
    /// <example><code>var registry = new CacheComponentRegistry();</code></example>
    public CacheComponentRegistry() { }

    private CacheComponentRegistry(CacheComponentRegistry source, IEnumerable<CacheValueContract> additionalContracts = null)
    {
        this.Contracts = source.Contracts.Values.Concat(additionalContracts ?? [])
            .ToFrozenDictionary(contract => CacheIdentityNormalizer.Name(contract.Alias), StringComparer.OrdinalIgnoreCase);
        this.Kinds = source.Kinds.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.Loaders = source.Loaders.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.WarmupSources = source.WarmupSources.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.Integrations = source.Integrations.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        this.SourceResilience = source.SourceResilience.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.PayloadTransforms = source.PayloadTransforms.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        this.Behaviors = source.Behaviors.ToFrozenDictionary();
        this.frozen = true;
    }

    /// <summary>Gets trusted value contracts; mutation is supported only on startup registries, not frozen snapshots.</summary>
    public readonly IDictionary<string, CacheValueContract> Contracts = new Dictionary<string, CacheValueContract>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets complete provider factories and capabilities approved by host code.</summary>
    public readonly IDictionary<string, CacheProviderKind> Kinds = new Dictionary<string, CacheProviderKind>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets declared refresh-loader aliases and their code-owned factories.</summary>
    public readonly IDictionary<string, CacheLoaderRegistration> Loaders = new Dictionary<string, CacheLoaderRegistration>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets code-owned bounded warm-up source factories by stable alias.</summary>
    public readonly IDictionary<string, Func<IServiceProvider, ICacheWarmupSource>> WarmupSources = new Dictionary<string, Func<IServiceProvider, ICacheWarmupSource>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the integration capabilities explicitly registered by host code.</summary>
    public readonly ISet<string> Integrations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets code-owned dependency resilience factories selected by stable policy alias.</summary>
    public readonly IDictionary<string, Func<IServiceProvider, object>> SourceResilience = new Dictionary<string, Func<IServiceProvider, object>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets registered payload transformation factories, independent of storage provider selection.</summary>
    public readonly IDictionary<string, Func<IServiceProvider, ICachePayloadTransform>> PayloadTransforms = new Dictionary<string, Func<IServiceProvider, ICachePayloadTransform>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets ordered-stage behavior factories keyed by stage and trusted stable alias.</summary>
    public readonly IDictionary<(CacheBehaviorStage Stage, string Alias), Func<IServiceProvider, object>> Behaviors = new Dictionary<(CacheBehaviorStage Stage, string Alias), Func<IServiceProvider, object>>();

    /// <summary>Creates an independent read-only startup snapshot; every exposed registration collection rejects later mutation.</summary>
    /// <returns>The immutable snapshot, or this instance when it is already frozen.</returns>
    /// <example><code>var effective = registrations.Freeze();</code></example>
    public CacheComponentRegistry Freeze() => this.frozen ? this : new(this);

    /// <summary>Creates a frozen snapshot including default contracts inferred from code-declared collections.</summary>
    /// <param name="additionalContracts">Trusted contracts created by typed collection declarations.</param>
    /// <returns>An independent immutable registry snapshot.</returns>
    /// <example><code>var catalogRegistry = registrations.Freeze(defaultContracts);</code></example>
    public CacheComponentRegistry Freeze(IEnumerable<CacheValueContract> additionalContracts)
    {
        ArgumentNullException.ThrowIfNull(additionalContracts);
        return new(this, additionalContracts);
    }

    /// <summary>Gets a defensive view of code-approved value contracts.</summary>
    public IReadOnlyDictionary<string, CacheValueContract> ValueContracts =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, CacheValueContract>(this.Contracts);

    /// <summary>Gets a defensive view of code-approved native provider kinds.</summary>
    public IReadOnlyDictionary<string, CacheProviderKind> ProviderKinds =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, CacheProviderKind>(this.Kinds);
}

/// <summary>Registers one complete native provider factory and truthful capabilities.</summary>
/// <param name="Alias">Trusted stable provider-kind alias.</param>
/// <param name="Capabilities">Capabilities required from every created instance.</param>
/// <param name="Factory">Code-owned factory; connection/root values remain in the supplied options.</param>
/// <param name="CapabilityResolver">Optional trusted profile resolver for final engine/access-mode options.</param>
/// <example><code>var provider = kind.Factory(services, name, options);</code></example>
public sealed record CacheProviderKind(string Alias, CacheProviderCapabilities Capabilities,
    Func<IServiceProvider, string, CacheProviderOptions, ICacheStoreProvider> Factory,
    Func<CacheProviderOptions, CacheProviderCapabilities> CapabilityResolver = null);

/// <summary>Identifies one optional cache-owned behavior stage.</summary>
public enum CacheBehaviorStage
{
    /// <summary>Public collection operation.</summary>
    CollectionOperations,

    /// <summary>Owned repeatable source execution.</summary>
    SourceExecution,

    /// <summary>Binary payload transformation.</summary>
    PayloadTransformation
}

/// <summary>Describes one code-owned typed or binary refresh loader and its optional host readiness dependency.</summary>
/// <example><code>var loader = catalog.Registry.Loaders[&quot;product-source&quot;];</code></example>
public sealed record CacheLoaderRegistration(string Alias, Type ValueType, bool IsBinary, Func<IServiceProvider, object> Factory,
    CacheBackgroundLoadDelegate Background = null, string DatabaseReadyAlias = null);