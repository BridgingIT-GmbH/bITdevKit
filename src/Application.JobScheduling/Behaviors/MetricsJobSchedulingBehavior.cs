// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics.Metrics;
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
public class MetricsJobSchedulingBehavior(IMeterFactory meterFactory = null) : IJobSchedulingBehavior
{
    /// <summary>
    /// Wraps scheduled job execution and records the corresponding metrics.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="next">The next job execution delegate.</param>
    public async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        if (meterFactory is null || context.CancellationToken.IsCancellationRequested)
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

        Metrics.Increment(meterFactory, executeSeries);
        Metrics.Increment(meterFactory, typedExecuteSeries);
        Metrics.ChangeCurrent(meterFactory, currentExecuteSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedExecuteSeries, 1);

        try
        {
            await next().AnyContext();
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(executeSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedExecuteSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentExecuteSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedExecuteSeries, -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(executeSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedExecuteSeries), startedTimestamp);
        }
    }
}