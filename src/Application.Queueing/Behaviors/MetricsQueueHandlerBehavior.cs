// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits queue handler total, current, failure, and duration metrics.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;MetricsQueueHandlerBehavior&gt;();
/// </code>
/// </example>
public class MetricsQueueHandlerBehavior(IMetricsService metricsService = null) : IQueueHandlerBehavior
{
    /// <summary>
    /// Wraps queue handler execution and records the corresponding metrics.
    /// </summary>
    /// <param name="message">The queue message being processed.</param>
    /// <param name="cancellationToken">The handler cancellation token.</param>
    /// <param name="handler">The concrete queue handler instance.</param>
    /// <param name="next">The next handler delegate.</param>
    public async Task Handle(IQueueMessage message, CancellationToken cancellationToken, object handler, QueueHandlerDelegate next)
    {
        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            await next().AnyContext();
            return;
        }

        var messageName = Metrics.NormalizeTypeName(message.GetType());
        var handleSeries = Metrics.Series("queueing_handle");
        var typedHandleSeries = Metrics.Series("queueing_handle", messageName);
        var currentHandleSeries = Metrics.CurrentSeries(handleSeries);
        var currentTypedHandleSeries = Metrics.CurrentSeries(typedHandleSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        metricsService.AddCounter(handleSeries);
        metricsService.AddCounter(typedHandleSeries);
        metricsService.AddUpDownCounter(currentHandleSeries, 1);
        metricsService.AddUpDownCounter(currentTypedHandleSeries, 1);

        try
        {
            await next().AnyContext();
        }
        catch
        {
            metricsService.AddCounter(Metrics.FailureSeries(handleSeries));
            metricsService.AddCounter(Metrics.FailureSeries(typedHandleSeries));
            throw;
        }
        finally
        {
            metricsService.AddUpDownCounter(currentHandleSeries, -1);
            metricsService.AddUpDownCounter(currentTypedHandleSeries, -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(handleSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedHandleSeries), startedTimestamp);
        }
    }
}
