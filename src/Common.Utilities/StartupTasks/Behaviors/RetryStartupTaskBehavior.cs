// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

/// <summary>
///     Retries startup tasks that supply retry options and records retry activity.
/// </summary>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
public class RetryStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var taskName = task.GetType().PrettyName();
        var options = (task as IRetryStartupTask)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            await this.CreatePipeline(options, taskName).ExecuteAsync(async _ => await next().AnyContext(),
                cancellationToken).AnyContext();
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }

    private ResiliencePipeline CreatePipeline(RetryStartupTaskOptions options, string taskName)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(), // Consider specifying exact exception types if known
                MaxRetryAttempts = options.Attempts,
                Delay = options.Backoff,
                BackoffType = options.BackoffExponential ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = false,
                OnRetry = args =>
                {
                    var attemptNumber = args.AttemptNumber + 1;
                    var message = args.Outcome.Exception?.Message;
                    Activity.Current?.AddEvent(new ActivityEvent($"Retry (attempt=#{attemptNumber}, task={taskName}) {message}"));
                    this.Logger.LogError(args.Outcome.Exception,
                        $"{{LogKey}} startup task retry behavior (attempt=#{attemptNumber}, wait={args.RetryDelay.Humanize()}, task={{StartupTaskType}}) {message}",
                        "UTL",
                        taskName);
                    return default;
                }
            })
            .Build();
    }
}
