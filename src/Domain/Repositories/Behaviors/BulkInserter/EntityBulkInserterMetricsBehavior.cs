// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits BulkInserter-specific total, current, failure, and duration metrics around the decorated inserter.
/// </summary>
/// <example>
/// <code>
/// builder.WithBehavior&lt;EntityBulkInserterMetricsBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterMetricsBehavior<TEntity>(
    IMeterFactory meterFactory,
    IEntityBulkInserter<TEntity> inner) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        if (meterFactory is null)
        {
            return await inner.InsertAsync(items, cancellationToken).AnyContext();
        }

        var series = Metrics.Series("bulk_inserter_insert");
        var typedSeries = Metrics.Series(series, Metrics.NormalizeTypeName(typeof(TEntity)));
        var started = Metrics.StartTimestamp();
        Metrics.Increment(meterFactory, series);
        Metrics.Increment(meterFactory, typedSeries);
        Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(series), 1);
        Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(typedSeries), 1);

        try
        {
            var result = await inner.InsertAsync(items, cancellationToken).AnyContext();
            if (result.IsFailure)
            {
                Metrics.Increment(meterFactory, Metrics.FailureSeries(series));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(typedSeries));
            }

            return result;
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(series));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(series), -1);
            Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(typedSeries), -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(series), started);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedSeries), started);
        }
    }
}
