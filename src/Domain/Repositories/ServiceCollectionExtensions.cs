// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Provides extension methods for the <see cref="IServiceCollection" /> to add repository services.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds a repository for the specified entity type to the service collection.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="services">The service collection to which the repository should be added.</param>
    /// <param name="repositoryFactory">A factory function to create the repository instance.</param>
    /// <param name="lifetime">The lifetime of the service in the dependency injection container. Defaults to Scoped.</param>
    /// <returns>A <see cref="RepositoryBuilderContext{TEntity}" /> to allow further configuration.</returns>
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

    /// <summary>
    ///     Adds a generic repository of the specified types to the service collection with the specified lifetime.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TRepository">The type of the repository.</typeparam>
    /// <param name="services">The service collection to which the repository is added.</param>
    /// <param name="lifetime">The lifetime of the service (default is Scoped).</param>
    /// <returns>A context for configuring the repository.</returns>
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

    /// <summary>
    ///     Adds a readonly repository for the specified entity type to the service collection using a custom factory function.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="services">The service collection to which the repository should be added.</param>
    /// <param name="repositoryFactory">A factory function to create the repository.</param>
    /// <param name="lifetime">The service lifetime for the repository. Default is Scoped.</param>
    /// <returns>A <see cref="RepositoryBuilderContext{TEntity}" /> for further configuration.</returns>
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

    /// <summary>
    ///     Adds a read-only repository for the specified entity type with the specified lifespan into the service collection.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TRepository">The type of the repository.</typeparam>
    /// <param name="services">The service collection to add the repository to.</param>
    /// <param name="lifetime">The lifetime of the repository service instance.</param>
    /// <returns>A context for configuring the repository.</returns>
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