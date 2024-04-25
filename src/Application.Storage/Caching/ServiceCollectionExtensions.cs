// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static CachingBuilderContext UseDocumentStoreProvider(
        this CachingBuilderContext context,
        DocumentStoreCacheProviderConfiguration configuration = null,
        string section = "Caching:DocumentStore")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<DocumentStoreCacheProviderConfiguration>() ?? new DocumentStoreCacheProviderConfiguration();

        context.Services.TryAddScoped<ICacheProvider>(sp =>
            new DocumentStoreCacheProvider(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IDistributedCache>(),
                sp.GetRequiredService<IDocumentStoreClient<CacheDocument>>(),
                configuration: configuration));

        return context;
    }
}