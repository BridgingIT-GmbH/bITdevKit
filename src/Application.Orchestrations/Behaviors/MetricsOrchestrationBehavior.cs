// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using System.Diagnostics.Metrics;
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
public class MetricsOrchestrationBehavior(IMeterFactory meterFactory = null) : IOrchestrationBehavior
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
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
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

        Metrics.Increment(meterFactory, executeSeries);
        Metrics.Increment(meterFactory, typedExecuteSeries);
        Metrics.ChangeCurrent(meterFactory, currentExecuteSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedExecuteSeries, 1);

        try
        {
            return await next().AnyContext();
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