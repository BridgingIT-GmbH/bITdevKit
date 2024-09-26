// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

public class DummyStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var taskName = task.GetType().Name;

        this.Logger.LogDebug("{LogKey} >>>>> dummy startup task behavior - before (task={StartupTaskType})", "UTL", taskName);
        await next().AnyContext(); // continue pipeline
        this.Logger.LogDebug("{LogKey} <<<<< dummy startup task behavior - after (task={StartupTaskType})", "UTL", taskName);
    }
}