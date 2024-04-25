// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

public static partial class ServiceCollectionExtensions
{
    public static RepositoryBuilderContext<TEntity> AddRepository<TEntity>(
        this IServiceCollection services,
        Func<IServiceProvider, IGenericRepository<TEntity>> repositoryFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(repositoryFactory, nameof(repositoryFactory));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>), repositoryFactory);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>), repositoryFactory);
                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>), repositoryFactory);
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }

    public static RepositoryBuilderContext<TEntity> AddRepository<TEntity, TRepository>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TRepository : class, IGenericRepository<TEntity>
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>), typeof(TRepository));
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>), typeof(TRepository));
                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>), typeof(TRepository));
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }

    public static RepositoryBuilderContext<TEntity> AddReadonlyRepository<TEntity>(
        this IServiceCollection services,
        Func<IServiceProvider, IGenericReadOnlyRepository<TEntity>> repositoryFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(repositoryFactory, nameof(repositoryFactory));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericReadOnlyRepository<TEntity>), repositoryFactory);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericReadOnlyRepository<TEntity>), repositoryFactory);
                break;
            default:
                services.AddScoped(typeof(IGenericReadOnlyRepository<TEntity>), repositoryFactory);
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }

    public static RepositoryBuilderContext<TEntity> AddReadonlyRepository<TEntity, TRepository>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TRepository : class, IGenericReadOnlyRepository<TEntity>
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericReadOnlyRepository<TEntity>), typeof(TRepository));
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericReadOnlyRepository<TEntity>), typeof(TRepository));
                break;
            default:
                services.AddScoped(typeof(IGenericReadOnlyRepository<TEntity>), typeof(TRepository));
                break;
        }

        return new RepositoryBuilderContext<TEntity>(services, lifetime);
    }
}