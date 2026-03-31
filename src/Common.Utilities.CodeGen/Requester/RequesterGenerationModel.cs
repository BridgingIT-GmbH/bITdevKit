// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Identifies whether a source-generated Requester artifact is a command or a query.
/// </summary>
public enum RequesterRequestKind
{
    /// <summary>
    /// Represents a source-generated command.
    /// </summary>
    Command,

    /// <summary>
    /// Represents a source-generated query.
    /// </summary>
    Query,
}

/// <summary>
/// Describes how a handle-method parameter is supplied at runtime.
/// </summary>
public enum RequesterParameterKind
{
    /// <summary>
    /// Indicates that the parameter is supplied from the current <see cref="SendOptions"/>.
    /// </summary>
    SendOptions,

    /// <summary>
    /// Indicates that the parameter is supplied from the current <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    CancellationToken,

    /// <summary>
    /// Indicates that the parameter is resolved from dependency injection.
    /// </summary>
    Service,
}

/// <summary>
/// Represents a classified parameter from a developer-authored Requester handle method.
/// </summary>
public sealed class RequesterParameterModel(IParameterSymbol symbol, RequesterParameterKind kind)
{
    /// <summary>
    /// Gets the original parameter symbol from the authored method.
    /// </summary>
    public IParameterSymbol Symbol { get; } = symbol;

    /// <summary>
    /// Gets how the parameter is supplied to the generated invocation.
    /// </summary>
    public RequesterParameterKind Kind { get; } = kind;
}

/// <summary>
/// Represents the validated handle method selected for a source-generated Requester request.
/// </summary>
public sealed class RequesterHandleMethodModel(
    IMethodSymbol methodSymbol,
    ITypeSymbol responseType,
    bool returnsTask,
    ImmutableArray<RequesterParameterModel> parameters)
{
    /// <summary>
    /// Gets the authored method symbol marked with <c>[Handle]</c>.
    /// </summary>
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;

    /// <summary>
    /// Gets the response type inferred from the authored handle-method return type.
    /// </summary>
    public ITypeSymbol ResponseType { get; } = responseType;

    /// <summary>
    /// Gets a value indicating whether the authored handle method is asynchronous.
    /// </summary>
    public bool ReturnsTask { get; } = returnsTask;

    /// <summary>
    /// Gets the classified parameters used to build the generated invocation.
    /// </summary>
    public ImmutableArray<RequesterParameterModel> Parameters { get; } = parameters;
}

/// <summary>
/// Represents the fully validated semantic model used to emit Requester source-generated code.
/// </summary>
public sealed class RequesterGenerationModel(
    INamedTypeSymbol classSymbol,
    RequesterRequestKind kind,
    ITypeSymbol responseType,
    bool emitRequestBase,
    RequesterHandleMethodModel handleMethod,
    IMethodSymbol validateMethod,
    ImmutableArray<ValidationPropertyRuleModel> propertyValidationRules,
    ImmutableArray<AttributeData> policyAttributes,
    string namespaceName,
    string requestTypeName,
    string generatedHandlerName,
    string accessibilityKeyword)
{
    /// <summary>
    /// Gets the generated bridge method name used to invoke the authored handle method.
    /// </summary>
    public const string HandleBridgeMethodName = "__RequesterGeneratedInvokeHandleAsync";

    /// <summary>
    /// Gets the authored request type symbol.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    /// <summary>
    /// Gets whether the request is generated as a command or a query.
    /// </summary>
    public RequesterRequestKind Kind { get; } = kind;

    /// <summary>
    /// Gets the resolved Requester response type.
    /// </summary>
    public ITypeSymbol ResponseType { get; } = responseType;

    /// <summary>
    /// Gets a value indicating whether the generator must emit <c>RequestBase&lt;TResponse&gt;</c> on the partial request.
    /// </summary>
    public bool EmitRequestBase { get; } = emitRequestBase;

    /// <summary>
    /// Gets the validated handle-method model.
    /// </summary>
    public RequesterHandleMethodModel HandleMethod { get; } = handleMethod;

    /// <summary>
    /// Gets the optional authored validation method.
    /// </summary>
    public IMethodSymbol ValidateMethod { get; } = validateMethod;

    /// <summary>
    /// Gets the generated property-validation rules collected from validation attributes.
    /// </summary>
    public ImmutableArray<ValidationPropertyRuleModel> PropertyValidationRules { get; } = propertyValidationRules;

    /// <summary>
    /// Gets the handler policy attributes that should be copied to the generated handler.
    /// </summary>
    public ImmutableArray<AttributeData> PolicyAttributes { get; } = policyAttributes;

    /// <summary>
    /// Gets the namespace that contains the authored request type.
    /// </summary>
    public string NamespaceName { get; } = namespaceName;

    /// <summary>
    /// Gets the fully qualified request type name used in generated code.
    /// </summary>
    public string RequestTypeName { get; } = requestTypeName;

    /// <summary>
    /// Gets the generated handler type name.
    /// </summary>
    public string GeneratedHandlerName { get; } = generatedHandlerName;

    /// <summary>
    /// Gets the generated accessibility keyword that keeps emitted types visibility-safe.
    /// </summary>
    public string AccessibilityKeyword { get; } = accessibilityKeyword;

    /// <summary>
    /// Gets a value indicating whether a generated validator should be emitted.
    /// </summary>
    public bool HasValidation => this.ValidateMethod is not null || this.PropertyValidationRules.Length > 0;

    /// <summary>
    /// Gets a value indicating whether the resolved response type is <see cref="Unit"/>.
    /// </summary>
    public bool IsUnitResponse =>
        this.ResponseType.Name == "Unit" &&
        this.ResponseType.ContainingNamespace.ToDisplayString() == "BridgingIT.DevKit.Common";
}
