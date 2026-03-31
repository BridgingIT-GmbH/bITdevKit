// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the immutable structural blueprint of a pipeline.
/// </summary>
public interface IPipelineDefinition
{
    /// <summary>
    /// Gets the logical pipeline name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the execution context type required by the pipeline.
    /// </summary>
    Type ContextType { get; }

    /// <summary>
    /// Gets the ordered step definitions in execution order.
    /// </summary>
    IReadOnlyList<IPipelineStepDefinition> Steps { get; }

    /// <summary>
    /// Gets the registered pipeline hook types.
    /// </summary>
    IReadOnlyList<Type> HookTypes { get; }

    /// <summary>
    /// Gets the registered pipeline behavior types.
    /// </summary>
    IReadOnlyList<Type> BehaviorTypes { get; }
}

/// <summary>
/// Represents the immutable structural blueprint of one pipeline step.
/// </summary>
public interface IPipelineStepDefinition
{
    /// <summary>
    /// Gets the canonical step name used for logging, tracking, and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the step is backed by a type or by an inline delegate.
    /// </summary>
    PipelineStepSourceKind SourceKind { get; }

    /// <summary>
    /// Gets the step type when the step is type-backed.
    /// </summary>
    Type StepType { get; }

    /// <summary>
    /// Gets the inline step descriptor when the step is delegate-backed.
    /// </summary>
    PipelineInlineStepDescriptor InlineStep { get; }

    /// <summary>
    /// Gets the optional structural condition that controls whether the step is included in the built definition.
    /// </summary>
    IPipelineDefinitionCondition Condition { get; }

    /// <summary>
    /// Gets arbitrary metadata attached to the step definition.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}
