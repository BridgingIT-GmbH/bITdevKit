// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

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
    IEntityBulkInserter<TEntity> inner,
    IMetricsService metricsService = null) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        if (metricsService is null)
        {
            return await inner.InsertAsync(items, cancellationToken).AnyContext();
        }

        var series = Metrics.Series("bulk_inserter_insert");
        var typedSeries = Metrics.Series(series, Metrics.NormalizeTypeName(typeof(TEntity)));
        var started = Metrics.StartTimestamp();
        metricsService.AddCounter(series);
        metricsService.AddCounter(typedSeries);
        metricsService.AddUpDownCounter(Metrics.CurrentSeries(series), 1);
        metricsService.AddUpDownCounter(Metrics.CurrentSeries(typedSeries), 1);

        try
        {
            var result = await inner.InsertAsync(items, cancellationToken).AnyContext();
            if (result.IsFailure)
            {
                metricsService.AddCounter(Metrics.FailureSeries(series));
                metricsService.AddCounter(Metrics.FailureSeries(typedSeries));
            }

            return result;
        }
        catch
        {
            metricsService.AddCounter(Metrics.FailureSeries(series));
            metricsService.AddCounter(Metrics.FailureSeries(typedSeries));
            throw;
        }
        finally
        {
            metricsService.AddUpDownCounter(Metrics.CurrentSeries(series), -1);
            metricsService.AddUpDownCounter(Metrics.CurrentSeries(typedSeries), -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(series), started);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedSeries), started);
        }
    }
}
