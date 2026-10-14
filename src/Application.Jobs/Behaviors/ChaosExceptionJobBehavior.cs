// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Polly;
using Polly.Simmy;
using Polly.Simmy.Fault;

/// <summary>
/// Injects chaos exceptions into job execution for failure-path testing.
/// </summary>
public sealed class ChaosExceptionJobBehavior : IJobBehavior
{
    /// <inheritdoc />
    public async Task<IResult<JobExecutionResult>> HandleAsync(
        JobBehaviorContext context,
        JobBehaviorDelegate next,
        CancellationToken cancellationToken = default)
    {
        if (context.Job is not IChaosExceptionJob instance || instance.Options.InjectionRate <= 0)
        {
            return await next().ConfigureAwait(false);
        }

        var fault = instance.Options.Fault ?? new ChaosException();
        var pipeline = new ResiliencePipelineBuilder()
            .AddChaosFault(new ChaosFaultStrategyOptions
            {
                FaultGenerator = _ => new ValueTask<Exception>(fault),
                InjectionRate = instance.Options.InjectionRate,
                Enabled = true
            })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next().ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }
}
