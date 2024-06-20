// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using Quartz;

public class ChaosExceptionJobSchedulingBehavior(ILoggerFactory loggerFactory) : JobSchedulingBehaviorBase(loggerFactory)
{
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as IChaosExceptionJobScheduling)?.Options;
        if (options?.InjectionRate > 0)
        {
            // https://github.com/Polly-Contrib/Simmy#Inject-exception
            var policy = MonkeyPolicy.InjectException(with =>
                with.Fault(options.Fault ?? new ChaosException())
                    .InjectionRate(options.InjectionRate)
                    .Enabled());

            await policy.Execute(async (context) => await next().AnyContext(), context.CancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}