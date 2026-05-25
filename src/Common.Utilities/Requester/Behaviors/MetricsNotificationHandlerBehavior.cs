// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits notifier handler total, current, failure, and duration metrics for each concrete handler execution.
/// </summary>
/// <typeparam name="TRequest">The notification type.</typeparam>
/// <typeparam name="TResponse">The result type produced by the handler pipeline.</typeparam>
/// <example>
/// <code>
/// services.AddNotifier()
///     .WithBehavior(typeof(MetricsNotificationHandlerBehavior&lt;,&gt;));
/// </code>
/// </example>
public class MetricsNotificationHandlerBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
    : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    /// <summary>
    /// Indicates that this behavior runs once per resolved notification handler.
    /// </summary>
    /// <returns><see langword="true"/> to opt into handler-specific execution.</returns>
    public override bool IsHandlerSpecific()
    {
        return true;
    }

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
        var handleSeries = Metrics.Series("notifier_handle");
        var typedHandleSeries = Metrics.Series("notifier_handle", notificationName);
        var currentHandleSeries = Metrics.CurrentSeries(handleSeries);
        var currentTypedHandleSeries = Metrics.CurrentSeries(typedHandleSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        Metrics.Increment(meterFactory, handleSeries);
        Metrics.Increment(meterFactory, typedHandleSeries);
        Metrics.ChangeCurrent(meterFactory, currentHandleSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, 1);

        try
        {
            var result = await next().AnyContext();

            if (result.IsFailure)
            {
                Metrics.Increment(meterFactory, Metrics.FailureSeries(handleSeries));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(typedHandleSeries));
            }

            return result;
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