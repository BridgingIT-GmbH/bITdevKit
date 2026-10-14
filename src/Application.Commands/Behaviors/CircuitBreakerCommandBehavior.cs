// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Humanizer;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

/// <summary>
/// Provides circuit breaker command behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class CircuitBreakerCommandBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : CommandBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    private static readonly TimeSpan MinBreakDuration = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is ICircuitBreakerCommand;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // retry only if implements interface
        if (request is not ICircuitBreakerCommand instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.Attempts <= 0)
        {
            instance.Options.Attempts = 1;
        }

        var breakDuration = instance.Options.BreakDuration.TotalMilliseconds >= 0
            ? instance.Options.BreakDuration
            : new TimeSpan(0, 0, 30);

        // Polly v8 has no consecutive-failure breaker: a failure ratio of 100% over a sampling window
        // spanning the whole execution breaks after N consecutive failures within this execution
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = int.MaxValue,
                Delay = instance.Options.Backoff,
                BackoffType = instance.Options.BackoffExponential ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = false,
                OnRetry = args =>
                {
                    this.Logger.LogError(args.Outcome.Exception,
                        "[{LogKey}] command circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, type={BehaviorType}) {ErrorMessage}",
                        Constants.LogKey,
                        args.AttemptNumber + 1,
                        args.RetryDelay.Humanize(),
                        this.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 1.0,
                MinimumThroughput = Math.Max(2, instance.Options.Attempts),
                SamplingDuration = TimeSpan.FromDays(1),
                BreakDuration = breakDuration < MinBreakDuration ? MinBreakDuration : breakDuration,
                OnOpened = args =>
                {
                    this.Logger.LogError(args.Outcome.Exception,
                        "[{LogKey}] command circuitbreaker behavior (circuit=open, wait={Wait}, type={BehaviorType}) {ErrorMessage}",
                        Constants.LogKey,
                        args.BreakDuration.Humanize(),
                        this.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return default;
                },
                OnClosed = args =>
                {
                    this.Logger.LogDebug(
                        "[{LogKey}] command circuitbreaker behavior (circuit=closed, type={BehaviorType})",
                        Constants.LogKey,
                        this.GetType().Name);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    this.Logger.LogDebug(
                        "[{LogKey}] command circuitbreaker behavior (circuit=halfopen, type={BehaviorType})",
                        Constants.LogKey,
                        this.GetType().Name);
                    return default;
                }
            })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
    }
}
