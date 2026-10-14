// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics;
using Humanizer;
using Polly;
using Polly.Retry;

/// <summary>
/// Provides retry job scheduling behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class RetryJobSchedulingBehavior(ILoggerFactory loggerFactory) : JobSchedulingBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as IRetryJobScheduling)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            var jobTypeName = context.JobDetail.JobType.FullName;
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
                            new ActivityEvent($"Retry (attempt=#{attempt}, type={jobTypeName}) {message}"));
                        this.Logger.LogError(args.Outcome.Exception,
                            $"{{LogKey}} job retry behavior (attempt=#{{RetryAttempts}}, wait={{RetryWait}}, type={{JobType}}) {message}",
                            Constants.LogKey,
                            attempt,
                            args.RetryDelay.Humanize(),
                            jobTypeName);
                        return default;
                    }
                })
                .Build();

            await pipeline.ExecuteAsync(async _ => await next().AnyContext(), context.CancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
