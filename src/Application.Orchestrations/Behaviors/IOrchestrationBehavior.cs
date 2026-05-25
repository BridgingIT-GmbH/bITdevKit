// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Defines a behavior that wraps orchestration activity execution.
/// </summary>
public interface IOrchestrationBehavior
{
    /// <summary>
    /// Executes behavior logic around an orchestration activity.
    /// </summary>
    /// <param name="context">The execution context for the current orchestration activity attempt.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="next">The next delegate in the behavior pipeline.</param>
    /// <returns>The orchestration outcome returned by the wrapped activity pipeline.</returns>
    Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationActivityExecutionContext context,
        CancellationToken cancellationToken,
        OrchestrationDelegate next);
}
