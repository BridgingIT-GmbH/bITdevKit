// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics.Metrics;
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
public class MetricsQueueHandlerBehavior(IMeterFactory meterFactory = null) : IQueueHandlerBehavior
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
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
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

        Metrics.Increment(meterFactory, handleSeries);
        Metrics.Increment(meterFactory, typedHandleSeries);
        Metrics.ChangeCurrent(meterFactory, currentHandleSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, 1);

        try
        {
            await next().AnyContext();
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(handleSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedHandleSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentHandleSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(handleSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedHandleSeries), startedTimestamp);
        }
    }
}