// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Registers one created domain event for every aggregate in a bulk-insert batch.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterDomainEventBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterDomainEventBehavior<TEntity>(IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
{
    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);

        foreach (var entity in items)
        {
            entity.DomainEvents.Register(new EntityCreatedDomainEvent<TEntity>(entity));
        }

        return inner.InsertAsync(items, cancellationToken);
    }
}
