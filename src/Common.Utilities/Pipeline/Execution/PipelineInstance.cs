// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an executable non-generic pipeline instance.
/// </summary>
/// <param name="definition">The pipeline definition to execute.</param>
/// <param name="runtime">The runtime executor.</param>
public class PipelineInstance(
    IPipelineDefinition definition,
    IPipelineRuntime runtime) : IPipeline
{
    /// <inheritdoc />
    public Task<Result> ExecuteAsync(PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        runtime.ExecuteAsync(definition, new NullPipelineContext(), options ?? new PipelineExecutionOptionsBuilder().Build(), cancellationToken);

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(BuildOptions(configure), cancellationToken);

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(PipelineContextBase context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        runtime.ExecuteAsync(definition, context ?? new NullPipelineContext(), options ?? new PipelineExecutionOptionsBuilder().Build(), cancellationToken);

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(PipelineContextBase context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(context, BuildOptions(configure), cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        runtime.ExecuteAndForgetAsync(definition, new NullPipelineContext(), options ?? new PipelineExecutionOptionsBuilder().Build(), cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        this.ExecuteAndForgetAsync(BuildOptions(configure), cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineContextBase context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        runtime.ExecuteAndForgetAsync(definition, context ?? new NullPipelineContext(), options ?? new PipelineExecutionOptionsBuilder().Build(), cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(PipelineContextBase context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        this.ExecuteAndForgetAsync(context, BuildOptions(configure), cancellationToken);

    private static PipelineExecutionOptions BuildOptions(Action<IPipelineExecutionOptionsBuilder> configure)
    {
        var builder = new PipelineExecutionOptionsBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }
}

/// <summary>
/// Represents an executable typed pipeline instance.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="definition">The pipeline definition to execute.</param>
/// <param name="runtime">The runtime executor.</param>
public class PipelineInstance<TContext>(
    IPipelineDefinition definition,
    IPipelineRuntime runtime) : PipelineInstance(definition, runtime), IPipeline<TContext>
    where TContext : PipelineContextBase
{
    /// <inheritdoc />
    public Task<Result> ExecuteAsync(TContext context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        base.ExecuteAsync(context, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(TContext context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        base.ExecuteAsync(context, configure, cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(TContext context, PipelineExecutionOptions options = null, CancellationToken cancellationToken = default) =>
        base.ExecuteAndForgetAsync(context, options, cancellationToken);

    /// <inheritdoc />
    public Task<PipelineExecutionHandle> ExecuteAndForgetAsync(TContext context, Action<IPipelineExecutionOptionsBuilder> configure, CancellationToken cancellationToken = default) =>
        base.ExecuteAndForgetAsync(context, configure, cancellationToken);
}
