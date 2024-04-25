// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static CachingBuilderContext WithAzureBlobDocumentStoreProvider(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ?? new DocumentStoreCacheProviderConfiguration();

        // store client > store provider
        context.Services.TryAddScoped<IDocumentStoreClient<CacheDocument>>(sp =>
            new DocumentStoreClient<CacheDocument>(
                new AzureBlobDocumentStoreProvider(
                    sp.GetRequiredService<ILoggerFactory>(),
                    configuration.ConnectionString)));

        // cache provider > distrbuted cache + store client
        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(
                sp.GetRequiredService<ILoggerFactory>(),
                new DocumentStoreCache(sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>()),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }

    public static CachingBuilderContext WithAzureTableDocumentStoreProvider(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ?? new DocumentStoreCacheProviderConfiguration();

        // store client > store provider
        context.Services.TryAddScoped<IDocumentStoreClient<CacheDocument>>(sp =>
            new DocumentStoreClient<CacheDocument>(
                new AzureTableDocumentStoreProvider(
                    sp.GetRequiredService<ILoggerFactory>(),
                    configuration.ConnectionString)));

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