// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Polly;
using Polly.Simmy;
using Polly.Simmy.Fault;

/// <summary>
///     Randomly injects configured exceptions while executing startup tasks that opt into chaos behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
public class ChaosExceptionStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (task as IChaosExceptionStartupTask)?.Options;
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

            await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
