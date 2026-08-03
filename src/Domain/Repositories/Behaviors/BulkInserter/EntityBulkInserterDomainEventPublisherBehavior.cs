// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Publishes aggregate domain events after a successful bulk insert and clears them only after publication succeeds.
/// This post-persistence publication is non-atomic and must not be combined with the Outbox behavior.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterDomainEventPublisherBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterDomainEventPublisherBehavior<TEntity>(
    IDomainEventPublisher publisher,
    IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
{
    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        var result = await inner.InsertAsync(items, cancellationToken).AnyContext();
        if (result.IsFailure)
        {
            return result;
        }

        foreach (var entity in items)
        {
            foreach (var domainEvent in entity.DomainEvents.GetAll().ToArray())
            {
                var publishResult = await publisher.Send(domainEvent, cancellationToken).AnyContext();
                if (publishResult.IsFailure)
                {
                    return Result<long>.Failure().WithErrors(publishResult.Errors);
                }
            }
        }

        foreach (var entity in items)
        {
            entity.DomainEvents.Clear();
        }

        return result;
    }
}
