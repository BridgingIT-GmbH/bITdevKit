// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a base class for asynchronous pipeline steps that do not require a shared context.
/// </summary>
public abstract class AsyncPipelineStep : AsyncPipelineStep<NullPipelineContext>
{
    /// <summary>
    /// Executes the step asynchronously without a shared context.
    /// </summary>
    /// <param name="result">The current carried result.</param>
    /// <param name="options">The execution options for the pipeline run.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The step control returned by the step.</returns>
    protected abstract ValueTask<PipelineControl> ExecuteAsync(
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    protected sealed override ValueTask<PipelineControl> ExecuteAsync(
        NullPipelineContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken) =>
        this.ExecuteAsync(result, options, cancellationToken);
}

/// <summary>
/// Provides a base class for asynchronous pipeline steps with a strongly typed context.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
public abstract class AsyncPipelineStep<TContext> : IPipelineStep
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Gets the canonical step name. By default the class name is converted to kebab-case and a trailing <c>Step</c> suffix is removed.
    /// </summary>
    public virtual string Name => PipelineStepNameConvention.FromType(this.GetType());

    /// <inheritdoc />
    ValueTask<PipelineControl> IPipelineStep.ExecuteAsync(
        PipelineContextBase context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken) =>
        this.ExecuteAsync((TContext)context, result, options, cancellationToken);

    /// <summary>
    /// Executes the step asynchronously with the strongly typed context.
    /// </summary>
    /// <param name="context">The strongly typed pipeline context.</param>
    /// <param name="result">The current carried result.</param>
    /// <param name="options">The execution options for the pipeline run.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The step control returned by the step.</returns>
    protected abstract ValueTask<PipelineControl> ExecuteAsync(
        TContext context,
        Result result,
        PipelineExecutionOptions options,
        CancellationToken cancellationToken);
}
