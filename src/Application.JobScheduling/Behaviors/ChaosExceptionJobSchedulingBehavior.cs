// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Polly;
using Polly.Simmy;
using Polly.Simmy.Fault;

/// <summary>
/// Provides chaos exception job scheduling behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class ChaosExceptionJobSchedulingBehavior(ILoggerFactory loggerFactory)
    : JobSchedulingBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IJobExecutionContext context, JobDelegate next)
    {
        var options = ((context.JobInstance as JobWrapper)?.InnerJob as IChaosExceptionJobScheduling)?.Options;
        if (options?.InjectionRate > 0)
        {
            // https://www.pollydocs.org/chaos/fault.html
            var fault = options.Fault ?? new ChaosException();
            var pipeline = new ResiliencePipelineBuilder()
                .AddChaosFault(new ChaosFaultStrategyOptions
                {
                    FaultGenerator = _ => new ValueTask<Exception>(fault),
                    InjectionRate = options.InjectionRate,
                    Enabled = true
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
