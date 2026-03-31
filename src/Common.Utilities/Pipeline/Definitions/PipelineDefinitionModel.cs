// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an immutable runtime-ready pipeline definition.
/// </summary>
public class PipelineDefinitionModel : IPipelineDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineDefinitionModel"/> class.
    /// </summary>
    /// <param name="name">The pipeline name.</param>
    /// <param name="contextType">The pipeline context type.</param>
    /// <param name="steps">The ordered step definitions.</param>
    /// <param name="hookTypes">The registered hook types.</param>
    /// <param name="behaviorTypes">The registered behavior types.</param>
    public PipelineDefinitionModel(
        string name,
        Type contextType,
        IReadOnlyList<IPipelineStepDefinition> steps,
        IReadOnlyList<Type> hookTypes,
        IReadOnlyList<Type> behaviorTypes)
    {
        this.Name = name;
        this.ContextType = contextType;
        this.Steps = steps;
        this.HookTypes = hookTypes;
        this.BehaviorTypes = behaviorTypes;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Type ContextType { get; }

    /// <inheritdoc />
    public IReadOnlyList<IPipelineStepDefinition> Steps { get; }

    /// <inheritdoc />
    public IReadOnlyList<Type> HookTypes { get; }

    /// <inheritdoc />
    public IReadOnlyList<Type> BehaviorTypes { get; }
}
