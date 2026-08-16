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
    /// <summary>Requires a non-null value.</summary>
    NotNull,
    /// <summary>Requires a non-empty value.</summary>
    NotEmpty,
    /// <summary>Requires an empty value.</summary>
    Empty,
    /// <summary>Requires a value within an inclusive length range.</summary>
    Length,
    /// <summary>Requires a minimum length.</summary>
    MinLength,
    /// <summary>Requires a maximum length.</summary>
    MaxLength,
    /// <summary>Requires a value greater than a comparison value.</summary>
    GreaterThan,
    /// <summary>Requires a value greater than or equal to a comparison value.</summary>
    GreaterThanOrEqualTo,
    /// <summary>Requires a value less than a comparison value.</summary>
    LessThan,
    /// <summary>Requires a value less than or equal to a comparison value.</summary>
    LessThanOrEqualTo,
    /// <summary>Requires equality with a comparison value.</summary>
    Equal,
    /// <summary>Requires inequality with a comparison value.</summary>
    NotEqual,
    /// <summary>Requires a value within an inclusive range.</summary>
    InclusiveBetween,
    /// <summary>Requires a value within an exclusive range.</summary>
    ExclusiveBetween,
    /// <summary>Requires a non-empty GUID.</summary>
    NotEmptyGuid,
    /// <summary>Requires a GUID that is neither default nor empty.</summary>
    NotDefaultOrEmptyGuid,
    /// <summary>Requires a valid GUID value.</summary>
    ValidGuid,
    /// <summary>Requires an empty GUID.</summary>
    EmptyGuid,
    /// <summary>Allows only a default or empty GUID.</summary>
    DefaultOrEmptyGuid,
    /// <summary>Requires a GUID in a specified format.</summary>
    GuidFormat,
    /// <summary>Requires a valid email address.</summary>
    Email,
    /// <summary>Requires a value matching a regular expression.</summary>
    Matches,
}

/// <summary>
/// Identifies whether a generated validation rule applies to the property itself or to each collection element.
/// </summary>
public enum ValidationRuleTargetKind
{
    /// <summary>Applies the rule to the property value.</summary>
    Property,
    /// <summary>Applies the rule to each element of a collection property.</summary>
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
