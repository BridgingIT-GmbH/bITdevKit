// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

/// <summary>
///     Applies a pessimistic timeout to startup tasks that supply timeout options.
/// </summary>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
public class TimeoutStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var taskName = task.GetType().PrettyName();
        var options = (task as ITimeoutStartupTask)?.Options;
        if (options is not null)
        {
            var timeoutPolicy = Policy.TimeoutAsync(options.Timeout,
                TimeoutStrategy.Pessimistic,
                async (context, timeout, task) =>
                {
                    await Task.Run(() =>
                        {
                            Activity.Current?.AddEvent(new ActivityEvent($"Timout (took={timeout.Humanize()}, task={taskName})"));
                            this.Logger.LogError(
                                $"{{LogKey}} startup task timeout behavior (timeout={timeout.Humanize()}, task={{StartupTaskType}})",
                                "UTL",
                                taskName);
                        },
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
