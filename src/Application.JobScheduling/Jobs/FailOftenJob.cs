// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

[ExcludeFromCodeCoverage]
public class FailOftenJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
//IRetryJobScheduling,
//IChaosExceptionJobScheduling
{
    //RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    //ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.75 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var dataMap = context.JobDetail.JobDataMap;
        dataMap.TryGetString("Message", out var message);

        var random = new Random();
        var delay = random.Next(500, 3001); // Random delay between 500ms and 3000ms

        await Task.Delay(delay, cancellationToken);

        this.Logger.LogInformation("{LogKey} {JobMessage} (jobKey={JobKey}, lastProcessed={LastProcessed})",
            Constants.LogKey,
            message ?? "echo from failing job",
            context.JobDetail.Key,
            this.RunDate);

        // Randomly throw an exception
        if (random.NextDouble() < 0.5) // 50% chance to throw an exception
        {
            throw new Exception("FailingJob is randomly failing");
        }
    }
}
