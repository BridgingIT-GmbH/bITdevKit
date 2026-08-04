// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Emits metrics for domain events currently registered on bulk-insert aggregates.
/// Register this after <see cref="EntityBulkInserterDomainEventBehavior{TEntity}"/> to include created events.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterDomainEventBehavior&lt;Order&gt;&gt;()
///     .WithBehavior&lt;EntityBulkInserterDomainEventMetricsBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterDomainEventMetricsBehavior<TEntity>(
    IEntityBulkInserter<TEntity> inner,
    IMetricsService metricsService = null) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
{
    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);

        if (metricsService is not null)
        {
            foreach (var domainEvent in items.SelectMany(entity => entity.DomainEvents.GetAll()))
            {
                metricsService.AddCounter(Metrics.Series("domainevents_create"));
                metricsService.AddCounter(Metrics.Series("domainevents_create", Metrics.NormalizeTypeName(domainEvent.GetType())));
            }
        }

        return inner.InsertAsync(items, cancellationToken);
    }
}
