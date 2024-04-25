// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static CachingBuilderContext AddCaching(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<CachingBuilderContext> optionsAction = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        optionsAction?.Invoke(new CachingBuilderContext(services));

        services.TryAddSingleton<IMemoryCache>(sp =>
            new MemoryCache(new MemoryCacheOptions()));

        return new CachingBuilderContext(services, configuration);
    }

    public static CachingBuilderContext WithInMemoryProvider(
        this CachingBuilderContext context,
        InMemoryCacheProviderConfiguration configuration = null,
        string section = "Caching:InProcess")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<InMemoryCacheProviderConfiguration>() ?? new InMemoryCacheProviderConfiguration();

        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new InMemoryCacheProvider(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IMemoryCache>(), configuration));

        return context;
    }
}