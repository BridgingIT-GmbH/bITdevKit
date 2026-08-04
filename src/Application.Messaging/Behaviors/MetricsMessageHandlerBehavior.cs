// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits message handler total, current, and failure metrics around message processing.
/// </summary>
/// <example>
/// <code>
/// services.AddMessaging()
///     .WithBehavior&lt;MetricsMessageHandlerBehavior&gt;();
/// </code>
/// </example>
public class MetricsMessageHandlerBehavior(ILoggerFactory loggerFactory, IMetricsService metricsService = null)
    : MessageHandlerBehaviorBase(loggerFactory)
{
    /// <summary>
    /// Wraps message handling and records the corresponding metrics.
    /// </summary>
    /// <typeparam name="TMessage">The message type being handled.</typeparam>
    /// <param name="message">The message being processed.</param>
    /// <param name="cancellationToken">The handler cancellation token.</param>
    /// <param name="handler">The concrete handler instance.</param>
    /// <param name="next">The next handler delegate.</param>
    public override async Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
    {
        if (message is null)
        {
            return;
        }

        if (metricsService is null || cancellationToken.IsCancellationRequested)
        {
            await next().AnyContext();
            return;
        }

        var messageName = Metrics.NormalizeTypeName(message.GetType());
        var handleSeries = Metrics.Series("messaging_handle");
        var typedHandleSeries = Metrics.Series("messaging_handle", messageName);
        var currentHandleSeries = Metrics.CurrentSeries(handleSeries);
        var currentTypedHandleSeries = Metrics.CurrentSeries(typedHandleSeries);

        metricsService.AddCounter(handleSeries);
        metricsService.AddCounter(typedHandleSeries);
        metricsService.AddUpDownCounter(currentHandleSeries, 1);
        metricsService.AddUpDownCounter(currentTypedHandleSeries, 1);

        try
        {
            await next().AnyContext(); // continue pipeline
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
        }
    }
}
