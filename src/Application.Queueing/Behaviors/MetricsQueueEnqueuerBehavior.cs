// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics.Metrics;
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
public class MetricsQueueEnqueuerBehavior(IMeterFactory meterFactory = null) : IQueueEnqueuerBehavior
{
    /// <summary>
    /// Wraps queue enqueue execution and records the corresponding metrics.
    /// </summary>
    /// <param name="message">The queued message.</param>
    /// <param name="cancellationToken">The enqueue cancellation token.</param>
    /// <param name="next">The next enqueue delegate.</param>
    public async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
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

        Metrics.Increment(meterFactory, enqueueSeries);
        Metrics.Increment(meterFactory, typedEnqueueSeries);
        Metrics.ChangeCurrent(meterFactory, currentEnqueueSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedEnqueueSeries, 1);

        try
        {
            await next().AnyContext();
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(enqueueSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedEnqueueSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentEnqueueSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedEnqueueSeries, -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(enqueueSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedEnqueueSeries), startedTimestamp);
        }
    }
}