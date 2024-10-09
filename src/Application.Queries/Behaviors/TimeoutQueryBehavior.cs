// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using Humanizer;
using Polly;
using Polly.Timeout;

public class TimeoutQueryBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, IRequest<TResponse>
{
    protected override bool CanProcess(TRequest request)
    {
        return request is ITimeoutQuery;
    }

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

        var timeoutPolicy = Policy.TimeoutAsync(instance.Options.Timeout,
            TimeoutStrategy.Pessimistic,
            async (context, timeout, task) =>
            {
                await Task.Run(() => this.Logger.LogError(
                    "{LogKey} query timeout behavior (timeout={Timeout}, type={BehaviorType})",
                    Constants.LogKey,
                    timeout.Humanize(),
                    this.GetType().Name));
            });

        return await timeoutPolicy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken);
    }
}