// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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
public class MetricsRequestBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IMetricsService metricsService = null)
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
        if (metricsService is null || cancellationToken.IsCancellationRequested)
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

        metricsService.AddCounter(sendSeries);
        metricsService.AddCounter(typedSendSeries);
        metricsService.AddCounter(handleSeries);
        metricsService.AddCounter(typedHandleSeries);
        metricsService.AddUpDownCounter(currentSendSeries, 1);
        metricsService.AddUpDownCounter(currentTypedSendSeries, 1);
        metricsService.AddUpDownCounter(currentHandleSeries, 1);
        metricsService.AddUpDownCounter(currentTypedHandleSeries, 1);

        try
        {
            var result = await next().AnyContext();

            if (result.IsFailure)
            {
                metricsService.AddCounter(Metrics.FailureSeries(sendSeries));
                metricsService.AddCounter(Metrics.FailureSeries(typedSendSeries));
                metricsService.AddCounter(Metrics.FailureSeries(handleSeries));
                metricsService.AddCounter(Metrics.FailureSeries(typedHandleSeries));
            }

            return result;
        }
        catch
        {
            metricsService.AddCounter(Metrics.FailureSeries(sendSeries));
            metricsService.AddCounter(Metrics.FailureSeries(typedSendSeries));
            metricsService.AddCounter(Metrics.FailureSeries(handleSeries));
            metricsService.AddCounter(Metrics.FailureSeries(typedHandleSeries));
            throw;
        }
        finally
        {
            metricsService.AddUpDownCounter(currentSendSeries, -1);
            metricsService.AddUpDownCounter(currentTypedSendSeries, -1);
            metricsService.AddUpDownCounter(currentHandleSeries, -1);
            metricsService.AddUpDownCounter(currentTypedHandleSeries, -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(sendSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedSendSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(handleSeries), startedTimestamp);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(typedHandleSeries), startedTimestamp);
        }
    }
}
