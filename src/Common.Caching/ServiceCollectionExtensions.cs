// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Caching.Memory;
using Configuration;
using Extensions;
using Logging;

/// <summary>
///     Provides dependency-injection registration for DevKit caching.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the shared memory cache and initializes a caching builder context.
    /// </summary>
    /// <param name="services">The service collection receiving registrations.</param>
    /// <param name="configuration">The configuration made available to subsequent provider registrations.</param>
    /// <param name="optionsAction">An optional action invoked with a builder context before the default registrations are added.</param>
    /// <returns>A caching builder context for registering a provider.</returns>
    public static CachingBuilderContext AddCaching(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<CachingBuilderContext> optionsAction = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        optionsAction?.Invoke(new CachingBuilderContext(services));

        services.TryAddSingleton<IMemoryCache>(sp => new MemoryCache(new MemoryCacheOptions()));

        return new CachingBuilderContext(services, configuration);
    }

    /// <summary>
    ///     Registers the in-memory cache provider using explicit settings or settings bound from configuration.
    /// </summary>
    /// <param name="context">The caching builder context.</param>
    /// <param name="configuration">Explicit provider settings, or <see langword="null"/> to bind them from <paramref name="section"/>.</param>
    /// <param name="section">The configuration section used when explicit settings are not supplied.</param>
    /// <returns>The same builder context.</returns>
    public static CachingBuilderContext WithInMemoryProvider(
        this CachingBuilderContext context,
        InMemoryCacheProviderConfiguration configuration = null,
        string section = "Caching:InProcess")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<InMemoryCacheProviderConfiguration>() ??
            new InMemoryCacheProviderConfiguration();

        context.Services.TryAddTransient<ICacheProvider>(sp =>
            new InMemoryCacheProvider(sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IMemoryCache>(),
                configuration));

        return context;
    }
}
