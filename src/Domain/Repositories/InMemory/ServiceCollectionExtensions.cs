// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

public static partial class ServiceCollectionExtensions
{
    public static InMemoryRepositoryBuilderContext<TEntity, InMemoryContext<TEntity>> AddInMemoryRepository<TEntity>(
        this IServiceCollection services,
        InMemoryContext<TEntity> context = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (context is not null)
        {
            services.AddSingleton(context);
        }
        else
        {
            services.AddSingleton(new InMemoryContext<TEntity>());
        }

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));
                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, InMemoryContext<TEntity>>));
                break;
        }

        return new InMemoryRepositoryBuilderContext<TEntity, InMemoryContext<TEntity>>(services, lifetime);
    }

    public static InMemoryRepositoryBuilderContext<TEntity, TContext> AddInMemoryRepository<TEntity, TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TContext : InMemoryContext<TEntity>
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, TContext>));
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, TContext>));
                break;
            default:
                services.AddScoped(typeof(IGenericRepository<TEntity>), typeof(InMemoryRepositoryWrapper<TEntity, TContext>));
                break;
        }

        return new InMemoryRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }
}