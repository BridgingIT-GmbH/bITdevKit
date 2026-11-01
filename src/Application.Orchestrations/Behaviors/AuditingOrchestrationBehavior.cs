// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records actual regular, signal, and compensation attempts beneath the finite executor slice.</summary>
/// <example><code>services.AddOrchestrations().WithAuditingBehavior();</code></example>
public sealed class AuditingOrchestrationBehavior(IServiceScopeFactory scopes) : IOrchestrationBehavior
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationActivityExecutionContext context, CancellationToken cancellationToken, OrchestrationDelegate next)
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.Key("orchestration.activity").Kind("OrchestrationActivity"), () => next(),
            new() { Feature = "Orchestrations", FlowId = context.InstanceId.ToString("D"), Attempt = context.Attempt, ExecutionStatus = "Invoked" },
            starting: (_, scope) =>
            {
                scope.SetDetail("orchestration.state", Text(context.StateName));
                scope.SetDetail("orchestration.activity", Text(context.ActivityName));
                scope.SetDetail("orchestration.activityKind", AuditValue.From(context.Kind.ToString()));
            }, completed: (scope, outcome) =>
            {
                scope.Complete(outcome is null ? AuditOutcome.Unknown : outcome.Kind == OrchestrationOutcomeKind.Cancel ? AuditOutcome.Canceled : AuditOutcome.Succeeded);
                (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = outcome?.Kind.ToString() ?? "Unavailable" });
            });

    private static AuditValue Text(string text) => text?.Length <= 512 ? AuditValue.From(text) : AuditValue.Policy("Oversized");
}
