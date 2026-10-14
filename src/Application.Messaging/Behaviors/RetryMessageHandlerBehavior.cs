// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics;
using Humanizer;
using Polly;
using Polly.Retry;

/// <summary>
/// Provides retry message handler behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class RetryMessageHandlerBehavior(ILoggerFactory loggerFactory) : MessageHandlerBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (handler as IRetryMessageHandler)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = options.Attempts,
                    Delay = options.Backoff,
                    BackoffType = options.BackoffExponential ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                    UseJitter = false,
                    OnRetry = args =>
                    {
                        var attempt = args.AttemptNumber + 1;
                        var message = args.Outcome.Exception?.Message;
                        Activity.Current?.AddEvent(
                            new ActivityEvent(
                                $"Retry (attempt=#{attempt}, type={this.GetType().Name}) {message}"));
                        this.Logger.LogError(args.Outcome.Exception,
                            $"{{LogKey}} message handler retry behavior (attempt=#{attempt}, wait={args.RetryDelay.Humanize()}, type={this.GetType().Name}) {message}",
                            Constants.LogKey);
                        return default;
                    }
                })
                .Build();

            await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
