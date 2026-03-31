// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Wraps pipeline execution and step execution in tracing activities.
/// </summary>
/// <example>
/// Register the behavior when a pipeline should participate in OpenTelemetry tracing:
/// <code>
/// services.AddPipelines()
///     .WithPipeline&lt;OrderImportContext&gt;("order-import", builder => builder
///         .AddBehavior&lt;PipelineTracingBehavior&gt;());
/// </code>
/// </example>
public class PipelineTracingBehavior(IEnumerable<ActivitySource> activitySources) : IPipelineBehavior<PipelineContextBase>
{
    /// <summary>
    /// Executes the inner pipeline delegate inside a pipeline activity.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The inner pipeline delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final pipeline result returned by the inner delegate.</returns>
    public async ValueTask<Result> ExecuteAsync(
        PipelineContextBase context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken)
    {
        var activitySource = activitySources.Find(context.Pipeline.Name);

        return await activitySource.StartActvity(
            $"PIPELINE Execute {context.Pipeline.Name}",
            async (activity, ct) =>
            {
                activity?.AddEvent(new ActivityEvent(
                    $"executing (pipeline={context.Pipeline.Name}, executionId={context.Pipeline.ExecutionId})"));

                return await next();
            },
            ActivityKind.Internal,
            tags: new Dictionary<string, string>
            {
                ["pipeline.name"] = context.Pipeline.Name,
                ["pipeline.execution_id"] = context.Pipeline.ExecutionId.ToString("N")
            },
            baggages: new Dictionary<string, string>
            {
                [ActivityConstants.CorrelationIdTagKey] = context.Pipeline.CorrelationId,
                ["pipeline.execution_id"] = context.Pipeline.ExecutionId.ToString("N"),
                ["pipeline.name"] = context.Pipeline.Name
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes the inner step delegate inside a step activity.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="step">The step definition being executed.</param>
    /// <param name="result">The carried pipeline result before the step runs.</param>
    /// <param name="next">The inner step delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The <see cref="PipelineControl"/> returned by the inner step delegate.</returns>
    public async ValueTask<PipelineControl> ExecuteStepAsync(
        PipelineContextBase context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken)
    {
        var activitySource = activitySources.Find(context.Pipeline.Name);

        return await activitySource.StartActvity(
            $"PIPELINE STEP {step.Name}",
            async (_, ct) => await next(),
            ActivityKind.Internal,
            tags: new Dictionary<string, string>
            {
                ["pipeline.name"] = context.Pipeline.Name,
                ["pipeline.execution_id"] = context.Pipeline.ExecutionId.ToString("N"),
                ["pipeline.step"] = step.Name
            },
            cancellationToken: cancellationToken);
    }
}
