// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;

/// <summary>
/// Defines diagnostics produced by <see cref="NotifierSourceGenerator"/>.
/// </summary>
public static class NotifierSourceGeneratorDiagnostics
{
#pragma warning disable RS1032
#pragma warning disable RS2008
    /// <summary>
    /// Gets the shared property-validation diagnostics used by notifier source generation.
    /// </summary>
    public static ValidationGenerationDiagnostics ValidationGeneration => new(
        UnsupportedValidationAttributeUsage,
        InvalidValidationAttributeArguments,
        EachValidationRequiresCollection,
        ConflictingValidationAttributes);

    public static readonly DiagnosticDescriptor EventMustBePartial = new(
        "NTGEN001",
        "Notifier event must be partial",
        "Event '{0}' must be declared as partial to use Notifier source generation",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated events must be partial so the generator can emit additional members.");

    public static readonly DiagnosticDescriptor InvalidEventBaseType = new(
        "NTGEN002",
        "Event base type is incompatible with notifier generation",
        "Event '{0}' must inherit NotificationBase or declare no explicit base type",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The generator can add NotificationBase automatically, but it cannot override an existing incompatible base type.");

    public static readonly DiagnosticDescriptor NestedEventsNotSupported = new(
        "NTGEN003",
        "Nested event types are not supported",
        "Event '{0}' is nested, but nested source-generated events are not supported",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Move the source-generated event to a top-level type for v1.");

    public static readonly DiagnosticDescriptor GenericEventsNotSupported = new(
        "NTGEN004",
        "Generic event types are not supported",
        "Event '{0}' is generic, but generic source-generated events are not supported",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated notifier events are intentionally limited to non-generic types in v1.");

    public static readonly DiagnosticDescriptor MissingHandleMethod = new(
        "NTGEN005",
        "Missing handle method",
        "Event '{0}' must declare at least one [Handle] method",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "At least one developer-authored [Handle] method is required for each source-generated event.");

    public static readonly DiagnosticDescriptor HandleMethodMustBeInstance = new(
        "NTGEN006",
        "Handle method must be an instance method",
        "Handle method '{0}' must be an instance method",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Notifier generator only supports instance [Handle] methods so event members can be accessed directly.");

    public static readonly DiagnosticDescriptor HandleMethodMustNotBeGeneric = new(
        "NTGEN007",
        "Generic handle methods are not supported",
        "Handle method '{0}' must not be generic",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Notifier generator only supports non-generic [Handle] methods.");

    public static readonly DiagnosticDescriptor AsyncVoidHandleNotAllowed = new(
        "NTGEN008",
        "async void handle methods are not supported",
        "Handle method '{0}' must not return async void",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Handle methods must return Result or Task<Result>.");

    public static readonly DiagnosticDescriptor InvalidHandleReturnType = new(
        "NTGEN009",
        "Unsupported handle return type",
        "Handle method '{0}' must return Result or Task<Result>",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only synchronous or asynchronous Result returns are supported.");

    public static readonly DiagnosticDescriptor InvalidHandleParameters = new(
        "NTGEN010",
        "Unsupported handle parameters",
        "Handle method '{0}' has an unsupported parameter shape",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Handle methods must not declare the event type as a parameter, may declare optional PublishOptions and CancellationToken parameters, and any remaining parameters are resolved from DI.");

    public static readonly DiagnosticDescriptor DuplicateValidateMethod = new(
        "NTGEN011",
        "Duplicate validate methods",
        "Event '{0}' declares more than one [Validate] method",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only one [Validate] method is supported per source-generated event.");

    public static readonly DiagnosticDescriptor ValidateMethodMustBeStatic = new(
        "NTGEN012",
        "Validate method must be static",
        "Validate method '{0}' must be static",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Notifier generator only supports static [Validate] methods.");

    public static readonly DiagnosticDescriptor InvalidValidateReturnType = new(
        "NTGEN013",
        "Invalid validate return type",
        "Validate method '{0}' must return void",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Validate methods must configure rules on an InlineValidator<TEvent> and not return a value.");

    public static readonly DiagnosticDescriptor InvalidValidateParameter = new(
        "NTGEN014",
        "Invalid validate parameter type",
        "Validate method '{0}' must declare exactly one InlineValidator<{1}> parameter",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Notifier generator only supports void Validate(InlineValidator<TEvent> validator).");

    public static readonly DiagnosticDescriptor GeneratedNameCollision = new(
        "NTGEN015",
        "Generated member name collision",
        "Event '{0}' already declares a member or type named '{1}', which conflicts with generated Notifier code",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Rename the authored member, avoid overloaded [Handle] method names, or use a different event type name so the generator can emit its bridge, helper, or handler members.");

    public static readonly DiagnosticDescriptor InvalidAttributedType = new(
        "NTGEN016",
        "Invalid source-generated event type",
        "Event '{0}' must be a non-static, non-abstract class",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated notifier events must be concrete classes.");

    public static readonly DiagnosticDescriptor UnsupportedValidationAttributeUsage = new(
        "NTGEN017",
        "Validation attribute usage is not supported",
        "Validation attribute '{0}' on property '{1}' is not supported for type '{2}'. Use [Validate] on event '{3}' for advanced validation.",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Simple property validation attributes only support a conservative set of property and collection-element validation scenarios.");

    public static readonly DiagnosticDescriptor InvalidValidationAttributeArguments = new(
        "NTGEN018",
        "Validation attribute arguments are invalid",
        "Validation attribute '{0}' on property '{1}' has invalid arguments",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Validation attributes must provide the argument shape expected by the generated FluentValidation mapping.");

    public static readonly DiagnosticDescriptor EachValidationRequiresCollection = new(
        "NTGEN019",
        "Element validation requires a collection property",
        "Validation attribute '{0}' on property '{1}' requires a collection property",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "RuleForEach-based validation attributes can only be applied to collection properties.");

    public static readonly DiagnosticDescriptor ConflictingValidationAttributes = new(
        "NTGEN020",
        "Validation attributes conflict",
        "Event '{0}' property '{1}' declares conflicting validation attributes '{2}' and '{3}'",
        "Notifier.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Remove the conflicting validation attributes or move the rule into the explicit [Validate] method.");
#pragma warning restore RS2008
#pragma warning restore RS1032
}
