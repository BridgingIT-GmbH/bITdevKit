// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Polly; // TODO: migrate to Polly 8 https://www.pollydocs.org/migration-v8.html
using Quartz;
using Humanizer;
using Polly.Timeout;

public class TimeoutJobSchedulingBehavior : JobSchedulingBehaviorBase
{
    public TimeoutJobSchedulingBehavior(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as ITimeoutJobScheduling)?.Options;
        if (options is not null)
        {
            var timeoutPolicy = Policy
            .TimeoutAsync(options.Timeout, TimeoutStrategy.Pessimistic, onTimeoutAsync: async (context, timeout, task) =>
            {
                await Task.Run(() => this.Logger.LogError("{LogKey} job timeout behavior (timeout={Timeout}, type={BehaviorType})", Constants.LogKey, timeout.Humanize(), this.GetType().Name));
            });

            await timeoutPolicy.ExecuteAsync(async (context) => await next().AnyContext(), context.CancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}