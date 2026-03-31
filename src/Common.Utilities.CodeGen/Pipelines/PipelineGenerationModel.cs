// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Represents the validated semantic model used to emit a generated pipeline definition.
/// </summary>
public sealed class PipelineGenerationModel(
    INamedTypeSymbol classSymbol,
    bool isGenericPipeline,
    ITypeSymbol contextType,
    ImmutableArray<PipelineTypeRegistration> hookRegistrations,
    ImmutableArray<PipelineTypeRegistration> behaviorRegistrations,
    ImmutableArray<PipelineStepGenerationModel> steps)
{
    /// <summary>
    /// Gets the authored pipeline type symbol.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    /// <summary>
    /// Gets a value indicating whether the pipeline uses a typed pipeline context.
    /// </summary>
    public bool IsGenericPipeline { get; } = isGenericPipeline;

    /// <summary>
    /// Gets the resolved pipeline context type.
    /// </summary>
    public ITypeSymbol ContextType { get; } = contextType;

    /// <summary>
    /// Gets the generated hook registrations in source order.
    /// </summary>
    public ImmutableArray<PipelineTypeRegistration> HookRegistrations { get; } = hookRegistrations;

    /// <summary>
    /// Gets the generated behavior registrations in source order.
    /// </summary>
    public ImmutableArray<PipelineTypeRegistration> BehaviorRegistrations { get; } = behaviorRegistrations;

    /// <summary>
    /// Gets the validated pipeline steps in execution order.
    /// </summary>
    public ImmutableArray<PipelineStepGenerationModel> Steps { get; } = steps;
}

/// <summary>
/// Represents a generated pipeline hook or behavior registration.
/// </summary>
public sealed class PipelineTypeRegistration(INamedTypeSymbol typeSymbol)
{
    /// <summary>
    /// Gets the registered type symbol.
    /// </summary>
    public INamedTypeSymbol TypeSymbol { get; } = typeSymbol;
}

/// <summary>
/// Describes the normalized return shape of a pipeline step method.
/// </summary>
public enum PipelineStepReturnKind
{
    /// <summary>
    /// The step returns <see langword="void"/>.
    /// </summary>
    Void,

    /// <summary>
    /// The step returns <see cref="System.Threading.Tasks.Task"/>.
    /// </summary>
    Task,

    /// <summary>
    /// The step returns <see cref="Result"/>.
    /// </summary>
    Result,

    /// <summary>
    /// The step returns <c>Task&lt;Result&gt;</c>.
    /// </summary>
    TaskOfResult,

    /// <summary>
    /// The step returns <see cref="PipelineControl"/>.
    /// </summary>
    PipelineControl,

    /// <summary>
    /// The step returns <c>Task&lt;PipelineControl&gt;</c>.
    /// </summary>
    TaskOfPipelineControl,
}

/// <summary>
/// Describes how a pipeline step parameter is supplied at runtime.
/// </summary>
public enum PipelineStepParameterKind
{
    /// <summary>
    /// The parameter receives the current pipeline context.
    /// </summary>
    Context,

    /// <summary>
    /// The parameter receives the current <see cref="Result"/>.
    /// </summary>
    Result,

    /// <summary>
    /// The parameter receives the current cancellation token.
    /// </summary>
    CancellationToken,

    /// <summary>
    /// The parameter is resolved from dependency injection.
    /// </summary>
    Service,
}

/// <summary>
/// Represents a classified parameter from a developer-authored pipeline step method.
/// </summary>
public sealed class PipelineStepParameterModel(IParameterSymbol symbol, PipelineStepParameterKind kind)
{
    /// <summary>
    /// Gets the original parameter symbol from the authored method.
    /// </summary>
    public IParameterSymbol Symbol { get; } = symbol;

    /// <summary>
    /// Gets how the parameter is supplied to the generated step wrapper.
    /// </summary>
    public PipelineStepParameterKind Kind { get; } = kind;
}

/// <summary>
/// Represents a validated pipeline step ready for source emission.
/// </summary>
public sealed class PipelineStepGenerationModel(
    IMethodSymbol methodSymbol,
    int order,
    string stepName,
    string generatedTypeName,
    bool isAsync,
    bool isStatic,
    PipelineStepReturnKind returnKind,
    ImmutableArray<PipelineStepParameterModel> parameters)
{
    /// <summary>
    /// Gets the authored step method symbol.
    /// </summary>
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;

    /// <summary>
    /// Gets the generated execution order.
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// Gets the generated step name.
    /// </summary>
    public string StepName { get; } = stepName;

    /// <summary>
    /// Gets the generated wrapper type name for the step.
    /// </summary>
    public string GeneratedTypeName { get; } = generatedTypeName;

    /// <summary>
    /// Gets a value indicating whether the generated wrapper executes asynchronously.
    /// </summary>
    public bool IsAsync { get; } = isAsync;

    /// <summary>
    /// Gets a value indicating whether the authored step method is static.
    /// </summary>
    public bool IsStatic { get; } = isStatic;

    /// <summary>
    /// Gets the normalized step return kind.
    /// </summary>
    public PipelineStepReturnKind ReturnKind { get; } = returnKind;

    /// <summary>
    /// Gets the classified step parameters used during wrapper emission.
    /// </summary>
    public ImmutableArray<PipelineStepParameterModel> Parameters { get; } = parameters;
}
