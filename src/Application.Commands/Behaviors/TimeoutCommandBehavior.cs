// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Humanizer;
using Polly;
using Polly.Timeout;

/// <summary>
/// Provides timeout command behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class TimeoutCommandBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : CommandBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is ITimeoutCommand;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // timeout only if implements interface
        if (request is not ITimeoutCommand instance)
        {
            return await next().AnyContext();
        }

        var timeoutPolicy = Policy.TimeoutAsync(instance.Options.Timeout,
            TimeoutStrategy.Pessimistic,
            async (context, timeout, task) =>
            {
                await Task.Run(() => this.Logger.LogError(
                    "[{LogKey}] command timeout behavior (timeout={Timeout}, type={BehaviorType})",
                    Constants.LogKey,
                    timeout.Humanize(),
                    this.GetType().Name));
            });

        return await timeoutPolicy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken);
    }
}
