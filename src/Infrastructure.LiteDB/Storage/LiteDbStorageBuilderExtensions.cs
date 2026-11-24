// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using static BridgingIT.DevKit.Application.Storage.FileStorageProviderFactory;

/// <summary>Registers native document, cache, virtual file, and permalink persistence.</summary>
public static class LiteDbStorageBuilderExtensions
{
    /// <summary>Registers a shared native document namespace for typed clients and cache composition.</summary>
    /// <param name="services">The host service registrations.</param>
    /// <param name="configure">Optional native namespace and bounds.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbDocumentStore();</code></example>
    public static IServiceCollection AddLiteDbDocumentStore(this IServiceCollection services, Action<LiteDbFeatureOptions> configure = null)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LiteDbDocumentStoreProvider)))
        {
            if (configure is not null) throw new ArgumentException("Configure the shared document namespace once before registering its clients.");
            return services;
        }

        var options = services.RegisterLiteDbFeature("documents", ["documents"], configure);
        services.AddSingleton(sp => new LiteDbDocumentStoreProvider(new(sp.GetRequiredService<IServiceScopeFactory>(), options), sp.GetService<TimeProvider>()));
        return services;
    }

    /// <summary>Selects native persistence through the existing typed document-client registration.</summary>
    /// <typeparam name="T">The document model.</typeparam>
    /// <param name="context">The existing storage builder.</param>
    /// <param name="configure">Optional shared native settings, configured once.</param>
    /// <param name="documentStoreOptions">The existing query and payload policies.</param>
    /// <param name="name">The typed client registration name.</param>
    /// <param name="isDefault">Whether this client is the default for its model.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddDocumentStorage().WithLiteDbClient&lt;LedgerEntry&gt;();</code></example>
    public static DocumentStorageBuilderContext WithLiteDbClient<T>(this DocumentStorageBuilderContext context, Action<LiteDbFeatureOptions> configure = null,
        DocumentStoreOptions documentStoreOptions = null, string name = "default", bool isDefault = true) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Options.IsEnabled) return context;
        context.Services.AddLiteDbDocumentStore(configure);
        return context.RegisterProvider<T>(sp => sp.GetRequiredService<LiteDbDocumentStoreProvider>(), "LiteDB",
            capabilities: LiteDbDocumentStoreProvider.StorageCapabilities, documentStoreOptions: documentStoreOptions, name: name, isDefault: isDefault);
    }

    /// <summary>Composes Common Caching from the native document client and existing cache adapters.</summary>
    /// <param name="context">The existing Common Caching builder.</param>
    /// <param name="configuration">Optional cache defaults.</param>
    /// <param name="configure">Optional shared native document settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddCaching().WithLiteDbDocumentStoreProvider();</code></example>
    public static CachingBuilderContext WithLiteDbDocumentStoreProvider(this CachingBuilderContext context, DocumentStoreCacheProviderConfiguration configuration = null, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddLiteDbDocumentStore(configure);
        context.Services.Replace(ServiceDescriptor.Scoped<IDocumentStoreClient<CacheDocument>>(sp => new DocumentStoreClient<CacheDocument>(sp.GetRequiredService<LiteDbDocumentStoreProvider>(), timeProvider: sp.GetService<TimeProvider>())));
        context.Services.Replace(ServiceDescriptor.Scoped<IDistributedCache>(sp => new DocumentStoreCache(sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(), sp.GetService<TimeProvider>())));
        context.Services.Replace(ServiceDescriptor.Scoped<ICacheProvider>(sp => new DocumentStoreCacheProvider(sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(), configuration: configuration ?? new())));
        return context;
    }

    /// <summary>Registers the native content owner used by file-provider factory registrations.</summary>
    /// <param name="services">The host service registrations.</param>
    /// <param name="configure">Optional named database, namespace, and list bounds.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbFileStorage();</code></example>
    public static IServiceCollection AddLiteDbFileStorage(this IServiceCollection services, Action<LiteDbFeatureOptions> configure = null)
    {
        var options = services.RegisterLiteDbFeature("files", ["entries", "generations", "chunks", "fences"], configure);
        if (options.MaxMaterializedRows < 16) throw new ArgumentException("File storage requires room for namespace and content records.");
        services.AddSingleton(sp => new LiteDbFileContentStore(new(sp.GetRequiredService<IServiceScopeFactory>(), options), sp.GetService<TimeProvider>()));
        return services;
    }

    /// <summary>Selects native file persistence for one existing file factory registration.</summary>
    /// <param name="builder">The existing file provider builder.</param>
    /// <param name="locationName">The isolated logical location.</param>
    /// <returns>The same file provider builder.</returns>
    /// <example><code>factory.RegisterProvider("ledger", files => files.UseLiteDb("ledger"));</code></example>
    public static FileStorageBuilder UseLiteDb(this FileStorageBuilder builder, string locationName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        builder.ProviderFactory = sp => new LiteDbFileStorageProvider(sp.GetRequiredService<LiteDbFileContentStore>(), locationName);
        return builder;
    }

    /// <summary>Selects native permalink identity, mutation-fence, and lifecycle persistence.</summary>
    /// <param name="context">The existing permalink builder.</param>
    /// <param name="configure">Optional native database and namespace settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddStoragePermalinks().UseLiteDb();</code></example>
    public static StoragePermalinkBuilderContext UseLiteDb(this StoragePermalinkBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("permalinks", ["entries", "locations", "tombstones", "prefixes"], configure);
        context.Services.AddSingleton(sp => new LiteDbStoragePermalinkRegistryProvider(new(sp.GetRequiredService<IServiceScopeFactory>(), options), sp.GetService<TimeProvider>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IStoragePermalinkRegistryProvider>(sp => sp.GetRequiredService<LiteDbStoragePermalinkRegistryProvider>()));
        return context;
    }
}
