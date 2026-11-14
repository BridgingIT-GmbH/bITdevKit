// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Creates declared collection engines and composes their selected operation, source and payload behaviors.</summary>
/// <example><code>var collection = factory.Create(binding, contract, provider);</code></example>
public sealed class CacheCollectionFactory(CacheCatalog catalog, CacheRuntimeResources resources,
    CacheProviderInitialization initialization, CacheRuntimeTasks tasks, TimeProvider time, IServiceProvider services) : ICacheCollectionFactory
{
    /// <inheritdoc />
    public ICacheCollection<T> Create<T>(CacheCollectionBinding binding, CacheValueContract<T> contract, ICacheStoreProvider provider)
    {
        var observations = services.GetService<CacheDiagnostics>()?.ForCollection(binding.Name);
        var runtime = new CacheCollectionRuntime(binding, provider, catalog, resources, initialization, tasks, time, observations);
        runtime.Transforms = services.GetRequiredService<CachePayloadTransformRegistry>();
        runtime.PayloadPipeline = new CacheBehaviorFactory(catalog, services).Payloads(binding);
        ICacheCollection<T> inner = new CacheCollection<T>(runtime, contract, services);
        var behaviors = new CacheBehaviorFactory(catalog, services).Operations(binding);
        runtime.TransferObservers = behaviors.OfType<ICacheTransferObserver>().ToArray();
        inner = behaviors.Count == 0 ? inner : new CacheCollectionOperationBehavior<T>(inner, binding, tasks, runtime.Identity, behaviors);
        return observations?.Enabled == true ? new ObservingCacheCollectionBehavior<T>(inner, observations, time) : inner;
    }

    /// <inheritdoc />
    public ICacheBinaryCollection CreateBinary(CacheCollectionBinding binding, CacheBinaryValueContract contract, ICacheStoreProvider provider)
    {
        var observations = services.GetService<CacheDiagnostics>()?.ForCollection(binding.Name);
        var runtime = new CacheCollectionRuntime(binding, provider, catalog, resources, initialization, tasks, time, observations);
        runtime.Transforms = services.GetRequiredService<CachePayloadTransformRegistry>();
        runtime.PayloadPipeline = new CacheBehaviorFactory(catalog, services).Payloads(binding);
        ICacheBinaryCollection inner = new CacheBinaryCollection(runtime, contract, services);
        var behaviors = new CacheBehaviorFactory(catalog, services).Operations(binding);
        runtime.TransferObservers = behaviors.OfType<ICacheTransferObserver>().ToArray();
        inner = behaviors.Count == 0 ? inner : new CacheBinaryOperationBehavior(inner, binding, tasks, runtime.Identity, behaviors);
        return observations?.Enabled == true ? new ObservingCacheBinaryCollectionBehavior(inner, observations, time) : inner;
    }
}