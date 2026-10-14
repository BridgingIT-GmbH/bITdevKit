// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using Humanizer;
using Polly;
using Polly.Timeout;

/// <summary>
/// Provides timeout query behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class TimeoutQueryBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is ITimeoutQuery;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // timeout only if implements interface
        if (request is not ITimeoutQuery instance)
        {
            return await next().AnyContext();
        }

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = instance.Options.Timeout,
                OnTimeout = args =>
                {
                    this.Logger.LogError(
                        "[{LogKey}] query timeout behavior (timeout={Timeout}, type={BehaviorType})",
                        Constants.LogKey,
                        args.Timeout.Humanize(),
                        this.GetType().Name);
                    return default;
                }
            })
            .Build();

        // WaitAsync abandons a handler that ignores cancellation, so the timeout is enforced pessimistically
        return await pipeline.ExecuteAsync(async token => await next().WaitAsync(token).AnyContext(), cancellationToken);
    }
}
