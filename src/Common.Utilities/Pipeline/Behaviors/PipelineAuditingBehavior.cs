// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Records selected complete pipeline runs and actual step attempts without context or result payload capture.</summary>
/// <example><code>definition.AddBehavior&lt;PipelineAuditingBehavior&gt;();</code></example>
public sealed class PipelineAuditingBehavior(IServiceScopeFactory scopes) : IPipelineBehavior<PipelineContextBase>
{
    /// <inheritdoc />
    public ValueTask<Result> ExecuteAsync(PipelineContextBase context, Func<ValueTask<Result>> next, CancellationToken cancellationToken)
        => new(AuditSourceExecutor.RunAsync(scopes, builder => builder.For(context.GetType(), "Run").Kind("Pipeline"), () => next().AsTask(),
            new() { Feature = "Pipelines", FlowId = context.Pipeline.ExecutionId.ToString("D") }, starting: (_, scope) =>
            {
                scope.SetDetail("pipeline.name", Text(context.Pipeline.Name));
                scope.SetDetail("pipeline.executionId", AuditValue.From(context.Pipeline.ExecutionId.ToString("D")));
            }, completed: (scope, _) => scope.SetDetail("pipeline.observedSteps", AuditValue.From((long)context.Pipeline.ExecutedStepCount))));

    /// <inheritdoc />
    public ValueTask<PipelineControl> ExecuteStepAsync(PipelineContextBase context, IPipelineStepDefinition step, Result result,
        Func<ValueTask<PipelineControl>> next, CancellationToken cancellationToken)
        => new(AuditSourceExecutor.RunAsync(scopes, builder => builder.For(step.StepType ?? context.GetType(), "Step").Kind("PipelineStep"),
            () => next().AsTask(), new() { Feature = "Pipelines", HandlerType = step.StepType?.FullName, Attempt = context.Pipeline.CurrentStepAttempt, ExecutionStatus = "Invoked" },
            starting: (_, scope) => scope.SetDetail("pipeline.step", Text(step.Name)), completed: (scope, control) =>
            {
                scope.Complete(control is null ? AuditOutcome.Unknown : control.Result.IsFailure ? AuditOutcome.Rejected : AuditOutcome.Succeeded);
                (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = control?.Outcome.ToString() ?? "Unavailable" });
            }));

    private static AuditValue Text(string value) => value?.Length <= 512 ? AuditValue.From(value) : AuditValue.Policy("Oversized");
}
