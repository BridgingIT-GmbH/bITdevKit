// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registers provider-neutral Entity Framework bulk insert orchestration for a repository entity type.
/// </summary>
/// <example>
/// <code>
/// services.AddEntityFrameworkRepository&lt;Person, AppDbContext&gt;()
///     .WithBulkInsert();
/// </code>
/// </example>
public static class EntityFrameworkBulkInsertServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEntityBulkInserter{TEntity}"/> and the provider-neutral mapping services for the repository entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type handled by the bulk inserter.</typeparam>
    /// <typeparam name="TContext">The Entity Framework context used by the bulk inserter.</typeparam>
    /// <param name="context">The repository builder context.</param>
    /// <param name="options">Optional provider-neutral bulk insert options.</param>
    /// <returns>The same builder context for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddEntityFrameworkRepository&lt;Person, AppDbContext&gt;()
    ///     .WithBulkInsert(new EntityBulkInsertOptions { BatchSize = 1_000 });
    /// </code>
    /// </example>
    public static EntityFrameworkRepositoryBuilderContext<TEntity, TContext> WithBulkInsert<TEntity, TContext>(
        this EntityFrameworkRepositoryBuilderContext<TEntity, TContext> context,
        EntityBulkInsertOptions options = null)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        options ??= new EntityBulkInsertOptions();
        RegisterServices(context, options);

        return context;
    }

    private static void RegisterServices<TEntity, TContext>(
        EntityFrameworkRepositoryBuilderContext<TEntity, TContext> context,
        EntityBulkInsertOptions options)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        context.Services.TryAdd(new ServiceDescriptor(
            typeof(EntityBulkInsertConfiguration<TEntity, TContext>),
            _ => new EntityBulkInsertConfiguration<TEntity, TContext>(options),
            context.Lifetime));
        context.Services.TryAdd(new ServiceDescriptor(
            typeof(EntityBulkInsertMappingBuilder<TEntity>),
            typeof(EntityBulkInsertMappingBuilder<TEntity>),
            context.Lifetime));
        context.Services.TryAdd(new ServiceDescriptor(
            typeof(IEntityBulkInserter<TEntity>),
            serviceProvider => new EntityFrameworkEntityBulkInserter<TEntity, TContext>(
                serviceProvider.GetService<ILoggerFactory>(),
                serviceProvider.GetRequiredService<TContext>(),
                serviceProvider.GetRequiredService<EntityBulkInsertMappingBuilder<TEntity>>(),
                serviceProvider.GetRequiredService<EntityBulkInsertConfiguration<TEntity, TContext>>().Options,
                serviceProvider.GetServices<IEntityBulkInsertProvider>()),
            context.Lifetime));
    }
}
