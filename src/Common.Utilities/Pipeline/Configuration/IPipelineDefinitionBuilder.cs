// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a fluent API for building a no-context pipeline definition.
/// </summary>
public interface IPipelineDefinitionBuilder
{
    /// <summary>
    /// Adds a class-based step to the pipeline definition.
    /// </summary>
    /// <typeparam name="TStep">The step type to add.</typeparam>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep&lt;ValidateOrderImportStep&gt;(enabled: isValidationEnabled);
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep;

    /// <summary>
    /// Adds a synchronous inline no-context step to the pipeline definition.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(() => Console.WriteLine("hello"));
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds an asynchronous inline no-context step to the pipeline definition.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddAsyncStep(async () => await Task.Yield());
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds a synchronous inline step with full access to pipeline execution services and the carried result.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(execution => execution.Continue(execution.Result.WithMessage("done")));
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds an asynchronous inline step with full access to pipeline execution services and the carried result.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddAsyncStep(async execution =>
    /// {
    ///     await Task.Yield();
    ///     return execution.Continue();
    /// });
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds a pipeline hook to the definition.
    /// </summary>
    /// <typeparam name="THook">The hook type to add.</typeparam>
    /// <param name="enabled">A value indicating whether the hook should be added to the built definition.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddHook&lt;PipelineAuditHook&gt;();
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddHook<THook>(bool enabled = true)
        where THook : class;

    /// <summary>
    /// Adds a pipeline behavior to the definition.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type to add.</typeparam>
    /// <param name="enabled">A value indicating whether the behavior should be added to the built definition.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddBehavior&lt;PipelineTracingBehavior&gt;();
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class;

    /// <summary>
    /// Builds the immutable pipeline definition.
    /// </summary>
    /// <returns>The built pipeline definition.</returns>
    /// <example>
    /// <code>
    /// var definition = new PipelineDefinitionBuilder("file-cleanup")
    ///     .AddStep(() => { })
    ///     .Build();
    /// </code>
    /// </example>
    IPipelineDefinition Build();
}

/// <summary>
/// Provides a fluent API for building a context-aware pipeline definition.
/// </summary>
public interface IPipelineDefinitionBuilder<TContext>
    where TContext : PipelineContextBase
{
    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddStep{TStep}(bool, Action{IPipelineStepDefinitionBuilder})"/>
    IPipelineDefinitionBuilder<TContext> AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep;

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddStep(Action, string, bool, Action{IPipelineStepDefinitionBuilder})"/>
    IPipelineDefinitionBuilder<TContext> AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddAsyncStep(Func{Task}, string, bool, Action{IPipelineStepDefinitionBuilder})"/>
    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds a synchronous inline step that receives the typed execution context.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(context => context.IsValidated = true);
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder<TContext> AddStep(
        Action<TContext> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds an asynchronous inline step that receives the typed execution context.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddAsyncStep(async context => await repository.LoadAsync(context.SourceFileName));
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<TContext, Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddStep(Func{IPipelineInlineStepExecution, PipelineControl}, string, bool, Action{IPipelineStepDefinitionBuilder})"/>
    IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddAsyncStep(Func{IPipelineInlineStepExecution, Task{PipelineControl}}, string, bool, Action{IPipelineStepDefinitionBuilder})"/>
    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds a synchronous inline step with full access to the typed context, carried result, and scoped services.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddStep(execution => execution.Continue(execution.Result.WithMessage(execution.Context.SourceFileName)));
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution<TContext>, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <summary>
    /// Adds an asynchronous inline step with full access to the typed context, carried result, and scoped services.
    /// </summary>
    /// <param name="step">The inline delegate to execute.</param>
    /// <param name="name">An optional explicit step name. When omitted, an inline-step name is generated.</param>
    /// <param name="enabled">A value indicating whether the step should be added to the built definition.</param>
    /// <param name="configure">Optional step-definition customization.</param>
    /// <returns>The same builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddAsyncStep(async execution =>
    /// {
    ///     await Task.Yield();
    ///     return execution.Continue();
    /// });
    /// </code>
    /// </example>
    IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution<TContext>, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null);

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddHook{THook}(bool)"/>
    IPipelineDefinitionBuilder<TContext> AddHook<THook>(bool enabled = true)
        where THook : class;

    /// <inheritdoc cref="IPipelineDefinitionBuilder.AddBehavior{TBehavior}(bool)"/>
    IPipelineDefinitionBuilder<TContext> AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class;

    /// <inheritdoc cref="IPipelineDefinitionBuilder.Build"/>
    IPipelineDefinition Build();
}
