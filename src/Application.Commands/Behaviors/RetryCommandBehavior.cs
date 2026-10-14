// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System.Diagnostics;
using Humanizer;
using Polly;
using Polly.Retry;

/// <summary>
/// Provides retry command behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class RetryCommandBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : CommandBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is IRetryCommand;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // retry only if implements interface
        if (request is not IRetryCommand instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.Attempts <= 0)
        {
            instance.Options.Attempts = 1;
        }

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = instance.Options.Attempts,
                Delay = instance.Options.Backoff,
                BackoffType = instance.Options.BackoffExponential ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = false,
                OnRetry = args =>
                {
                    var attempt = args.AttemptNumber + 1;
                    var message = args.Outcome.Exception?.Message;
                    Activity.Current?.AddEvent(
                        new ActivityEvent($"Retry (attempt=#{attempt}, type={this.GetType().Name}) {message}"));
                    this.Logger.LogError(args.Outcome.Exception,
                        $"{{LogKey}} command retry behavior (attempt=#{attempt}, wait={args.RetryDelay.Humanize()}, type={this.GetType().Name}) {message}",
                        Constants.LogKey);
                    return default;
                }
            })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
    }
}
