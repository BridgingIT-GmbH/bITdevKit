// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits queue enqueue total, current, failure, and duration metrics.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;MetricsQueueEnqueuerBehavior&gt;();
/// </code>
/// </example>
public class MetricsQueueEnqueuerBehavior(IMetricsService metricsService = null) : IQueueEnqueuerBehavior
{
    /// <summary>
    /// Wraps queue enqueue execution and records the corresponding metrics.
    /// </summary>
    /// <param name="message">The queued message.</param>
    /// <param name="cancellationToken">The enqueue cancellation token.</param>
    /// <param name="next">The next enqueue delegate.</param>
    public async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
    {
        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            await next().AnyContext();
            return;
        }

        var messageName = Metrics.NormalizeTypeName(message.GetType());
        var enqueueSeries = Metrics.Series("queueing_enqueue");
        var typedEnqueueSeries = Metrics.Series("queueing_enqueue", messageName);
        var currentEnqueueSeries = Metrics.CurrentSeries(enqueueSeries);
        var currentTypedEnqueueSeries = Metrics.CurrentSeries(typedEnqueueSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        metricsService.AddCounter(enqueueSeries);
        metricsService.AddCounter(typedEnqueueSeries);
        metricsService.AddUpDownCounter(currentEnqueueSeries, 1);
        metricsService.AddUpDownCounter(currentTypedEnqueueSeries, 1);

        try
        {
            await next().AnyContext();
        }
        catch
        {
            metricsService.AddCounter(Metrics.FailureSeries(enqueueSeries));
            metricsService.AddCounter(Metrics.FailureSeries(typedEnqueueSeries));
            throw;
        }
        finally
        {
            metricsService.AddUpDownCounter(currentEnqueueSeries, -1);
            metricsService.AddUpDownCounter(currentTypedEnqueueSeries, -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(enqueueSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedEnqueueSeries), startedTimestamp);
        }
    }
}
