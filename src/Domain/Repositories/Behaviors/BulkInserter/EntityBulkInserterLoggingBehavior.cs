// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Emits payload-free bulk-insert lifecycle logs including operation identity, counts, and duration.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterLoggingBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterLoggingBehavior<TEntity>(
    ILoggerFactory loggerFactory,
    IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    private readonly ILogger logger = loggerFactory?.CreateLogger<EntityBulkInserterLoggingBehavior<TEntity>>() ??
        NullLoggerFactory.Instance.CreateLogger<EntityBulkInserterLoggingBehavior<TEntity>>();

    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        var operationId = Guid.NewGuid().ToString("N");
        var started = Stopwatch.GetTimestamp();
        this.logger.LogInformation("[{LogKey}] bulk inserter started (operationId={OperationId}, entityType={EntityType}, count={Count})", Constants.LogKey, operationId, typeof(TEntity).Name, items.Count);

        try
        {
            var result = await inner.InsertAsync(items, cancellationToken).AnyContext();
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (result.IsSuccess)
            {
                this.logger.LogInformation("[{LogKey}] bulk inserter succeeded (operationId={OperationId}, entityType={EntityType}, count={Count}, insertedCount={InsertedCount}, durationMs={DurationMs})", Constants.LogKey, operationId, typeof(TEntity).Name, items.Count, result.Value, duration);
            }
            else
            {
                this.logger.LogWarning("[{LogKey}] bulk inserter failed (operationId={OperationId}, entityType={EntityType}, count={Count}, durationMs={DurationMs})", Constants.LogKey, operationId, typeof(TEntity).Name, items.Count, duration);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("[{LogKey}] bulk inserter cancelled (operationId={OperationId}, entityType={EntityType}, count={Count}, durationMs={DurationMs})", Constants.LogKey, operationId, typeof(TEntity).Name, items.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "[{LogKey}] bulk inserter failed (operationId={OperationId}, entityType={EntityType}, count={Count}, durationMs={DurationMs})", Constants.LogKey, operationId, typeof(TEntity).Name, items.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
