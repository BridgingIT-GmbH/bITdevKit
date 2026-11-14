// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes advanced Application.Caching independently of the existing Common.Caching registration family.</summary>
/// <example><code>services.AddApplicationCaching(cache => cache.WithCollection&lt;ProductCard&gt;("cards"));</code></example>
public static class ServiceCollectionExtensions
{
    /// <summary>Gets the conventional advanced-cache section within the host configuration.</summary>
    /// <example><code>var section = configuration.GetSection(ServiceCollectionExtensions.DefaultConfigurationSection);</code></example>
    public const string DefaultConfigurationSection = "Caching:Application";

    /// <summary>Registers or reuses the shared advanced cache builder so modules contribute to one startup catalog.</summary>
    /// <param name="services">The host service collection shared by the contributing modules.</param>
    /// <param name="configuration">An optional host root, read at Caching:Application, or explicit section read directly.</param>
    /// <returns>The shared startup builder for this service collection.</returns>
    /// <remarks>Root and section selection is identical with or without a setup callback. Complete all module registrations before resolving cache services or calling BuildCatalog. Runtime services and workers are registered once.</remarks>
    /// <example><code>
    /// services.AddApplicationCaching(catalogSection).WithValueContract&lt;ProductCard&gt;("catalog-card");
    /// services.AddApplicationCaching(ordersSection).WithValueContract&lt;OrderSummary&gt;("orders-summary");
    /// </code></example>
    public static ApplicationCachingBuilderContext AddApplicationCaching(this IServiceCollection services, IConfiguration configuration = null)
        => AddApplicationCaching(services, configuration, null);

    /// <summary>Configures advanced caching using the conventional Caching:Application host section.</summary>
    /// <param name="services">The host service collection shared by modules.</param>
    /// <param name="configure">Typed provider, collection, and host policy declarations.</param>
    /// <returns>The shared startup builder.</returns>
    /// <example><code>services.AddApplicationCaching(cache => cache.Namespace("catalog").WithCollection&lt;ProductCard&gt;("cards"));</code></example>
    public static ApplicationCachingBuilderContext AddApplicationCaching(this IServiceCollection services,
        Action<ApplicationCachingBuilderContext> configure)
    {
        var builder = Register(services, null, DefaultConfigurationSection);
        configure?.Invoke(builder);
        return builder;
    }

    /// <summary>Configures advanced caching from a custom host configuration section.</summary>
    /// <param name="services">The host service collection shared by modules.</param>
    /// <param name="sectionName">The section path, accepting colon or dot separators.</param>
    /// <param name="configure">Optional typed provider, collection, and host declarations.</param>
    /// <returns>The shared startup builder.</returns>
    /// <example><code>services.AddApplicationCaching("Modules.Catalog.Caching", cache => cache.WithCollection&lt;ProductCard&gt;("cards"));</code></example>
    public static ApplicationCachingBuilderContext AddApplicationCaching(this IServiceCollection services, string sectionName,
        Action<ApplicationCachingBuilderContext> configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        var builder = Register(services, null, sectionName);
        configure?.Invoke(builder);
        return builder;
    }

    /// <summary>Configures advanced caching from a supplied host root or explicit section and typed declarations.</summary>
    /// <param name="services">The host service collection shared by modules.</param>
    /// <param name="configuration">The host root, read at Caching:Application, or an explicit section read directly.</param>
    /// <param name="configure">Typed provider, collection, and host policy declarations.</param>
    /// <returns>The shared startup builder.</returns>
    /// <example><code>services.AddApplicationCaching(configuration, cache => cache.WithCollection&lt;ProductCard&gt;("cards"));</code></example>
    public static ApplicationCachingBuilderContext AddApplicationCaching(this IServiceCollection services, IConfiguration configuration,
        Action<ApplicationCachingBuilderContext> configure)
    {
        var section = configuration is IConfigurationSection ? configuration : configuration?.GetSection(DefaultConfigurationSection);
        var builder = Register(services, section, configuration is null ? DefaultConfigurationSection : null);
        configure?.Invoke(builder);
        return builder;
    }

