// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits job scheduling total, current, failure, and duration metrics around job execution.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduling()
///     .WithBehavior&lt;MetricsJobSchedulingBehavior&gt;();
/// </code>
/// </example>
public class MetricsJobSchedulingBehavior(IMetricsService metricsService = null) : IJobSchedulingBehavior
{
    /// <summary>
    /// Wraps scheduled job execution and records the corresponding metrics.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="next">The next job execution delegate.</param>
    public async Task Execute(Quartz.IJobExecutionContext context, JobDelegate next)
    {
        if (metricsService is null || context.CancellationToken.IsCancellationRequested)
        {
            await next().AnyContext();
            return;
        }

        var jobName = Metrics.NormalizeTypeName(context.JobDetail.JobType);
        var executeSeries = Metrics.Series("jobscheduling_execute");
        var typedExecuteSeries = Metrics.Series("jobscheduling_execute", jobName);
        var currentExecuteSeries = Metrics.CurrentSeries(executeSeries);
        var currentTypedExecuteSeries = Metrics.CurrentSeries(typedExecuteSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        metricsService.AddCounter(executeSeries);
        metricsService.AddCounter(typedExecuteSeries);
        metricsService.AddUpDownCounter(currentExecuteSeries, 1);
        metricsService.AddUpDownCounter(currentTypedExecuteSeries, 1);

        try
        {
            await next().AnyContext();
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
