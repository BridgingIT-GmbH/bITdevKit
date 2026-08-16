// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Humanizer;
using Polly;
using Polly.Timeout;

/// <summary>
/// Provides timeout job scheduling behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class TimeoutJobSchedulingBehavior(ILoggerFactory loggerFactory) : JobSchedulingBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as ITimeoutJobScheduling)?.Options;
        if (options is not null)
        {
            var jobTypeName = context.JobDetail.JobType.FullName;
            var timeoutPolicy = Policy.TimeoutAsync(options.Timeout,
                TimeoutStrategy.Pessimistic,
                async (context, timeout, task) =>
                {
                    await Task.Run(() =>
                        this.Logger.LogError("[{LogKey}] job timeout behavior (timeout={Timeout}, type={JobType})",
                            Constants.LogKey,
                            timeout.Humanize(),
                            jobTypeName));
                });

            await timeoutPolicy.ExecuteAsync(async context => await next().AnyContext(), context.CancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
