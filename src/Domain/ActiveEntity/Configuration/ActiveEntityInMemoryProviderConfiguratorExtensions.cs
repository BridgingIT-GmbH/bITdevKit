// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;

/// <summary>
/// Provides extension methods for IEntityConfigurator to configure in-memory providers.
/// </summary>
public static class ActiveEntityInMemoryProviderConfiguratorExtensions
{
    /// <summary>
    /// Configures the in-memory provider for the entity using default options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.UseInMemoryProvider&lt;Customer, CustomerId&gt;()
    ///     .AddLoggingBehavior&lt;Customer, CustomerId&gt;();
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> UseInMemoryProvider<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.Services.AddSingleton(sp => new ActiveEntityInMemoryProviderOptions<TEntity> { Context = new InMemoryContext<TEntity>() });
        return configurator.UseProviderFactory(sp => new ActiveEntityInMemoryProvider<TEntity, TId>(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ActiveEntityInMemoryProviderOptions<TEntity>>()));
    }

    /// <summary>
    /// Configures the in-memory provider for the entity with custom options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="optionsBuilder">The builder for in-memory provider options.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.UseInMemoryProvider&lt;Order, OrderId&gt;(o => o
    ///     .LoggerFactory(sp.GetRequiredService&lt;ILoggerFactory&gt;())
    ///     .EnableOptimisticConcurrency())
    ///     .AddDomainEventPublishingBehavior&lt;Order, OrderId&gt;();
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> UseInMemoryProvider<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Func<ActiveEntityInMemoryProviderOptionsBuilder<TEntity>, ActiveEntityInMemoryProviderOptionsBuilder<TEntity>> optionsBuilder)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var options = optionsBuilder(new ActiveEntityInMemoryProviderOptionsBuilder<TEntity>()).Build();
        options.Context ??= new InMemoryContext<TEntity>();
        configurator.Services.AddSingleton(sp => options);
        return configurator.UseProviderFactory(sp => new ActiveEntityInMemoryProvider<TEntity, TId>(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ActiveEntityInMemoryProviderOptions<TEntity>>()));
    }
}
