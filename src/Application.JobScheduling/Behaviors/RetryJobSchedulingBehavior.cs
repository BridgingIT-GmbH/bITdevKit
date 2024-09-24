// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics;
using Common;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Quartz;

public class RetryJobSchedulingBehavior(ILoggerFactory loggerFactory) : JobSchedulingBehaviorBase(loggerFactory)
{
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as IRetryJobScheduling)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            var attempts = 1;
            var jobTypeName = context.JobDetail.JobType.FullName;
            AsyncRetryPolicy retryPolicy;
            if (!options.BackoffExponential)
            {
                retryPolicy = Policy.Handle<Exception>()
                    .WaitAndRetryAsync(options.Attempts,
                        attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0),
                        (ex, wait) =>
                        {
                            Activity.Current?.AddEvent(
                                new ActivityEvent($"Retry (attempt=#{attempts}, type={jobTypeName}) {ex.Message}"));
                            this.Logger.LogError(ex,
                                $"{{LogKey}} job retry behavior (attempt=#{{RetryAttempts}}, wait={{RetryWait}}, type={{JobType}}) {ex.Message}",
                                Constants.LogKey,
                                attempts,
                                wait.Humanize(),
                                jobTypeName);
                            attempts++;
                        });
            }
            else
            {
                retryPolicy = Policy.Handle<Exception>()
                    .WaitAndRetryAsync(options.Attempts,
                        attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0 * Math.Pow(2, attempt)),
                        (ex, wait) =>
                        {
                            Activity.Current?.AddEvent(
                                new ActivityEvent($"Retry (attempt=#{attempts}, type={jobTypeName}) {ex.Message}"));
                            this.Logger.LogError(ex,
                                $"{{LogKey}} job retry behavior (attempt=#{{RetryAttempts}}, wait={{RetryWait}}, type={{JobType}}) {ex.Message}",
                                Constants.LogKey,
                                attempts,
                                wait.Humanize(),
                                jobTypeName);
                            attempts++;
                        });
            }

            await retryPolicy.ExecuteAsync(async context => await next().AnyContext(), context.CancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}