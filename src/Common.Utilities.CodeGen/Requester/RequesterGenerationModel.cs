// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

public enum RequesterRequestKind
{
    Command,
    Query,
}

public enum RequesterParameterKind
{
    SendOptions,
    CancellationToken,
    Service,
}

public sealed class RequesterParameterModel(IParameterSymbol symbol, RequesterParameterKind kind)
{
    public IParameterSymbol Symbol { get; } = symbol;

    public RequesterParameterKind Kind { get; } = kind;
}

public sealed class RequesterHandleMethodModel(
    IMethodSymbol methodSymbol,
    ITypeSymbol responseType,
    bool returnsTask,
    ImmutableArray<RequesterParameterModel> parameters)
{
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;

    public ITypeSymbol ResponseType { get; } = responseType;

    public bool ReturnsTask { get; } = returnsTask;

    public ImmutableArray<RequesterParameterModel> Parameters { get; } = parameters;
}

public sealed class RequesterGenerationModel(
    INamedTypeSymbol classSymbol,
    RequesterRequestKind kind,
    ITypeSymbol responseType,
    bool emitRequestBase,
    RequesterHandleMethodModel handleMethod,
    IMethodSymbol validateMethod,
    ImmutableArray<AttributeData> policyAttributes,
    string namespaceName,
    string requestTypeName,
    string generatedHandlerName,
    string accessibilityKeyword)
{
    public const string HandleBridgeMethodName = "__RequesterGeneratedInvokeHandleAsync";

    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    public RequesterRequestKind Kind { get; } = kind;

    public ITypeSymbol ResponseType { get; } = responseType;

    public bool EmitRequestBase { get; } = emitRequestBase;

    public RequesterHandleMethodModel HandleMethod { get; } = handleMethod;

    public IMethodSymbol ValidateMethod { get; } = validateMethod;

    public ImmutableArray<AttributeData> PolicyAttributes { get; } = policyAttributes;

    public string NamespaceName { get; } = namespaceName;

    public string RequestTypeName { get; } = requestTypeName;

    public string GeneratedHandlerName { get; } = generatedHandlerName;

    public string AccessibilityKeyword { get; } = accessibilityKeyword;

    public bool HasValidation => this.ValidateMethod is not null;

    public bool IsUnitResponse =>
        this.ResponseType.Name == "Unit" &&
        this.ResponseType.ContainingNamespace.ToDisplayString() == "BridgingIT.DevKit.Common";
}
