// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class EchoJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory), IRetryJobScheduling,
    IChaosExceptionJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var dataMap = context.JobDetail.JobDataMap;
        dataMap.TryGetString("message", out var message);

        this.Logger.LogInformation("{LogKey} {JobMessage} (jobKey={JobKey}, lastProcessed={LastProcessed})",
            Constants.LogKey,
            message ?? "echo",
            context.JobDetail.Key,
            this.ProcessedDate);

        await Task.Delay(5000, cancellationToken);
    }
}