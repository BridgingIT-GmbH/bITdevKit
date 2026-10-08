// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Measures pipeline runs and actually executed step attempts with optional operation capture.</summary>
/// <example><code>definition.AddBehavior&lt;PipelineProfilingBehavior&gt;();</code></example>
public sealed class PipelineProfilingBehavior(IOperationProfiler profiling = null) : IPipelineBehavior<PipelineContextBase>
{
    /// <inheritdoc />
    public ValueTask<Result> ExecuteAsync(PipelineContextBase context, Func<ValueTask<Result>> next, CancellationToken cancellationToken)
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartValueTaskAsync(new OperationProfilingStartRequest
        {
            Key = "pipeline:" + context.Pipeline.Name, Kind = OperationProfilingKind.Pipeline.ToString(), CorrelationId = context.Pipeline.CorrelationId,
        }, (scope, _) =>
        {
            try { if (scope.IsRecording) { scope.SetDimension("pipeline.executionId", context.Pipeline.ExecutionId.ToString("D")); } }
            catch (Exception) { /* Profiling metadata cannot stop the pipeline. */ }

            return next();
        }, cancellationToken, result => OperationProfilingHelpers.ClassifyResult(result));
    }

    /// <inheritdoc />
    public ValueTask<PipelineControl> ExecuteStepAsync(PipelineContextBase context, IPipelineStepDefinition step, Result result,
        Func<ValueTask<PipelineControl>> next, CancellationToken cancellationToken)
    {
        if (profiling is null) { return next(); }

        return profiling.RunSegmentValueTaskAsync("step:" + step.Name, async (scope, _) =>
        {
            var control = await next().ConfigureAwait(false);
            try { if (scope.IsRecording) { scope.SetDimension("pipeline.control", control?.Outcome.ToString()); } }
            catch (Exception) { /* Flow control remains the pipeline's responsibility. */ }

            return control;
        }, cancellationToken, control => OperationProfilingHelpers.ClassifyResult(control?.Result));
    }
}
