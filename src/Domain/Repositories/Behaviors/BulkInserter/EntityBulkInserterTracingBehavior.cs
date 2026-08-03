// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Diagnostics;

/// <summary>
/// Creates one activity around the full bulk insert operation with non-payload diagnostic tags.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterTracingBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterTracingBehavior<TEntity>(IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    private static readonly ActivitySource ActivitySource = new("BridgingIT.DevKit.EntityBulkInserter");

    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        var operationId = Guid.NewGuid().ToString("N");
        using var activity = ActivitySource.StartActivity("EntityBulkInserter.Insert", ActivityKind.Internal);
        activity?.SetTag("bulk_inserter.operation_id", operationId);
        activity?.SetTag("bulk_inserter.entity_type", typeof(TEntity).Name);
        activity?.SetTag("bulk_inserter.entity_count", items.Count);

        try
        {
            var result = await inner.InsertAsync(items, cancellationToken).AnyContext();
            activity?.SetTag("bulk_inserter.inserted_count", result.IsSuccess ? result.Value : 0L);
            activity?.SetStatus(result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return result;
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }
}
