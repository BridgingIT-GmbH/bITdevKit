// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an immutable runtime-ready pipeline step definition.
/// </summary>
public class PipelineStepDefinitionModel : IPipelineStepDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStepDefinitionModel"/> class.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <param name="sourceKind">The step source kind.</param>
    /// <param name="stepType">The type-backed step type, when applicable.</param>
    /// <param name="inlineStep">The inline step descriptor, when applicable.</param>
    /// <param name="condition">The structural inclusion condition.</param>
    /// <param name="metadata">The arbitrary step metadata.</param>
    public PipelineStepDefinitionModel(
        string name,
        PipelineStepSourceKind sourceKind,
        Type stepType,
        PipelineInlineStepDescriptor inlineStep,
        IPipelineDefinitionCondition condition,
        IReadOnlyDictionary<string, object> metadata)
    {
        this.Name = name;
        this.SourceKind = sourceKind;
        this.StepType = stepType;
        this.InlineStep = inlineStep;
        this.Condition = condition;
        this.Metadata = metadata ?? new Dictionary<string, object>();
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public PipelineStepSourceKind SourceKind { get; }

    /// <inheritdoc />
    public Type StepType { get; }

    /// <inheritdoc />
    public PipelineInlineStepDescriptor InlineStep { get; }

    /// <inheritdoc />
    public IPipelineDefinitionCondition Condition { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
