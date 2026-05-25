// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits message publish total, current, and failure metrics around message dispatch.
/// </summary>
/// <example>
/// <code>
/// services.AddMessaging()
///     .WithBehavior&lt;MetricsMessagePublisherBehavior&gt;();
/// </code>
/// </example>
public class MetricsMessagePublisherBehavior(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
    : MessagePublisherBehaviorBase(loggerFactory)
{
    /// <summary>
    /// Wraps message publishing and records the corresponding metrics.
    /// </summary>
    /// <typeparam name="TMessage">The message type being published.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">The publish cancellation token.</param>
    /// <param name="next">The next publish delegate.</param>
    public override async Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        MessagePublisherDelegate next)
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
        var publishSeries = Metrics.Series("messaging_publish");
        var typedPublishSeries = Metrics.Series("messaging_publish", messageName);
        var currentPublishSeries = Metrics.CurrentSeries(publishSeries);
        var currentTypedPublishSeries = Metrics.CurrentSeries(typedPublishSeries);

        Metrics.Increment(meterFactory, publishSeries);
        Metrics.Increment(meterFactory, typedPublishSeries);
        Metrics.ChangeCurrent(meterFactory, currentPublishSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedPublishSeries, 1);

        try
        {
            await next().AnyContext(); // continue pipeline
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(publishSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedPublishSeries));

            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentPublishSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedPublishSeries, -1);
        }
    }
}