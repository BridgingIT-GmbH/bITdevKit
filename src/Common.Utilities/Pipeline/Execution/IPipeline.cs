// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an executable pipeline that may run with or without a shared execution context.
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// Executes the pipeline without an explicit context using the supplied execution options.
    /// </summary>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create("file-cleanup");
    /// var result = await pipeline.ExecuteAsync(new PipelineExecutionOptions
    /// {
    ///     ContinueOnFailure = false
    /// });
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline without an explicit context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var result = await pipeline.ExecuteAsync(
    ///     options => options.ContinueOnFailure().MaxRetryAttemptsPerStep(5),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline with the provided context and execution options.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var context = new OrderImportContext { SourceFileName = "orders.csv" };
    /// var result = await pipeline.ExecuteAsync(context, new PipelineExecutionOptions());
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        PipelineContextBase context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline with the provided context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var result = await pipeline.ExecuteAsync(
    ///     context,
    ///     options => options.WhenCompleted(_ => ValueTask.CompletedTask));
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        PipelineContextBase context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background without an explicit context.
    /// </summary>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(
    ///     new PipelineExecutionOptions
    ///     {
    ///         CompletionCallback = completion => ValueTask.CompletedTask
    ///     });
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background without an explicit context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(
    ///     options => options.WhenCompleted(completion => ValueTask.CompletedTask));
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background with the provided context and execution options.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(context, new PipelineExecutionOptions());
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineContextBase context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background with the provided context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(
    ///     context,
    ///     options => options.WhenCompleted(completion => ValueTask.CompletedTask));
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        PipelineContextBase context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an executable pipeline with a strongly typed execution context.
/// </summary>
public interface IPipeline<TContext> : IPipeline
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Executes the pipeline with the provided typed context and execution options.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create&lt;OrderImportPipeline, OrderImportContext&gt;();
    /// var result = await pipeline.ExecuteAsync(new OrderImportContext(), new PipelineExecutionOptions());
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        TContext context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline with the provided typed context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>The final accumulated <see cref="Result"/> returned by the pipeline.</returns>
    /// <example>
    /// <code>
    /// var result = await pipeline.ExecuteAsync(
    ///     context,
    ///     options => options.ContinueOnFailure());
    /// </code>
    /// </example>
    Task<Result> ExecuteAsync(
        TContext context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background with the provided typed context and execution options.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="options">The execution options that control failure handling, retries, and completion behavior.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(context, new PipelineExecutionOptions());
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        TContext context,
        PipelineExecutionOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the pipeline in the background with the provided typed context by configuring execution options through a builder callback.
    /// </summary>
    /// <param name="context">The execution context shared by the pipeline steps.</param>
    /// <param name="configure">The callback that configures the execution options.</param>
    /// <param name="cancellationToken">The cancellation token for the execution.</param>
    /// <returns>A handle that can be used to query the background execution state.</returns>
    /// <example>
    /// <code>
    /// var handle = await pipeline.ExecuteAndForgetAsync(
    ///     context,
    ///     options => options.WhenCompleted(completion => ValueTask.CompletedTask));
    /// </code>
    /// </example>
    Task<PipelineExecutionHandle> ExecuteAndForgetAsync(
        TContext context,
        Action<IPipelineExecutionOptionsBuilder> configure,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates executable pipelines from registered pipeline definitions.
/// </summary>
public interface IPipelineFactory
{
    /// <summary>
    /// Creates a pipeline by its registered logical name.
    /// </summary>
    /// <param name="name">The registered pipeline name.</param>
    /// <returns>The executable pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create("file-cleanup");
    /// </code>
    /// </example>
    IPipeline Create(string name);

    /// <summary>
    /// Creates a no-context packaged pipeline by its definition type.
    /// </summary>
    /// <typeparam name="TPipelineDefinition">The packaged definition type.</typeparam>
    /// <returns>The executable pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create&lt;FileCleanupPipeline&gt;();
    /// </code>
    /// </example>
    IPipeline Create<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource;

    /// <summary>
    /// Creates a typed pipeline by its registered logical name.
    /// </summary>
    /// <typeparam name="TContext">The execution context type.</typeparam>
    /// <param name="name">The registered pipeline name.</param>
    /// <returns>The executable typed pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create&lt;OrderImportContext&gt;("order-import");
    /// </code>
    /// </example>
    IPipeline<TContext> Create<TContext>(string name)
        where TContext : PipelineContextBase;

    /// <summary>
    /// Creates a typed pipeline by its packaged definition type.
    /// </summary>
    /// <typeparam name="TPipelineDefinition">The packaged definition type.</typeparam>
    /// <typeparam name="TContext">The execution context type.</typeparam>
    /// <returns>The executable typed pipeline.</returns>
    /// <example>
    /// <code>
    /// var pipeline = pipelineFactory.Create&lt;OrderImportPipeline, OrderImportContext&gt;();
    /// </code>
    /// </example>
    IPipeline<TContext> Create<TPipelineDefinition, TContext>()
        where TPipelineDefinition : class, IPipelineDefinitionSource<TContext>
        where TContext : PipelineContextBase;
}

/// <summary>
/// Provides access to background pipeline execution snapshots.
/// </summary>
public interface IPipelineExecutionTracker
{
    /// <summary>
    /// Gets the latest snapshot for a background pipeline execution.
    /// </summary>
    /// <param name="executionId">The background execution identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the lookup.</param>
    /// <returns>The latest execution snapshot, or <see langword="null"/> when the execution is unknown.</returns>
    /// <example>
    /// <code>
    /// var snapshot = await tracker.GetAsync(handle.ExecutionId, cancellationToken);
    /// </code>
    /// </example>
    Task<PipelineExecutionSnapshot> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}
