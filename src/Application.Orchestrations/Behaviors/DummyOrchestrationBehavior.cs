// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.Logging;

/// <summary>
/// A no-op orchestration behavior that logs before and after activity execution.
/// </summary>
public class DummyOrchestrationBehavior(ILoggerFactory loggerFactory = null) : OrchestrationBehaviorBase(loggerFactory)
{
    /// <inheritdoc />
    public override async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationActivityExecutionContext context,
        CancellationToken cancellationToken,
        OrchestrationDelegate next)
    {
        this.Logger.LogDebug(
            "[{LogKey}] >>>>> dummy orchestration behavior - before (orchestration={Orchestration}, instanceId={InstanceId}, state={State}, activity={Activity}, kind={Kind}, attempt={Attempt})",
            Constants.LogKey,
            context.OrchestrationName,
            context.InstanceId,
            context.StateName,
            context.ActivityName,
            context.Kind,
            context.Attempt);

        var outcome = await next().ConfigureAwait(false);

        this.Logger.LogDebug(
            "[{LogKey}] <<<<< dummy orchestration behavior - after (orchestration={Orchestration}, instanceId={InstanceId}, state={State}, activity={Activity}, kind={Kind}, attempt={Attempt}, outcome={Outcome})",
            Constants.LogKey,
            context.OrchestrationName,
            context.InstanceId,
            context.StateName,
            context.ActivityName,
            context.Kind,
            context.Attempt,
            outcome?.Kind);

        return outcome;
    }
}
