// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a base class for synchronous pipeline steps that do not require a shared context.
/// </summary>
public abstract class PipelineStep : PipelineStep<NullPipelineContext>
{
    /// <summary>
    /// Executes the step synchronously without a shared context.
    /// </summary>
    /// <param name="result">The current carried result.</param>
    /// <param name="options">The execution options for the pipeline run.</param>
    /// <returns>The step control returned by the step.</returns>
    protected abstract PipelineControl Execute(
        Result result,
        PipelineExecutionOptions options);

    /// <inheritdoc />
    protected sealed override PipelineControl Execute(
        NullPipelineContext context,
        Result result,
        PipelineExecutionOptions options) =>
        this.Execute(result, options);
}

/// <summary>
/// Provides a base class for synchronous pipeline steps with a strongly typed context.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
public abstract class PipelineStep<TContext> : IPipelineStep
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
        ValueTask.FromResult(this.Execute((TContext)context, result, options));

    /// <summary>
    /// Executes the step synchronously with the strongly typed context.
    /// </summary>
    /// <param name="context">The strongly typed pipeline context.</param>
    /// <param name="result">The current carried result.</param>
    /// <param name="options">The execution options for the pipeline run.</param>
    /// <returns>The step control returned by the step.</returns>
    protected abstract PipelineControl Execute(
        TContext context,
        Result result,
        PipelineExecutionOptions options);
}
