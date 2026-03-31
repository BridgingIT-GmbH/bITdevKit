// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps pipeline execution and emits timing diagnostics for the whole pipeline.
/// </summary>
/// <example>
/// Register the behavior on a pipeline definition:
/// <code>
/// services.AddPipelines()
///     .WithPipeline&lt;OrderImportContext&gt;("order-import", builder => builder
///         .AddBehavior&lt;PipelineTimingBehavior&gt;());
/// </code>
/// </example>
public class PipelineTimingBehavior(ILogger<PipelineTimingBehavior> logger) : IPipelineBehavior<PipelineContextBase>
{
    /// <summary>
    /// Executes the timing wrapper around the inner pipeline delegate.
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();

            logger.LogDebug(
                "[{LogKey}] pipeline timing finished (pipeline={PipelineName}, executionId={ExecutionId}, success={Success}) -> took {DurationMs}ms",
                Constants.LogKey,
                context.Pipeline.Name,
                context.Pipeline.ExecutionId,
                result.IsSuccess,
                stopwatch.Elapsed.TotalMilliseconds);

            return result;
        }
        catch
        {
            logger.LogWarning(
                "[{LogKey}] pipeline timing failed (pipeline={PipelineName}, executionId={ExecutionId}) -> took {DurationMs}ms",
                Constants.LogKey,
                context.Pipeline.Name,
                context.Pipeline.ExecutionId,
                stopwatch.Elapsed.TotalMilliseconds);

            throw;
        }
    }
}
