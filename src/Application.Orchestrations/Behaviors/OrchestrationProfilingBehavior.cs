// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>Measures regular, signal and compensation attempts beneath a bounded orchestration execution scope.</summary>
/// <example><code>services.AddOrchestrations().WithProfilingBehavior();</code></example>
public sealed class OrchestrationProfilingBehavior(IOperationProfiler profiling = null) : IOrchestrationBehavior
{
    /// <inheritdoc />
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationActivityExecutionContext context, CancellationToken cancellationToken, OrchestrationDelegate next)
    {
        if (profiling is null) { return next(); }

        bool hasOwner;
        try { hasOwner = profiling.Current is not null; }
        catch (Exception) { return next(); }

        // An independently invoked activity owns a fallback slice; executor activities already have an owner.
        return !hasOwner
            ? profiling.JoinOrStartAsync(new OperationProfilingStartRequest
            {
                Key = "orchestration:" + context.OrchestrationName, Kind = OperationProfilingKind.Orchestration.ToString(), CorrelationId = context.CorrelationId,
            }, (_, _) => this.RunActivity(context, next, cancellationToken), cancellationToken, Classify)
            : this.RunActivity(context, next, cancellationToken);
    }

    private Task<OrchestrationOutcome> RunActivity(OrchestrationActivityExecutionContext context, OrchestrationDelegate next, CancellationToken cancellationToken) =>
        profiling.RunSegmentAsync("state:" + context.StateName, (_, _) =>
            profiling.RunSegmentAsync("action:" + context.Kind + ":" + context.ActivityName, async (scope, _) =>
            {
                if (!scope.IsRecording) { return await next().ConfigureAwait(false); }

                try
                {
                    scope.SetDimension("orchestration.attempt", context.Attempt);
                    scope.SetDimension("orchestration.kind", context.Kind.ToString());
                    scope.SetDimension("orchestration.instanceId", context.InstanceId.ToString("D"));
                }
                catch (Exception) { /* Diagnostic metadata cannot stop an action. */ }

                var outcome = await next().ConfigureAwait(false);
                try { scope.SetDimension("orchestration.outcome", outcome?.Kind.ToString()); }
                catch (Exception) { /* Domain flow control remains unchanged. */ }

                return outcome;
            }, cancellationToken, Classify), cancellationToken, Classify);

    private static ProfilingResultClassification Classify(OrchestrationOutcome outcome) => outcome is null ? null : new()
    {
        Outcome = outcome.Kind == OrchestrationOutcomeKind.Cancel ? OperationProfilingOutcome.Canceled : OperationProfilingOutcome.Completed,
    };
}
