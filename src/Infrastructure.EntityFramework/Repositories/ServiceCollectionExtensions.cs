// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using EntityFrameworkCore;
using EntityFrameworkCore.Diagnostics;
using EntityFrameworkCore.Infrastructure;
using Extensions;

public static partial class ServiceCollectionExtensions
{
    public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> AddEntityFrameworkRepository<TEntity,
        TContext>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TContext>));

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TContext>));

                break;
            default:
                services.TryAddScoped(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TContext>));

                break;
        }

        return new EntityFrameworkRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }

    public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> AddEntityFrameworkRepository<TEntity,
        TDatabaseEntity, TContext>(
        this IServiceCollection services,
        IEntityMapper entityMapper,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(entityMapper, nameof(entityMapper));

        services.TryAddSingleton(entityMapper);

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
            default:
                services.TryAddScoped(typeof(IGenericRepository<TEntity>),
                    typeof(EntityFrameworkRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
        }

        return new EntityFrameworkRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }

    public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext>
        AddEntityFrameworkReadonlyRepository<TEntity, TContext>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TContext>));

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TContext>));

                break;
            default:
                services.TryAddScoped(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TContext>));

                break;
        }

        return new EntityFrameworkRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }

    public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext>
        AddEntityFrameworkReadonlyRepository<TEntity, TDatabaseEntity, TContext>(
            this IServiceCollection services,
            IEntityMapper entityMapper,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(entityMapper, nameof(entityMapper));

        services.TryAddSingleton(entityMapper);

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
            default:
                services.TryAddScoped(typeof(IGenericReadOnlyRepository<TEntity>),
                    typeof(EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TDatabaseEntity, TContext>));

                break;
        }

        return new EntityFrameworkRepositoryBuilderContext<TEntity, TContext>(services, lifetime);
    }

    public static DbContextBuilderContext<TContext> AddInMemoryDbContext<TContext>(
        this IServiceCollection services,
        string name = null,
        Action<InMemoryDbContextOptionsBuilder> inMemoryOptionsAction = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, o) =>
            {
                o.UseInMemoryDatabase(name ?? typeof(TContext).Name, inMemoryOptionsAction);
                o.ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            },
            lifetime);

        return new DbContextBuilderContext<TContext>(services, lifetime, provider: Provider.InMemory);
    }
}