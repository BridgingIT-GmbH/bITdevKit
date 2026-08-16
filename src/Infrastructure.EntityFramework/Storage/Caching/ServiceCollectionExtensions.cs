// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Configuration;
using Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes the with entity framework document store provider operation.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="section">The section used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CachingBuilderContext WithEntityFrameworkDocumentStoreProvider<TContext>(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
        where TContext : DbContext, IDocumentStoreContext
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ??
            new DocumentStoreCacheProviderConfiguration();

        // store client > store provider
        context.Services.TryAddScoped<IDocumentStoreClient<CacheDocument>>(sp =>
            new DocumentStoreClient<CacheDocument>(
                new EntityFrameworkDocumentStoreProvider<TContext>(sp.GetRequiredService<IServiceScopeFactory>())));

        // cache provider > distrbuted cache + store client
        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(sp.GetRequiredService<ILoggerFactory>(),
                new DocumentStoreCache(sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>()),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }
}
