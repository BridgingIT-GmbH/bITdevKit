// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;

/// <summary>
/// Defines diagnostics produced by <see cref="RequesterSourceGenerator"/>.
/// </summary>
public static class RequesterSourceGeneratorDiagnostics
{
#pragma warning disable RS1032
#pragma warning disable RS2008
    public static readonly DiagnosticDescriptor RequestMustBePartial = new(
        "RQGEN001",
        "Requester request must be partial",
        "Request '{0}' must be declared as partial to use Requester source generation",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated commands and queries must be partial so the generator can emit additional members.");

    public static readonly DiagnosticDescriptor CommandAndQueryConflict = new(
        "RQGEN002",
        "Request cannot be both command and query",
        "Request '{0}' cannot declare both [Command] and [Query]",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Choose either [Command] or [Query] on a single source-generated request type.");

    public static readonly DiagnosticDescriptor InvalidResponseType = new(
        "RQGEN003",
        "Invalid request response type",
        "Request '{0}' has an invalid response type declaration",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Commands and queries may omit the explicit response type and infer it from the handle method. When specified explicitly, the response type must be valid.");

    public static readonly DiagnosticDescriptor QueryUnitNotAllowed = new(
        "RQGEN004",
        "Queries cannot return Unit",
        "Query '{0}' must declare a non-Unit response type",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Queries must return a value and cannot use Unit.");

    public static readonly DiagnosticDescriptor RequestBaseMismatch = new(
        "RQGEN005",
        "Request base type is incompatible with the generated response",
        "Request '{0}' must not declare an incompatible base type; expected RequestBase<{1}> or no explicit base type",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The generator can add RequestBase<TResponse> automatically, but it cannot override an existing incompatible base type.");

    public static readonly DiagnosticDescriptor NestedRequestsNotSupported = new(
        "RQGEN006",
        "Nested request types are not supported",
        "Request '{0}' is nested, but nested source-generated requests are not supported",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Move the source-generated request to a top-level type for v1.");

    public static readonly DiagnosticDescriptor GenericRequestsNotSupported = new(
        "RQGEN007",
        "Generic request types are not supported",
        "Request '{0}' is generic, but generic source-generated requests are not supported",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated Requester requests are intentionally limited to non-generic types in v1.");

    public static readonly DiagnosticDescriptor MissingHandleMethod = new(
        "RQGEN008",
        "Missing handle method",
        "Request '{0}' must declare exactly one [Handle] method",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "A single developer-authored [Handle] method is required for each source-generated request.");

    public static readonly DiagnosticDescriptor DuplicateHandleMethod = new(
        "RQGEN009",
        "Duplicate handle methods",
        "Request '{0}' declares more than one [Handle] method",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only one [Handle] method is supported per source-generated request.");

    public static readonly DiagnosticDescriptor HandleMethodMustBeInstance = new(
        "RQGEN010",
        "Handle method must be an instance method",
        "Handle method '{0}' must be an instance method",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Requester generator only supports instance [Handle] methods so request members can be accessed directly.");

    public static readonly DiagnosticDescriptor HandleMethodMustNotBeGeneric = new(
        "RQGEN011",
        "Generic handle methods are not supported",
        "Handle method '{0}' must not be generic",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Requester generator only supports non-generic [Handle] methods.");

    public static readonly DiagnosticDescriptor AsyncVoidHandleNotAllowed = new(
        "RQGEN012",
        "async void handle methods are not supported",
        "Handle method '{0}' must not return async void",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Handle methods must return Result<TResponse> or Task<Result<TResponse>>.");

    public static readonly DiagnosticDescriptor InvalidHandleReturnType = new(
        "RQGEN013",
        "Unsupported handle return type",
        "Handle method '{0}' must return Result<{1}> or Task<Result<{1}>>",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only synchronous or asynchronous Result<TResponse> returns are supported.");

    public static readonly DiagnosticDescriptor InvalidHandleParameters = new(
        "RQGEN014",
        "Unsupported handle parameters",
        "Handle method '{0}' has an unsupported parameter shape",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Handle methods must not declare the request type as a parameter, may declare optional SendOptions and CancellationToken parameters, and any remaining parameters are resolved from DI.");

    public static readonly DiagnosticDescriptor DuplicateValidateMethod = new(
        "RQGEN015",
        "Duplicate validate methods",
        "Request '{0}' declares more than one [Validate] method",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Only one [Validate] method is supported per source-generated request.");

    public static readonly DiagnosticDescriptor ValidateMethodMustBeStatic = new(
        "RQGEN016",
        "Validate method must be static",
        "Validate method '{0}' must be static",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Requester generator only supports static [Validate] methods.");

    public static readonly DiagnosticDescriptor InvalidValidateReturnType = new(
        "RQGEN017",
        "Invalid validate return type",
        "Validate method '{0}' must return void",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Validate methods must configure rules on an InlineValidator<TRequest> and not return a value.");

    public static readonly DiagnosticDescriptor InvalidValidateParameter = new(
        "RQGEN018",
        "Invalid validate parameter type",
        "Validate method '{0}' must declare exactly one InlineValidator<{1}> parameter",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "The v1 Requester generator only supports void Validate(InlineValidator<TRequest> validator).");

    public static readonly DiagnosticDescriptor GeneratedNameCollision = new(
        "RQGEN019",
        "Generated member name collision",
        "Request '{0}' already declares a member or type named '{1}', which conflicts with generated Requester code",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Rename the authored member or use a different request type name so the generator can emit its bridge, helper, or handler members.");

    public static readonly DiagnosticDescriptor InvalidAttributedType = new(
        "RQGEN020",
        "Invalid source-generated request type",
        "Request '{0}' must be a non-static, non-abstract class",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "Source-generated Requester requests must be concrete classes.");

    public static readonly DiagnosticDescriptor ExplicitResponseTypeMismatch = new(
        "RQGEN021",
        "Explicit response type does not match handle return type",
        "Request '{0}' declares response type '{1}', but handle method '{2}' returns '{3}'",
        "Requester.CodeGen",
        DiagnosticSeverity.Error,
        true,
        description: "When [Command(typeof(TResponse))] or [Query(typeof(TResponse))] specifies an explicit response type, it must match the Result<TResponse> returned by the handle method.");
#pragma warning restore RS2008
#pragma warning restore RS1032
}
