// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Identifies the generated FluentValidation rule emitted for an attributed property.
/// </summary>
public enum ValidationRuleKind
{
    NotNull,
    NotEmpty,
    Empty,
    Length,
    MinLength,
    MaxLength,
    GreaterThan,
    GreaterThanOrEqualTo,
    LessThan,
    LessThanOrEqualTo,
    Equal,
    NotEqual,
    InclusiveBetween,
    ExclusiveBetween,
    NotEmptyGuid,
    NotDefaultOrEmptyGuid,
    ValidGuid,
    EmptyGuid,
    DefaultOrEmptyGuid,
    GuidFormat,
    Email,
    Matches,
}

/// <summary>
/// Identifies whether a generated validation rule applies to the property itself or to each collection element.
/// </summary>
public enum ValidationRuleTargetKind
{
    Property,
    EachElement,
}

/// <summary>
/// Represents one generated FluentValidation rule inferred from a property validation attribute.
/// </summary>
public sealed class ValidationPropertyRuleModel(
    IPropertySymbol propertySymbol,
    ITypeSymbol validatedType,
    ValidationRuleKind kind,
    ValidationRuleTargetKind targetKind,
    ImmutableArray<string> arguments,
    string message,
    string attributeName)
{
    /// <summary>
    /// Gets the property that declared the validation attribute.
    /// </summary>
    public IPropertySymbol PropertySymbol { get; } = propertySymbol;

    /// <summary>
    /// Gets the type validated by the emitted rule.
    /// </summary>
    public ITypeSymbol ValidatedType { get; } = validatedType;

    /// <summary>
    /// Gets the generated FluentValidation rule kind.
    /// </summary>
    public ValidationRuleKind Kind { get; } = kind;

    /// <summary>
    /// Gets whether the rule applies to the property itself or to each collection element.
    /// </summary>
    public ValidationRuleTargetKind TargetKind { get; } = targetKind;

    /// <summary>
    /// Gets the invariant string arguments captured from the attribute.
    /// </summary>
    public ImmutableArray<string> Arguments { get; } = arguments;

    /// <summary>
    /// Gets the optional custom error message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the attribute type name that produced the rule.
    /// </summary>
    public string AttributeName { get; } = attributeName;
}
