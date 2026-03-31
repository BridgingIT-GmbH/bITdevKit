// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Builds a no-context pipeline definition.
/// </summary>
/// <param name="name">The mandatory pipeline name.</param>
public class PipelineDefinitionBuilder(string name) : IPipelineDefinitionBuilder
{
    private readonly PipelineDefinitionBuilder<NullPipelineContext> inner = new(name);

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep
    {
        this.inner.AddStep<TStep>(enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddStep<TStep>(
        string name,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep
    {
        this.inner.AddStep<TStep>(name, enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        this.inner.AddStep(step, name, enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        this.inner.AddAsyncStep(step, name, enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        this.inner.AddStep(step, name, enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        this.inner.AddAsyncStep(step, name, enabled, configure);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddHook<THook>(bool enabled = true)
        where THook : class
    {
        this.inner.AddHook<THook>(enabled);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class
    {
        this.inner.AddBehavior<TBehavior>(enabled);
        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinition Build() => this.inner.Build();
}

/// <summary>
/// Builds a context-aware pipeline definition.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="name">The mandatory pipeline name.</param>
public class PipelineDefinitionBuilder<TContext>(string name) : IPipelineDefinitionBuilder<TContext>
    where TContext : PipelineContextBase
{
    private readonly List<IPipelineStepDefinition> steps = [];
    private readonly List<Type> hookTypes = [];
    private readonly List<Type> behaviorTypes = [];
    private readonly PipelineDefinitionContext definitionContext = new();
    private int inlineStepCount;

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep<TStep>(
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep
    {
        return this.AddStep<TStep>(null, enabled, configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep<TStep>(
        string name,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
        where TStep : class, IPipelineStep
    {
        if (!enabled)
        {
            return this;
        }

        this.steps.Add(this.CreateStepDefinition(
            string.IsNullOrWhiteSpace(name)
                ? PipelineStepNameConvention.FromType(typeof(TStep))
                : name,
            PipelineStepSourceKind.Type,
            typeof(TStep),
            null,
            configure));

        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep(
        Action step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(
            new(
                typeof(NullPipelineContext),
                false,
                (Func<IPipelineInlineStepExecution, PipelineControl>)(_ =>
                {
                    step();
                    return PipelineControl.Continue(_.Result);
                })),
            name,
            enabled,
            configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(
            new(
                typeof(NullPipelineContext),
                true,
                (Func<IPipelineInlineStepExecution, Task<PipelineControl>>)(async execution =>
                {
                    await step();
                    return PipelineControl.Continue(execution.Result);
                })),
            name,
            enabled,
            configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep(
        Action<TContext> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(
            new(
                typeof(TContext),
                false,
                (Func<IPipelineInlineStepExecution<TContext>, PipelineControl>)(execution =>
                {
                    step(execution.Context);
                    return PipelineControl.Continue(execution.Result);
                })),
            name,
            enabled,
            configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<TContext, Task> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(
            new(
                typeof(TContext),
                true,
                (Func<IPipelineInlineStepExecution<TContext>, Task<PipelineControl>>)(async execution =>
                {
                    await step(execution.Context);
                    return PipelineControl.Continue(execution.Result);
                })),
            name,
            enabled,
            configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(new(typeof(NullPipelineContext), false, step), name, enabled, configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(new(typeof(NullPipelineContext), true, step), name, enabled, configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddStep(
        Func<IPipelineInlineStepExecution<TContext>, PipelineControl> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(new(typeof(TContext), false, step), name, enabled, configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddAsyncStep(
        Func<IPipelineInlineStepExecution<TContext>, Task<PipelineControl>> step,
        string name = null,
        bool enabled = true,
        Action<IPipelineStepDefinitionBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        return this.AddInlineStep(new(typeof(TContext), true, step), name, enabled, configure);
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddHook<THook>(bool enabled = true)
        where THook : class
    {
        if (enabled)
        {
            this.hookTypes.Add(typeof(THook));
        }

        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinitionBuilder<TContext> AddBehavior<TBehavior>(bool enabled = true)
        where TBehavior : class
    {
        if (enabled)
        {
            this.behaviorTypes.Add(typeof(TBehavior));
        }

        return this;
    }

    /// <inheritdoc />
    public IPipelineDefinition Build()
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PipelineDefinitionValidationException("Pipeline name is mandatory.");
        }

        // Structural conditions decide definition membership up front, not at execution time.
        var includedSteps = this.steps
            .Where(s => s.Condition?.IsSatisfied(this.definitionContext) != false)
            .ToList();

        var duplicateStepName = includedSteps
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateStepName is not null)
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline '{name}' contains duplicate step name '{duplicateStepName.Key}'.");
        }

        return new PipelineDefinitionModel(
            name,
            typeof(TContext),
            includedSteps.AsReadOnly(),
            this.hookTypes.ToList().AsReadOnly(),
            this.behaviorTypes.ToList().AsReadOnly());
    }

    private IPipelineDefinitionBuilder<TContext> AddInlineStep(
        PipelineInlineStepDescriptor descriptor,
        string stepName,
        bool enabled,
        Action<IPipelineStepDefinitionBuilder> configure)
    {
        if (!enabled)
        {
            return this;
        }

        this.steps.Add(this.CreateStepDefinition(
            this.GetInlineStepName(stepName),
            PipelineStepSourceKind.Inline,
            null,
            descriptor,
            configure));

        return this;
    }

    private PipelineStepDefinitionModel CreateStepDefinition(
        string stepName,
        PipelineStepSourceKind sourceKind,
        Type stepType,
        PipelineInlineStepDescriptor inlineStep,
        Action<IPipelineStepDefinitionBuilder> configure)
    {
        var builder = new PipelineStepDefinitionBuilderState();
        configure?.Invoke(builder);

        return new PipelineStepDefinitionModel(
            stepName,
            sourceKind,
            stepType,
            inlineStep,
            builder.Condition,
            new Dictionary<string, object>(builder.Metadata, StringComparer.OrdinalIgnoreCase));
    }

    private string GetInlineStepName(string providedName)
    {
        if (!string.IsNullOrWhiteSpace(providedName))
        {
            return providedName;
        }

        // Generated names stay local to the built definition and only count included inline steps.
        this.inlineStepCount++;
        return $"inline-step-{this.inlineStepCount}";
    }
}
