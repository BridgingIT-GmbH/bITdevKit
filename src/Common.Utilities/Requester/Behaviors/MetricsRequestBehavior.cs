// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits requester total, current, failure, and duration metrics for request execution.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The result type produced by the request pipeline.</typeparam>
/// <example>
/// <code>
/// services.AddRequester()
///     .WithBehavior(typeof(MetricsRequestBehavior&lt;,&gt;));
/// </code>
/// </example>
public class MetricsRequestBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IMeterFactory meterFactory = null)
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

        var requestName = Metrics.NormalizeTypeName(typeof(TRequest));
        var sendSeries = Metrics.Series("requester_send");
        var typedSendSeries = Metrics.Series("requester_send", requestName);
        var handleSeries = Metrics.Series("requester_handle");
        var typedHandleSeries = Metrics.Series("requester_handle", requestName);
        var currentSendSeries = Metrics.CurrentSeries(sendSeries);
        var currentTypedSendSeries = Metrics.CurrentSeries(typedSendSeries);
        var currentHandleSeries = Metrics.CurrentSeries(handleSeries);
        var currentTypedHandleSeries = Metrics.CurrentSeries(typedHandleSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        Metrics.Increment(meterFactory, sendSeries);
        Metrics.Increment(meterFactory, typedSendSeries);
        Metrics.Increment(meterFactory, handleSeries);
        Metrics.Increment(meterFactory, typedHandleSeries);
        Metrics.ChangeCurrent(meterFactory, currentSendSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedSendSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentHandleSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, 1);

        try
        {
            var result = await next().AnyContext();

            if (result.IsFailure)
            {
                Metrics.Increment(meterFactory, Metrics.FailureSeries(sendSeries));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(typedSendSeries));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(handleSeries));
                Metrics.Increment(meterFactory, Metrics.FailureSeries(typedHandleSeries));
            }

            return result;
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(sendSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedSendSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(handleSeries));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedHandleSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentSendSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedSendSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentHandleSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedHandleSeries, -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(sendSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedSendSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(handleSeries), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedHandleSeries), startedTimestamp);
        }
    }
}