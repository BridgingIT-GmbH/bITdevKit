// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Describes how a handle-method parameter is supplied at runtime for source-generated notifier events.
/// </summary>
public enum NotifierParameterKind
{
    /// <summary>
    /// Indicates that the parameter is supplied from the current <see cref="PublishOptions"/>.
    /// </summary>
    PublishOptions,

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
/// Represents a classified parameter from a developer-authored notifier handle method.
/// </summary>
public sealed class NotifierParameterModel(IParameterSymbol symbol, NotifierParameterKind kind)
{
    /// <summary>
    /// Gets the original parameter symbol from the authored method.
    /// </summary>
    public IParameterSymbol Symbol { get; } = symbol;

    /// <summary>
    /// Gets how the parameter is supplied to the generated invocation.
    /// </summary>
    public NotifierParameterKind Kind { get; } = kind;
}

/// <summary>
/// Represents the validated handle method selected for a source-generated notifier event.
/// </summary>
public sealed class NotifierHandleMethodModel(
    IMethodSymbol methodSymbol,
    bool returnsTask,
    ImmutableArray<NotifierParameterModel> parameters,
    string bridgeMethodName,
    string generatedHandlerName)
{
    /// <summary>
    /// Gets the authored method symbol marked with <c>[Handle]</c>.
    /// </summary>
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;

    /// <summary>
    /// Gets a value indicating whether the authored handle method is asynchronous.
    /// </summary>
    public bool ReturnsTask { get; } = returnsTask;

    /// <summary>
    /// Gets the classified parameters used to build the generated invocation.
    /// </summary>
    public ImmutableArray<NotifierParameterModel> Parameters { get; } = parameters;

    /// <summary>
    /// Gets the generated bridge method name used to invoke the authored handle method.
    /// </summary>
    public string BridgeMethodName { get; } = bridgeMethodName;

    /// <summary>
    /// Gets the generated handler type name.
    /// </summary>
    public string GeneratedHandlerName { get; } = generatedHandlerName;
}

/// <summary>
/// Represents the fully validated semantic model used to emit notifier source-generated code.
/// </summary>
public sealed class NotifierGenerationModel(
    INamedTypeSymbol classSymbol,
    bool emitNotificationBase,
    ImmutableArray<NotifierHandleMethodModel> handleMethods,
    IMethodSymbol validateMethod,
    ImmutableArray<ValidationPropertyRuleModel> propertyValidationRules,
    ImmutableArray<AttributeData> policyAttributes,
    string namespaceName,
    string eventTypeName,
    string accessibilityKeyword)
{
    /// <summary>
    /// Gets the authored event type symbol.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    /// <summary>
    /// Gets a value indicating whether the generator must emit <c>NotificationBase</c> on the partial event.
    /// </summary>
    public bool EmitNotificationBase { get; } = emitNotificationBase;

    /// <summary>
    /// Gets the validated handle-method models in source order.
    /// </summary>
    public ImmutableArray<NotifierHandleMethodModel> HandleMethods { get; } = handleMethods;

    /// <summary>
    /// Gets the optional authored validation method.
    /// </summary>
    public IMethodSymbol ValidateMethod { get; } = validateMethod;

    /// <summary>
    /// Gets the generated property-validation rules collected from validation attributes.
    /// </summary>
    public ImmutableArray<ValidationPropertyRuleModel> PropertyValidationRules { get; } = propertyValidationRules;

    /// <summary>
    /// Gets the handler policy attributes that should be copied to generated handlers.
    /// </summary>
    public ImmutableArray<AttributeData> PolicyAttributes { get; } = policyAttributes;

    /// <summary>
    /// Gets the namespace that contains the authored event type.
    /// </summary>
    public string NamespaceName { get; } = namespaceName;

    /// <summary>
    /// Gets the fully qualified event type name used in generated code.
    /// </summary>
    public string EventTypeName { get; } = eventTypeName;

    /// <summary>
    /// Gets the generated accessibility keyword that keeps emitted types visibility-safe.
    /// </summary>
    public string AccessibilityKeyword { get; } = accessibilityKeyword;

    /// <summary>
    /// Gets a value indicating whether a generated validator should be emitted.
    /// </summary>
    public bool HasValidation => this.ValidateMethod is not null || this.PropertyValidationRules.Length > 0;
}
