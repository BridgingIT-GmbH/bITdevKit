// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;

/// <summary>
/// Defines diagnostics produced by <see cref="PipelineContextValidationSourceGenerator"/>.
/// </summary>
public static class PipelineContextValidationSourceGeneratorDiagnostics
{
#pragma warning disable RS1032
#pragma warning disable RS2008
    /// <summary>
    /// Gets the shared property-validation diagnostics used by pipeline-context validation generation.
    /// </summary>
    public static ValidationGenerationDiagnostics ValidationGeneration => new(
        UnsupportedValidationAttributeUsage,
        InvalidValidationAttributeArguments,
        EachValidationRequiresCollection,
        ConflictingValidationAttributes);

    public static readonly DiagnosticDescriptor ContextMustBePartial = new(
        "PLNVAL001",
        "Pipeline context must be partial",
        "Pipeline context '{0}' must be declared as partial to use source-generated validation",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Pipeline contexts using source-generated validation must be partial so the generator can emit a nested Validator type.");

    public static readonly DiagnosticDescriptor DuplicateValidateMethod = new(
        "PLNVAL002",
        "Duplicate validate methods",
        "Pipeline context '{0}' declares more than one [Validate] method",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only one [Validate] method is supported per source-generated pipeline context.");

    public static readonly DiagnosticDescriptor ValidateMethodMustBeStatic = new(
        "PLNVAL003",
        "Validate method must be static",
        "Validate method '{0}' must be static",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Pipeline-context validation generation only supports static [Validate] methods.");

    public static readonly DiagnosticDescriptor InvalidValidateReturnType = new(
        "PLNVAL004",
        "Invalid validate return type",
        "Validate method '{0}' must return void",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Validate methods must configure rules on an InlineValidator<TContext> and not return a value.");

    public static readonly DiagnosticDescriptor InvalidValidateParameter = new(
        "PLNVAL005",
        "Invalid validate parameter type",
        "Validate method '{0}' must declare exactly one InlineValidator<{1}> parameter",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Pipeline-context validation generation only supports void Validate(InlineValidator<TContext> validator).");

    public static readonly DiagnosticDescriptor GeneratedNameCollision = new(
        "PLNVAL006",
        "Generated member name collision",
        "Pipeline context '{0}' already declares a member or type named '{1}', which conflicts with generated validation code",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Rename the authored member or nested type so the generator can emit its Validator type.");

    public static readonly DiagnosticDescriptor InvalidAttributedType = new(
        "PLNVAL007",
        "Invalid pipeline context type",
        "Pipeline context '{0}' must be a non-static, non-abstract class deriving from PipelineContextBase",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated pipeline-context validation is limited to concrete classes deriving from PipelineContextBase.");

    public static readonly DiagnosticDescriptor NestedContextsNotSupported = new(
        "PLNVAL008",
        "Nested pipeline contexts are not supported",
        "Pipeline context '{0}' is nested, but nested source-generated pipeline contexts are not supported",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Move the validated pipeline context to a top-level type for v1.");

    public static readonly DiagnosticDescriptor GenericContextsNotSupported = new(
        "PLNVAL009",
        "Generic pipeline contexts are not supported",
        "Pipeline context '{0}' is generic, but generic source-generated pipeline contexts are not supported",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated pipeline-context validation is intentionally limited to non-generic types in v1.");

    public static readonly DiagnosticDescriptor UnsupportedValidationAttributeUsage = new(
        "PLNVAL010",
        "Validation attribute usage is not supported",
        "Validation attribute '{0}' on property '{1}' is not supported for type '{2}'. Use [Validate] on pipeline context '{3}' for advanced validation.",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Simple property validation attributes on pipeline contexts only support a conservative set of property and collection-element validation scenarios.");

    public static readonly DiagnosticDescriptor InvalidValidationAttributeArguments = new(
        "PLNVAL011",
        "Validation attribute arguments are invalid",
        "Validation attribute '{0}' on property '{1}' has invalid arguments",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Validation attributes must provide the argument shape expected by the generated FluentValidation mapping.");

    public static readonly DiagnosticDescriptor EachValidationRequiresCollection = new(
        "PLNVAL012",
        "Element validation requires a collection property",
        "Validation attribute '{0}' on property '{1}' requires a collection property",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "RuleForEach-based validation attributes can only be applied to collection properties.");

    public static readonly DiagnosticDescriptor ConflictingValidationAttributes = new(
        "PLNVAL013",
        "Validation attributes conflict",
        "Pipeline context '{0}' property '{1}' declares conflicting validation attributes '{2}' and '{3}'",
        "Pipelines.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Remove the conflicting validation attributes or move the rule into the explicit [Validate] method.");
#pragma warning restore RS2008
#pragma warning restore RS1032
}
