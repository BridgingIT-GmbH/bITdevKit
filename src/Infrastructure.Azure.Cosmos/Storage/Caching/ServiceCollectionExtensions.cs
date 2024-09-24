// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Azure.Cosmos;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;
using BridgingIT.DevKit.Infrastructure.Azure.Storage;
using Configuration;
using Extensions;
using Logging;

public static partial class ServiceCollectionExtensions
{
    public static CachingBuilderContext WithCosmosDocumentStoreProvider(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ??
            new DocumentStoreCacheProviderConfiguration();

        // store client > store provider > client
        //if (!configuration.ConnectionString.IsNullOrEmpty())
        //{
        //    context.Services.TryAddScoped<ICosmosSqlProvider<CosmosStorageDocument>>(sp =>
        //        new CosmosSqlProvider<CosmosStorageDocument>(o => o
        //            .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
        //            .ConnectionString(configuration.ConnectionString)
        //            .PartitionKey(e => e.Type)));
        //}
        //else
        //{
        context.Services.TryAddScoped<ICosmosSqlProvider<CosmosStorageDocument>>(sp =>
            new CosmosSqlProvider<CosmosStorageDocument>(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .Client(sp.GetRequiredService<CosmosClient>())
                .PartitionKey(e => e.Type)));
        //}

        context.Services.TryAddScoped<IDocumentStoreClient<CacheDocument>>(sp =>
            new DocumentStoreClient<CacheDocument>(
                new CosmosDocumentStoreProvider(sp.GetRequiredService<ICosmosSqlProvider<CosmosStorageDocument>>())));

        // cache provider > distrbuted cache + store client
        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(sp.GetRequiredService<ILoggerFactory>(),
                new DocumentStoreCache(sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>()),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }
}