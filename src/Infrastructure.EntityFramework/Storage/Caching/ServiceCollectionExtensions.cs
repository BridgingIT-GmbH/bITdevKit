// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static CachingBuilderContext WithEntityFrameworkDocumentStoreProvider<TContext>(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
        where TContext : DbContext, IDocumentStoreContext
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ?? new DocumentStoreCacheProviderConfiguration();

        // store client > store provider
        context.Services.TryAddScoped<IDocumentStoreClient<CacheDocument>>(sp =>
            new DocumentStoreClient<CacheDocument>(
                new EntityFrameworkDocumentStoreProvider<TContext>(
                    sp.GetRequiredService<TContext>())));

        // cache provider > distrbuted cache + store client
        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(
                sp.GetRequiredService<ILoggerFactory>(),
                new DocumentStoreCache(sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>()),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }
}