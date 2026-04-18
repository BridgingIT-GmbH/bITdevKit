// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Caching.Distributed;
using Configuration;
using Extensions;

/// <summary>
/// Provides service-collection extensions for registering cache providers backed by document storage.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICacheProvider" /> using an existing <see cref="IDistributedCache" /> and
    /// <see cref="IDocumentStoreClient{T}" /> for <see cref="CacheDocument" /> persistence.
    /// </summary>
    /// <param name="context">The caching builder context.</param>
    /// <param name="configuration">The optional cache-provider configuration overrides.</param>
    /// <param name="section">The configuration section used when <paramref name="configuration" /> is not supplied.</param>
    /// <returns>The current caching builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDistributedMemoryCache();
    /// services.AddEntityFrameworkDocumentStoreClient&lt;CacheDocument, AppDbContext&gt;();
    ///
    /// services.AddCaching(configuration)
    ///     .UseDocumentStoreProvider();
    /// </code>
    /// </example>
    public static CachingBuilderContext UseDocumentStoreProvider(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ??
            new DocumentStoreCacheProviderConfiguration();

        context.Services.TryAddScoped<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IDistributedCache>(),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }
}
