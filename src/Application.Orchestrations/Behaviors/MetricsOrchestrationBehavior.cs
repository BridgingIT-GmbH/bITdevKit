// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits orchestration activity total, current, failure, and duration metrics.
/// </summary>
/// <example>
/// <code>
/// services.AddOrchestrations()
///     .WithBehavior&lt;MetricsOrchestrationBehavior&gt;();
/// </code>
/// </example>
public class MetricsOrchestrationBehavior(IMetricsService metricsService = null) : IOrchestrationBehavior
{
    /// <summary>
    /// Wraps orchestration activity execution and records the corresponding metrics.
    /// </summary>
    /// <param name="context">The orchestration activity context.</param>
    /// <param name="cancellationToken">The activity cancellation token.</param>
    /// <param name="next">The next orchestration delegate.</param>
    /// <returns>The resulting orchestration outcome.</returns>
    public async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationActivityExecutionContext context,
        CancellationToken cancellationToken,
        OrchestrationDelegate next)
    {
        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            return await next().AnyContext();
        }

        var executeSeries = Metrics.Series("orchestrations_activity_execute");
        var typedExecuteSeries = Metrics.Series(
            "orchestrations_activity_execute",
            context.OrchestrationName,
            context.ActivityName);
        var currentExecuteSeries = Metrics.CurrentSeries(executeSeries);
        var currentTypedExecuteSeries = Metrics.CurrentSeries(typedExecuteSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        metricsService.AddCounter(executeSeries);
        metricsService.AddCounter(typedExecuteSeries);
        metricsService.AddUpDownCounter(currentExecuteSeries, 1);
        metricsService.AddUpDownCounter(currentTypedExecuteSeries, 1);

        try
        {
            return await next().AnyContext();
        }
        catch
        {
            metricsService.AddCounter(Metrics.FailureSeries(executeSeries));
            metricsService.AddCounter(Metrics.FailureSeries(typedExecuteSeries));
            throw;
        }
        finally
        {
            metricsService.AddUpDownCounter(currentExecuteSeries, -1);
            metricsService.AddUpDownCounter(currentTypedExecuteSeries, -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(executeSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedExecuteSeries), startedTimestamp);
        }
    }
}
