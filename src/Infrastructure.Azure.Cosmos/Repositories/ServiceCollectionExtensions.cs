// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static RepositoryBuilderContext<TEntity> AddCosmosSqlRepository<TEntity>(
        this IServiceCollection services,
        Builder<CosmosSqlProviderOptionsBuilder<TEntity>, CosmosSqlProviderOptions<TEntity>> providerOptionsBuilder,
        IEntityIdGenerator<TEntity> idGenerator = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var providerOptions = providerOptionsBuilder is not null
            ? providerOptionsBuilder(new CosmosSqlProviderOptionsBuilder<TEntity>()).Build()
            : new CosmosSqlProviderOptions<TEntity>();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<ICosmosSqlProvider<TEntity>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
                    return new CosmosSqlProvider<TEntity>(providerOptions);
                });
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<ICosmosSqlProvider<TEntity>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
                    return new CosmosSqlProvider<TEntity>(providerOptions);
                });
                break;
            default:
                services.TryAddScoped<ICosmosSqlProvider<TEntity>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
                    return new CosmosSqlProvider<TEntity>(providerOptions);
                });
                break;
        }

        return services.AddCosmosSqlRepository(provider: null, idGenerator, lifetime);
    }

    public static RepositoryBuilderContext<TEntity> AddCosmosSqlRepository<TEntity>(
        this IServiceCollection services,
        ICosmosSqlProvider<TEntity> provider = null,
        IEntityIdGenerator<TEntity> idGenerator = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                if (provider != null)
                {
                    services.TryAddSingleton(sp => provider);
                }

                services.TryAddSingleton<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(provider ?? sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
            case ServiceLifetime.Transient:
                if (provider != null)
                {
                    services.TryAddTransient(sp => provider);
                }

                services.TryAddTransient<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(provider ?? sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
            default:
                if (provider != null)
                {
                    services.TryAddScoped(sp => provider);
                }

                services.TryAddScoped<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(provider ?? sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }

    public static RepositoryBuilderContext<TEntity> AddCosmosSqlRepository<TEntity>(
        this IServiceCollection services,
        Func<IServiceProvider, ICosmosSqlProvider<TEntity>> providerFactory,
        IEntityIdGenerator<TEntity> idGenerator = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(providerFactory, nameof(providerFactory));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(providerFactory);
                services.TryAddSingleton<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(providerFactory);
                services.TryAddTransient<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
            default:
                services.TryAddScoped(providerFactory);
                services.TryAddScoped<IGenericRepository<TEntity>>(sp =>
                    new CosmosSqlGenericRepository<TEntity>(o => o
                        .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .Provider(sp.GetService<ICosmosSqlProvider<TEntity>>())
                        .IdGenerator(idGenerator)));
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }
}