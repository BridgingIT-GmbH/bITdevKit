// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits notifier publish total, current, failure, and duration metrics once per notification dispatch.
/// </summary>
/// <typeparam name="TRequest">The notification type.</typeparam>
/// <typeparam name="TResponse">The result type produced by the notification pipeline.</typeparam>
/// <example>
/// <code>
/// services.AddNotifier()
///     .WithBehavior(typeof(MetricsNotificationBehavior&lt;,&gt;));
/// </code>
/// </example>
public class MetricsNotificationBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
    : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return request is not null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return await next().AnyContext();
        }

        var notificationName = Metrics.NormalizeTypeName(typeof(TRequest));
        var publishSeries = Metrics.Series("notifier_publish");
        var typedPublishSeries = Metrics.Series("notifier_publish", notificationName);
        var currentPublishSeries = Metrics.CurrentSeries(publishSeries);
        var currentTypedPublishSeries = Metrics.CurrentSeries(typedPublishSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        Metrics.Increment(meterFactory, publishSeries);
        Metrics.Increment(meterFactory, typedPublishSeries);
        Metrics.ChangeCurrent(meterFactory, currentPublishSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedPublishSeries, 1);

        try
        {
            var result = await next().AnyContext();

            if (result.IsFailure)
            {
                Metrics.Increment(meterFactory, Metrics.FailureSeries(publishSeries));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(typedPublishSeries));
            }

            return result;
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
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(publishSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedPublishSeries), startedTimestamp);
        }
    }
}