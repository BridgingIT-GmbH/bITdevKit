// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

public class TimeoutStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (task as ITimeoutStartupTask)?.Options;
        if (options is not null)
        {
            var timeoutPolicy = Policy.TimeoutAsync(options.Timeout,
                TimeoutStrategy.Pessimistic,
                async (context, timeout, task) =>
                {
                    await Task.Run(() =>
                            this.Logger.LogError(
                                $"{{LogKey}} startup task timeout behavior (timeout={timeout.Humanize()}, type={this.GetType().Name})",
                                "UTL"),
                        cancellationToken).ConfigureAwait(false);
                });

            await timeoutPolicy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}