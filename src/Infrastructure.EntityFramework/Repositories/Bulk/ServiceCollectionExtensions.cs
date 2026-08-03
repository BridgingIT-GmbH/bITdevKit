// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registers Entity Framework implementations of <see cref="IEntityBulkInserter{TEntity}"/>.
/// </summary>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Person, AppDbContext&gt;(
///     new SqlServerEntityBulkInsertOptions { BatchSize = 1_000 });
/// </code>
/// </example>
public static class EntityFrameworkBulkInsertServiceCollectionExtensions
{
    /// <summary>
    /// Registers the terminal Entity Framework bulk inserter and returns its decorator builder.
    /// </summary>
    /// <typeparam name="TEntity">The entity type inserted by the bulk inserter.</typeparam>
    /// <typeparam name="TContext">The Entity Framework context used by the terminal inserter.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="options">Optional provider-neutral bulk insert options.</param>
    /// <returns>A builder for registering <see cref="IEntityBulkInserter{TEntity}"/> decorators.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the DbContext is unavailable, has an unsupported lifetime, or a bulk inserter is already registered for the entity.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddEntityFrameworkBulkInserter&lt;Person, AppDbContext&gt;()
    ///     .WithBehavior&lt;LoggingPersonBulkInserter&gt;();
    /// </code>
    /// </example>
    public static EntityBulkInserterBuilderContext<TEntity> AddEntityFrameworkBulkInserter<
        TEntity,
        TContext
    >(this IServiceCollection services, EntityBulkInsertOptions options = null)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(services, nameof(services));

        options ??= new EntityBulkInsertOptions();
        options.Validate();

        var lifetime = GetDbContextLifetime<TContext>(services);
        ValidateLifetime(lifetime);
        EnsureBulkInserterIsNotRegistered<TEntity>(services);

        services.Add(new ServiceDescriptor(
            typeof(EntityBulkInsertMappingBuilder<TEntity>),
            typeof(EntityBulkInsertMappingBuilder<TEntity>),
            lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IEntityBulkInserter<TEntity>),
            serviceProvider => new EntityFrameworkEntityBulkInserter<TEntity, TContext>(
                serviceProvider.GetService<ILoggerFactory>(),
                serviceProvider.GetRequiredService<TContext>(),
                serviceProvider.GetRequiredService<EntityBulkInsertMappingBuilder<TEntity>>(),
                options,
                serviceProvider.GetServices<IEntityBulkInsertProvider>()),
            lifetime));

        return new EntityBulkInserterBuilderContext<TEntity>(
            services,
            lifetime,
            shadowValueProviderRegistration: RegisterShadowValueProvider<TEntity>);
    }

    private static void RegisterShadowValueProvider<TEntity>(
        IServiceCollection services,
        ServiceLifetime lifetime,
        Type providerType)
        where TEntity : class, IEntity
    {
        if (!typeof(IEntityBulkInsertShadowValueProvider<TEntity>).IsAssignableFrom(providerType))
        {
            throw new InvalidOperationException(
                $"Shadow value provider '{providerType.FullName}' must implement {typeof(IEntityBulkInsertShadowValueProvider<TEntity>).PrettyName()}.");
        }

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IEntityBulkInsertShadowValueProvider<TEntity>) &&
                descriptor.ImplementationType == providerType))
        {
            throw new InvalidOperationException(
                $"Bulk insert shadow value provider '{providerType.FullName}' is already registered for entity '{typeof(TEntity).FullName}'.");
        }

        services.Add(new ServiceDescriptor(
            typeof(IEntityBulkInsertShadowValueProvider<TEntity>),
            providerType,
            lifetime));
    }

    private static ServiceLifetime GetDbContextLifetime<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        var lifetimes = services
            .Where(descriptor => descriptor.ServiceType == typeof(TContext))
            .Select(descriptor => descriptor.Lifetime)
            .Distinct()
            .ToArray();

        return lifetimes.Length switch
        {
            1 => lifetimes[0],
            0 => throw new InvalidOperationException(
                $"Register DbContext '{typeof(TContext).FullName}' before registering an entity bulk inserter."),
            _ => throw new InvalidOperationException(
                $"DbContext '{typeof(TContext).FullName}' has multiple service lifetimes. Register it with one lifetime before registering an entity bulk inserter."),
        };
    }

    private static void ValidateLifetime(ServiceLifetime lifetime)
    {
        if (lifetime is ServiceLifetime.Singleton)
        {
            throw new InvalidOperationException(
                "Entity bulk insertion cannot be registered as singleton because DbContext is not thread-safe.");
        }
    }

    private static void EnsureBulkInserterIsNotRegistered<TEntity>(IServiceCollection services)
        where TEntity : class, IEntity
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IEntityBulkInserter<TEntity>)))
        {
            throw new InvalidOperationException(
                $"An entity bulk inserter is already registered for entity '{typeof(TEntity).FullName}'. Register exactly one DbContext and options configuration for this entity.");
        }
    }
}