    private static ApplicationCachingBuilderContext Register(IServiceCollection services, IConfiguration configuration, string sectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registration = services.FirstOrDefault(d => d.ServiceType == typeof(ApplicationCachingBuilderContext));
        if (registration is not null)
        {
            if (registration.ImplementationInstance is not ApplicationCachingBuilderContext existing)
            {
                throw new InvalidOperationException("Application.Caching requires its shared startup builder instance registration.");
            }

            existing.WithConfiguration(configuration);
            return sectionName is null ? existing : existing.WithConfigurationSection(sectionName);
        }

        var builder = new ApplicationCachingBuilderContext(services, configuration);
        if (sectionName is not null) { builder.WithConfigurationSection(sectionName); }

        builder.WithBehavior<CompressionCachePayloadBehavior>("compression", CacheBehaviorStage.PayloadTransformation);
        builder.WithBehavior<LoggingCacheBehavior>("logging", CacheBehaviorStage.CollectionOperations)
            .WithBehavior<LoggingCacheBehavior>("logging", CacheBehaviorStage.SourceExecution)
            .WithBehavior<MetricsCacheBehavior>("metrics", CacheBehaviorStage.CollectionOperations)
            .WithBehavior<MetricsCacheBehavior>("metrics", CacheBehaviorStage.SourceExecution)
            .WithBehavior<ProfilingCacheBehavior>("profiling", CacheBehaviorStage.CollectionOperations)
            .WithBehavior<ProfilingCacheBehavior>("profiling", CacheBehaviorStage.SourceExecution)
            .WithBehavior<AuditingCacheBehavior>("auditing", CacheBehaviorStage.CollectionOperations);
        builder.WithProviderKind("InMemory", InMemoryCacheStoreProvider.DefaultCapabilities, (sp, name, options) =>
            new InMemoryCacheStoreProvider(name, options, sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<CacheRuntimeResources>(), sp.GetRequiredService<CacheCatalog>().Options.Diagnostics));
        services.AddSingleton(builder);
        services.AddSingleton(provider => builder.BuildCatalog(provider.GetService<IConfiguration>()));
        services.AddSingleton<CacheProviderRegistry>();
        services.AddSingleton<CacheRuntimeResources>();
        services.AddSingleton<CacheDiagnostics>();
        services.AddSingleton<CacheDiagnosticQueryGate>();
        services.AddSingleton<CacheDiagnosticsQueryService>();
        services.AddSingleton<CacheMetricHistory>();
        services.AddSingleton<CacheRuntimeTasks>();
        services.AddSingleton(sp => new CachePayloadTransformRegistry(sp.GetRequiredService<CacheCatalog>(), sp));
        services.AddSingleton(sp => new CacheProviderInitialization(sp.GetRequiredService<CacheCatalog>()));
        services.AddSingleton(sp => new CacheSourceResilienceRegistry(sp.GetRequiredService<CacheCatalog>(), sp));
        services.AddSingleton<ICacheCollectionFactory>(sp => new CacheCollectionFactory(sp.GetRequiredService<CacheCatalog>(),
            sp.GetRequiredService<CacheRuntimeResources>(), sp.GetRequiredService<CacheProviderInitialization>(),
            sp.GetRequiredService<CacheRuntimeTasks>(), sp.GetRequiredService<TimeProvider>(), sp));
        services.AddSingleton<IApplicationCache>(sp => new ApplicationCache(sp.GetRequiredService<CacheCatalog>(),
            sp.GetRequiredService<ICacheCollectionFactory>(), sp));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<CacheWorkerAccess>();
        services.TryAddSingleton<CacheBackgroundControl>();
        services.TryAddSingleton<CacheWarmupService>();
        services.TryAddSingleton<CacheOperationRunner>();
        services.TryAddSingleton<CacheOperationService>();
        services.TryAddSingleton<CacheMaintenanceService>();
        services.TryAddSingleton<CacheRefreshService>();
        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
        {
            services.AddHostedService(sp => sp.GetRequiredService<CacheMaintenanceService>());
            services.AddHostedService(sp => sp.GetRequiredService<CacheRefreshService>());
            services.TryAddBackgroundServiceHealthCheck<CacheMaintenanceService>(nameof(CacheMaintenanceService), tags: ["background", "caching"]);
            services.TryAddBackgroundServiceHealthCheck<CacheRefreshService>(nameof(CacheRefreshService), tags: ["background", "caching"]);
        }

        return builder;
    }
}