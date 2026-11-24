// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Builds collection-specific behavior chains from trusted startup registrations and validated effective options.</summary>
/// <param name="catalog">The finalized catalog that owns registered behavior factories.</param>
/// <param name="services">The service provider used to resolve registered behavior dependencies.</param>
/// <example><code>var operations = new CacheBehaviorFactory(catalog, services).Operations(binding);</code></example>
public sealed class CacheBehaviorFactory(CacheCatalog catalog, IServiceProvider services)
{
    /// <summary>Resolves compatible operation behaviors in the collection's declared order.</summary>
    /// <param name="binding">The validated collection whose selected aliases are resolved.</param>
    /// <returns>A read-only ordered behavior list; an empty list requires no wrapper.</returns>
    /// <exception cref="ArgumentException">A registered behavior is incompatible with the declared contract.</exception>
    public IReadOnlyList<ICacheCollectionBehavior> Operations(CacheCollectionBinding binding)
    {
        var result = new List<ICacheCollectionBehavior>();
        foreach (var alias in binding.Options.Behaviors.CollectionOperations)
        {
            var instance = catalog.Registry.Behaviors[(CacheBehaviorStage.CollectionOperations, CacheIdentityNormalizer.Name(alias))](services);
            if (instance is not ICacheCollectionBehavior behavior || !behavior.Supports(binding.Contract.ValueType, binding.Contract.IsBinary))
            {
                throw new ArgumentException("A registered collection behavior is incompatible with its declared collection contract.");
            }

            result.Add(behavior);
        }

        return result.AsReadOnly();
    }

    /// <summary>Builds the selected payload chain, appending compression when enabled and not already selected.</summary>
    /// <param name="binding">The validated collection whose payload settings are resolved.</param>
    /// <returns>The composed pipeline, or null when no payload behavior is required.</returns>
    /// <exception cref="ArgumentException">A registered behavior is incompatible with the declared contract.</exception>
    public CachePayloadPipeline Payloads(CacheCollectionBinding binding)
    {
        var aliases = binding.Options.Behaviors.PayloadTransformation.ToList();
        if (binding.Options.Compression.Enabled && !aliases.Contains("compression", StringComparer.OrdinalIgnoreCase))
        {
            aliases.Add("compression");
        }

        if (aliases.Count == 0)
        {
            return null;
        }

        var result = new List<ICachePayloadBehavior>();
        foreach (var alias in aliases)
        {
            var instance = catalog.Registry.Behaviors[(CacheBehaviorStage.PayloadTransformation, CacheIdentityNormalizer.Name(alias))](services);
            if (instance is not ICachePayloadBehavior behavior || !behavior.Supports(binding.Contract.ValueType, binding.Contract.IsBinary))
            {
                throw new ArgumentException("A registered payload behavior is incompatible with its collection contract.");
            }

            result.Add(behavior);
        }

        return new(result);
    }

    /// <summary>Resolves compatible owned-source behaviors in the collection's declared order.</summary>
    /// <param name="binding">The validated collection whose source aliases are resolved.</param>
    /// <returns>A read-only ordered list executed once per owned source work unit.</returns>
    /// <exception cref="ArgumentException">A registered behavior is incompatible with the declared contract.</exception>
    public IReadOnlyList<ICacheLoadBehavior> Loads(CacheCollectionBinding binding)
    {
        var result = new List<ICacheLoadBehavior>();
        foreach (var alias in binding.Options.Behaviors.SourceExecution)
        {
            var instance = catalog.Registry.Behaviors[(CacheBehaviorStage.SourceExecution, CacheIdentityNormalizer.Name(alias))](services);
            if (instance is not ICacheLoadBehavior behavior || !behavior.Supports(binding.Contract.ValueType, binding.Contract.IsBinary))
            {
                throw new ArgumentException("A registered owned-load behavior is incompatible with its collection contract.");
            }

            result.Add(behavior);
        }

        return result.AsReadOnly();
    }
}