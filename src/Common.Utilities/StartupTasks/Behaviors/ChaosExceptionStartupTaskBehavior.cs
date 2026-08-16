// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

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
            // https://github.com/Polly-Contrib/Simmy#Inject-exception
            var policy = MonkeyPolicy.InjectException(with =>
                with.Fault(options.Fault ?? new ChaosException()).InjectionRate(options.InjectionRate).Enabled());

            await policy.Execute(async _ => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
