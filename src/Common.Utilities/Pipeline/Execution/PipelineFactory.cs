// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Creates executable pipeline instances from registered pipeline definitions.
/// </summary>
/// <param name="registry">The validated pipeline registry.</param>
/// <param name="runtime">The runtime executor used by created pipeline instances.</param>
public class PipelineFactory(
    PipelineRegistry registry,
    IPipelineRuntime runtime) : IPipelineFactory
{
    /// <inheritdoc />
    public IPipeline Create(string name)
    {
        return new PipelineInstance(registry.GetByName(name), runtime);
    }

    /// <inheritdoc />
    public IPipeline Create<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource
    {
        var definition = registry.GetBySourceType(typeof(TPipelineDefinition));
        if (definition.ContextType != typeof(NullPipelineContext))
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline definition '{typeof(TPipelineDefinition).PrettyName()}' requires context '{definition.ContextType.PrettyName()}'. Use the typed factory overload instead.");
        }

        return new PipelineInstance(definition, runtime);
    }

    /// <inheritdoc />
    public IPipeline<TContext> Create<TContext>(string name)
        where TContext : PipelineContextBase
    {
        var definition = registry.GetByName(name);
        if (definition.ContextType != typeof(TContext))
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline '{name}' expects context '{definition.ContextType.PrettyName()}', not '{typeof(TContext).PrettyName()}'.");
        }

        return new PipelineInstance<TContext>(definition, runtime);
    }

    /// <inheritdoc />
    public IPipeline<TContext> Create<TPipelineDefinition, TContext>()
        where TPipelineDefinition : class, IPipelineDefinitionSource<TContext>
        where TContext : PipelineContextBase
    {
        var definition = registry.GetBySourceType(typeof(TPipelineDefinition));
        if (definition.ContextType != typeof(TContext))
        {
            throw new PipelineDefinitionValidationException(
                $"Pipeline definition '{typeof(TPipelineDefinition).PrettyName()}' expects context '{definition.ContextType.PrettyName()}', not '{typeof(TContext).PrettyName()}'.");
        }

        return new PipelineInstance<TContext>(definition, runtime);
    }
}
