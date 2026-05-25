// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics.Metrics;
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
public class MetricsMessageHandlerBehavior(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
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

        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            await next().AnyContext();
            return;
        }

        var messageName = Metrics.NormalizeTypeName(message.GetType());
        var handleSeries = Metrics.Series("messaging_handle");
        var typedHandleSeries = Metrics.Series("messaging_handle", messageName);
        var currentHandleSeries = Metrics.CurrentSeries(handleSeries);
        var currentTypedHandleSeries = Metrics.CurrentSeries(typedHandleSeries);

        Metrics.Increment(meterFactory, handleSeries);
        Metrics.Increment(meterFactory, typedHandleSeries);
        Metrics.ChangeCurrent(meterFactory, currentHandleSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, 1);

        try
        {
            await next().AnyContext(); // continue pipeline
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
        }
    }
}