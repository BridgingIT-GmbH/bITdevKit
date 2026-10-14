// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

/// <summary>
///     Applies retry and circuit-breaker policies to startup tasks that provide circuit-breaker options.
/// </summary>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
public class CircuitBreakerStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    private static readonly TimeSpan MinBreakDuration = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var taskName = task.GetType().PrettyName();
        var options = (task as ICircuitBreakerStartupTask)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            var breakDuration = options.BreakDuration.TotalMilliseconds >= 0 ? options.BreakDuration : new TimeSpan(0, 0, 30);

            // Polly v8 has no consecutive-failure breaker: a failure ratio of 100% over a sampling window
            // spanning the whole execution breaks after N consecutive failures within this execution
            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = int.MaxValue,
                    Delay = options.Backoff,
                    BackoffType = options.BackoffExponential ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                    UseJitter = false,
                    OnRetry = args =>
                    {
                        this.Logger.LogError(args.Outcome.Exception,
                            "[{LogKey}] startup task circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, task={StartupTaskType}) {ErrorMessage}",
                            "UTL",
                            args.AttemptNumber + 1,
                            args.RetryDelay.Humanize(),
                            taskName,
                            args.Outcome.Exception?.Message);
                        return default;
                    }
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    FailureRatio = 1.0,
                    MinimumThroughput = Math.Max(2, options.Attempts),
                    SamplingDuration = TimeSpan.FromDays(1),
                    BreakDuration = breakDuration < MinBreakDuration ? MinBreakDuration : breakDuration,
                    OnOpened = args =>
                    {
                        this.Logger.LogError(args.Outcome.Exception,
                            "[{LogKey}] startup task circuitbreaker behavior (circuit=open, wait={Wait}, task={StartupTaskType}) {ErrorMessage}",
                            "UTL",
                            args.BreakDuration.Humanize(),
                            taskName,
                            args.Outcome.Exception?.Message);
                        return default;
                    },
                    OnClosed = args =>
                    {
                        this.Logger.LogDebug(
                            "[{LogKey}] startup task circuitbreaker behavior (circuit=closed, task={StartupTaskType})",
                            "UTL",
                            taskName);
                        return default;
                    },
                    OnHalfOpened = args =>
                    {
                        this.Logger.LogDebug(
                            "[{LogKey}] startup task circuitbreaker behavior (circuit=halfopen, task={StartupTaskType})",
                            "UTL",
                            taskName);
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
