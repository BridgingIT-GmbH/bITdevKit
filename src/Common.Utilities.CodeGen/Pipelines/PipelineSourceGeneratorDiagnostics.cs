// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;

/// <summary>
/// Defines diagnostics produced by <see cref="PipelineSourceGenerator"/>.
/// </summary>
public static class PipelineSourceGeneratorDiagnostics
{
#pragma warning disable RS1032
#pragma warning disable RS2008
    public static readonly DiagnosticDescriptor PipelineMustBePartial = new(
        "PLNGEN001",
        "Pipeline must be partial",
        "Pipeline '{0}' must be declared as partial to use [Pipeline]",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor PipelineMustNotDeclareBaseType = new(
        "PLNGEN002",
        "Pipeline must not declare an explicit base type",
        "Pipeline '{0}' must not declare an explicit base type to use [Pipeline]; the generator supplies PipelineDefinition",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidPipelineContextType = new(
        "PLNGEN003",
        "Pipeline context type is invalid",
        "Pipeline '{0}' declares context '{1}' in [Pipeline], but it must derive from PipelineContextBase",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor PipelineContextMismatch = new(
        "PLNGEN004",
        "Pipeline context mismatch",
        "Pipeline '{0}' declares context '{1}' in [Pipeline] but inherits '{2}'",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ManualConfigureNotAllowed = new(
        "PLNGEN005",
        "Generated pipelines must not implement Configure manually",
        "Pipeline '{0}' uses [Pipeline] and must not declare its own Configure(...) implementation",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor StepMethodMustNotBeGeneric = new(
        "PLNGEN006",
        "Pipeline step method must not be generic",
        "Pipeline step method '{0}' must not be generic",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor StepMethodSignatureNotSupported = new(
        "PLNGEN007",
        "Unsupported pipeline step method signature",
        "Pipeline step method '{0}' has an unsupported signature. Supported return types are void, Task, Result, Task<Result>, PipelineControl, and Task<PipelineControl>",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor AsyncVoidNotAllowed = new(
        "PLNGEN008",
        "async void pipeline steps are not supported",
        "Pipeline step method '{0}' must not return async void",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor DuplicateStepOrder = new(
        "PLNGEN009",
        "Duplicate pipeline step order",
        "Pipeline '{0}' contains duplicate generated step order '{1}'",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor DuplicateStepName = new(
        "PLNGEN010",
        "Duplicate pipeline step name",
        "Pipeline '{0}' contains duplicate generated step name '{1}'",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidHookType = new(
        "PLNGEN011",
        "Invalid pipeline hook type",
        "Type '{0}' is not a compatible pipeline hook for pipeline '{1}'",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor InvalidBehaviorType = new(
        "PLNGEN012",
        "Invalid pipeline behavior type",
        "Type '{0}' is not a compatible pipeline behavior for pipeline '{1}'",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ContextParameterNotAllowed = new(
        "PLNGEN013",
        "No-context pipeline step must not declare a context parameter",
        "Pipeline step method '{0}' belongs to a no-context pipeline and must not declare a pipeline context parameter",
        "Pipeline.CodeGen",
        DiagnosticSeverity.Error,
        true);
#pragma warning restore RS2008
#pragma warning restore RS1032
}
