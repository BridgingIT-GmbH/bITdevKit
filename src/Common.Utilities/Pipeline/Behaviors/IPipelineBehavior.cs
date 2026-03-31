// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Wraps pipeline execution with cross-cutting behavior for one context type.
/// </summary>
public interface IPipelineBehavior<TContext>
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Executes behavior logic around the inner pipeline delegate.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="next">The inner pipeline delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final pipeline result returned by the inner delegate.</returns>
    /// <example>
    /// <code>
    /// public sealed class PipelineTimingBehavior : IPipelineBehavior&lt;PipelineContextBase&gt;
    /// {
    ///     public async ValueTask&lt;Result&gt; ExecuteAsync(
    ///         PipelineContextBase context,
    ///         Func&lt;ValueTask&lt;Result&gt;&gt; next,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         return await next();
    ///     }
    /// }
    /// </code>
    /// </example>
    ValueTask<Result> ExecuteAsync(
        TContext context,
        Func<ValueTask<Result>> next,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes behavior logic around one step attempt inside the pipeline.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="step">The step definition being executed.</param>
    /// <param name="result">The carried pipeline result before the step runs.</param>
    /// <param name="next">The inner step delegate.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The <see cref="PipelineControl"/> returned by the inner step delegate.</returns>
    /// <example>
    /// <code>
    /// public sealed class PipelineTracingBehavior : IPipelineBehavior&lt;PipelineContextBase&gt;
    /// {
    ///     public ValueTask&lt;PipelineControl&gt; ExecuteStepAsync(
    ///         PipelineContextBase context,
    ///         IPipelineStepDefinition step,
    ///         Result result,
    ///         Func&lt;ValueTask&lt;PipelineControl&gt;&gt; next,
    ///         CancellationToken cancellationToken) => next();
    /// }
    /// </code>
    /// </example>
    ValueTask<PipelineControl> ExecuteStepAsync(
        TContext context,
        IPipelineStepDefinition step,
        Result result,
        Func<ValueTask<PipelineControl>> next,
        CancellationToken cancellationToken) =>
        next();
}
