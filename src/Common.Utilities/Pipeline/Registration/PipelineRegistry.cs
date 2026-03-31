// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves, normalizes, and validates registered pipeline definitions for runtime lookup.
/// </summary>
public class PipelineRegistry
{
    private readonly IReadOnlyDictionary<string, IPipelineDefinition> definitionsByName;
    private readonly IReadOnlyDictionary<Type, IPipelineDefinition> definitionsBySourceType;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRegistry"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to validate and build definitions.</param>
    /// <param name="state">The additive registration state.</param>
    public PipelineRegistry(IServiceProvider serviceProvider, PipelineRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(state);

        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var definitions = new List<IPipelineDefinition>();
        var sourceMappings = new Dictionary<Type, IPipelineDefinition>();

        foreach (var definition in state.Definitions.SafeNull())
        {
            definitions.Add(this.NormalizeDefinition(definition, scopedProvider));
        }

        foreach (var sourceType in state.DefinitionSourceTypes)
        {
            var source = (IPipelineDefinitionSource)this.CreateComponentInstance(
                scopedProvider,
                sourceType,
                $"pipeline definition source '{sourceType.PrettyName()}'");
            var definition = this.NormalizeDefinition(source.Build(), scopedProvider);

            definitions.Add(definition);
            if (!sourceMappings.ContainsKey(sourceType))
            {
                sourceMappings[sourceType] = definition;
            }
        }

        var duplicate = definitions
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new PipelineDefinitionValidationException(
                $"Duplicate pipeline name '{duplicate.Key}' detected during pipeline registration.");
        }

        this.definitionsByName = definitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        this.definitionsBySourceType = sourceMappings;
    }

    /// <summary>
    /// Gets a registered pipeline definition by name.
    /// </summary>
    /// <param name="name">The logical pipeline name.</param>
    /// <returns>The matching pipeline definition.</returns>
    public IPipelineDefinition GetByName(string name)
    {
        if (this.definitionsByName.TryGetValue(name, out var definition))
        {
            return definition;
        }

        throw new PipelineDefinitionValidationException($"Pipeline '{name}' is not registered.");
    }

    /// <summary>
    /// Gets a registered pipeline definition by packaged definition source type.
    /// </summary>
    /// <param name="sourceType">The packaged pipeline definition source type.</param>
    /// <returns>The matching pipeline definition.</returns>
    public IPipelineDefinition GetBySourceType(Type sourceType)
    {
        if (this.definitionsBySourceType.TryGetValue(sourceType, out var definition))
        {
            return definition;
        }

        throw new PipelineDefinitionValidationException(
            $"Pipeline definition source '{sourceType.PrettyName()}' is not registered.");
    }

    private IPipelineDefinition NormalizeDefinition(IPipelineDefinition definition, IServiceProvider serviceProvider)
    {
        // Validation normalizes type-backed steps to their effective runtime names before duplicate checks.
        var normalizedSteps = definition.Steps.SafeNull()
            .Select(step => this.NormalizeStepDefinition(definition, step, serviceProvider))
            .ToList();

        var duplicateStep = normalizedSteps
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateStep is not null)
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline '{definition.Name}' contains duplicate step name '{duplicateStep.Key}'.");
        }

        foreach (var hookType in definition.HookTypes.SafeNull())
        {
            var hookContextType = PipelineContextTypeResolver.InferHookContextType(hookType);
            if (!PipelineContextTypeResolver.IsCompatible(definition.ContextType, hookContextType))
            {
                throw new PipelineDefinitionValidationException(
                    $"Pipeline '{definition.Name}' cannot use hook '{hookType.PrettyName()}' with context '{hookContextType.PrettyName()}'.");
            }

            _ = this.CreateComponentInstance(serviceProvider, hookType, $"pipeline hook '{hookType.PrettyName()}'");
        }

        foreach (var behaviorType in definition.BehaviorTypes.SafeNull())
        {
            var behaviorContextType = PipelineContextTypeResolver.InferBehaviorContextType(behaviorType);
            if (!PipelineContextTypeResolver.IsCompatible(definition.ContextType, behaviorContextType))
            {
                throw new PipelineDefinitionValidationException(
                    $"Pipeline '{definition.Name}' cannot use behavior '{behaviorType.PrettyName()}' with context '{behaviorContextType.PrettyName()}'.");
            }

            _ = this.CreateComponentInstance(serviceProvider, behaviorType, $"pipeline behavior '{behaviorType.PrettyName()}'");
        }

        return new PipelineDefinitionModel(
            definition.Name,
            definition.ContextType,
            normalizedSteps.AsReadOnly(),
            definition.HookTypes.ToList().AsReadOnly(),
            definition.BehaviorTypes.ToList().AsReadOnly());
    }

    private IPipelineStepDefinition NormalizeStepDefinition(
        IPipelineDefinition definition,
        IPipelineStepDefinition step,
        IServiceProvider serviceProvider)
    {
        if (step.SourceKind == PipelineStepSourceKind.Inline)
        {
            // Inline steps already carry their runtime delegate; validation only checks compatibility.
            if (step.InlineStep is null)
            {
                throw new PipelineDefinitionValidationException(
                    $"Pipeline '{definition.Name}' contains inline step '{step.Name}' without a handler.");
            }

            if (!PipelineContextTypeResolver.IsCompatible(definition.ContextType, step.InlineStep.ContextType))
            {
                throw new PipelineDefinitionValidationException(
                    $"Pipeline '{definition.Name}' cannot use inline step '{step.Name}' with context '{step.InlineStep.ContextType.PrettyName()}'.");
            }

            return step;
        }

        if (step.StepType is null)
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline '{definition.Name}' contains a type-backed step '{step.Name}' without a step type.");
        }

        var stepContextType = PipelineContextTypeResolver.InferStepContextType(step.StepType);
        if (!PipelineContextTypeResolver.IsCompatible(definition.ContextType, stepContextType))
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline '{definition.Name}' cannot use step '{step.StepType.PrettyName()}' with context '{stepContextType.PrettyName()}'.");
        }

        var instance = this.CreateComponentInstance(serviceProvider, step.StepType, $"pipeline step '{step.StepType.PrettyName()}'");
        if (instance is not IPipelineStep pipelineStep)
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline step '{step.StepType.PrettyName()}' must implement '{typeof(IPipelineStep).PrettyName()}'.");
        }

        // Class-based steps can override the convention-based name through their runtime Name property.
        return new PipelineStepDefinitionModel(
            string.IsNullOrWhiteSpace(pipelineStep.Name)
                ? PipelineStepNameConvention.FromType(step.StepType)
                : pipelineStep.Name,
            step.SourceKind,
            step.StepType,
            null,
            step.Condition,
            step.Metadata);
    }

    private object CreateComponentInstance(IServiceProvider serviceProvider, Type type, string displayName)
    {
        try
        {
            return serviceProvider.GetService(type) ?? ActivatorUtilities.CreateInstance(serviceProvider, type);
        }
        catch (Exception ex)
        {
            throw new PipelineDefinitionValidationException(
                $"Unable to create {displayName}. Ensure its constructor dependencies are registered. {ex.GetBaseException().Message}");
        }
    }
}
