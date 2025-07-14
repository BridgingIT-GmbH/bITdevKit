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

public class RetryStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
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

            await this.CreatePolicy(options, taskName).ExecuteAsync(async _ => await next().AnyContext(),
                cancellationToken).AnyContext();
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }

    private AsyncRetryPolicy CreatePolicy(RetryStartupTaskOptions options, string taskName)
    {
        return Policy
            .Handle<Exception>() // Consider specifying exact exception types if known
            .WaitAndRetryAsync(
                options.Attempts,
                (attempt, _) =>
                {
                    var delay = options.BackoffExponential
                        ? TimeSpan.FromMilliseconds(options.Backoff.Milliseconds * Math.Pow(2, attempt - 1))
                        : options.Backoff;

                    return delay;
                },
                (ex, timeSpan, attemptNumber, _) =>
                {
                    Activity.Current?.AddEvent(new ActivityEvent($"Retry (attempt=#{attemptNumber}, task={taskName}) {ex.Message}"));
                    this.Logger.LogError(ex,
                        $"{{LogKey}} startup task retry behavior (attempt=#{attemptNumber}, wait={timeSpan.Humanize()}, task={{StartupTaskType}}) {ex.Message}",
                        "UTL",
                        taskName);
                }
            );
    }
}