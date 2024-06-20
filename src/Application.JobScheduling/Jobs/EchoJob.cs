// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

public class EchoJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory),
    IRetryJobScheduling,
    IChaosExceptionJobScheduling
{
    RetryJobSchedulingOptions IRetryJobScheduling.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("{LogKey} echo (jobKey={JobKey}, lastProcessed={LastProcessed})", Constants.LogKey, context.JobDetail.Key, this.ProcessedDate);

        await Task.Delay(5000, cancellationToken);
    }
}