// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Applies ordinary cache I/O admission and provider readiness to background and administrative units.</summary>
/// <param name="catalog">The validated startup catalog.</param>
/// <param name="providers">The container-owned complete provider registry.</param>
/// <param name="resources">The shared process and collection resource parents.</param>
/// <param name="tasks">The owner that tracks native calls after an attempt timeout.</param>
/// <param name="timeProvider">The clock used for attempt deadlines.</param>
/// <example><code>var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken);</code></example>
public sealed class CacheWorkerAccess(CacheCatalog catalog, CacheProviderRegistry providers, CacheRuntimeResources resources,
    CacheRuntimeTasks tasks, TimeProvider timeProvider)
{
    private readonly CacheProviderInitialization initialization = new(catalog);
    private readonly ConcurrentDictionary<string, CacheProviderAccess> access = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Awaits an optional named database gate and checks the provider without blocking other independently scheduled scopes.</summary>
    /// <param name="binding">The enabled declared collection being admitted.</param>
    /// <param name="services">The current unit's owned service scope.</param>
    /// <param name="cancellationToken">The worker shutdown token.</param>
    /// <param name="loaderAlias">An optional trusted background-loader alias with its own named database dependency.</param>
    /// <returns>Success when dependencies are ready, otherwise a controlled readiness failure.</returns>
    public async ValueTask<Result> EnsureReadyAsync(CacheCollectionBinding binding, IServiceProvider services, CancellationToken cancellationToken = default, string loaderAlias = null)
    {
        var aliases = new List<string>();
        if (binding.Provider.Capabilities.Persistent && binding.Provider.Options.DatabaseReadyAlias is not null)
        {
            aliases.Add(binding.Provider.Options.DatabaseReadyAlias);
        }

        if (loaderAlias is not null && catalog.Registry.Loaders.TryGetValue(CacheIdentityNormalizer.Name(loaderAlias), out var loader) &&
            loader.DatabaseReadyAlias is not null)
        {
            aliases.Add(loader.DatabaseReadyAlias);
        }

        var gate = services.GetService<IDatabaseReadyService>();
        if (gate is not null)
        {
            foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await gate.WaitForReadyAsync(alias, timeout: catalog.Options.BackgroundServices.ReadinessTimeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is TimeoutException or InvalidOperationException && !cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The configured background dependency is not ready."));
                }
            }
        }

        var definitions = catalog.Collections.Values.Where(c => c.Provider.Name == binding.Provider.Name).Select(c =>
            new CacheProviderCollection(catalog.Options.Namespace, c.Name, c.Options.ContractVersion, c.Contract.Alias,
                c.Options.Dimensions, c.Options)).ToArray();
        return await this.GetAccess(binding).CallAsync(token => providers.Get(binding.Provider.Name).InitializeAsync(definitions, token),
            cancellationToken, initialize: false).ConfigureAwait(false);
    }

    /// <summary>Executes one provider unit with the same native attempt and occupied-work accounting as foreground cache operations.</summary>
    /// <typeparam name="T">The provider response type.</typeparam>
    /// <param name="binding">The declared collection whose native limits apply.</param>
    /// <param name="operation">The bounded provider call.</param>
    /// <param name="cancellationToken">The caller's bounded wait token.</param>
    /// <returns>The provider result, preserving uncertain native completion and occupied reservations.</returns>
    public ValueTask<Result<T>> CallAsync<T>(CacheCollectionBinding binding,
        Func<ICacheStoreProvider, CancellationToken, ValueTask<Result<T>>> operation, CancellationToken cancellationToken = default) =>
        this.GetAccess(binding).CallAsync(token => operation(providers.Get(binding.Provider.Name), token), cancellationToken);

    /// <summary>Executes one provider unit that carries no response value.</summary>
    /// <param name="binding">The declared collection whose native limits apply.</param>
    /// <param name="operation">The bounded provider call.</param>
    /// <param name="cancellationToken">The caller's bounded wait token.</param>
    /// <returns>The provider's controlled outcome.</returns>
    public ValueTask<Result> CallAsync(CacheCollectionBinding binding,
        Func<ICacheStoreProvider, CancellationToken, ValueTask<Result>> operation, CancellationToken cancellationToken = default) =>
        this.GetAccess(binding).CallAsync(token => operation(providers.Get(binding.Provider.Name), token), cancellationToken);

    private CacheProviderAccess GetAccess(CacheCollectionBinding binding) => this.access.GetOrAdd(binding.Name,
        _ => new(binding, providers.Get(binding.Provider.Name), resources, this.initialization, tasks, timeProvider));
}