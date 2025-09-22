// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain;

public static class ActiveEntityConfiguratorExtensions
{
    /// <summary>
    /// Adds a domain event outbox publishing behavior for the entity with optional configuration options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <typeparam name="TContext">The DbContext type implementing IOutboxDomainEventContext.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="options">Optional configuration options for the behavior.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.AddDomainEventOutboxPublishingBehavior&lt;Order, OrderId, DbContext&gt;(
    ///     new ActiveRecordDomainEventPublishingBehaviorOptions { PublishBefore = false });
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> AddDomainEventOutboxPublishingBehavior<TEntity, TId, TContext>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        ActiveEntityDomainEventPublishingBehaviorOptions options = null)
        where TEntity : ActiveEntity<TEntity, TId>
        where TContext : DbContext, IOutboxDomainEventContext
    {
        configurator.AddBehaviorType(
            typeof(ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext>), options ?? new ActiveEntityDomainEventPublishingBehaviorOptions());
        return configurator;
    }
}