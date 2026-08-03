// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Assigns a fresh sequential concurrency value to every entity in a bulk-insert batch.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterConcurrencyBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterConcurrencyBehavior<TEntity>(IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IConcurrency
{
    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);

        foreach (var entity in items)
        {
            entity.ConcurrencyVersion = GuidGenerator.CreateSequential();
        }

        return inner.InsertAsync(items, cancellationToken);
    }
}
