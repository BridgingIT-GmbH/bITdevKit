// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;

/// <summary>
/// Bundles the diagnostics used by shared property-validation source generation.
/// </summary>
public sealed class ValidationGenerationDiagnostics(
    DiagnosticDescriptor unsupportedValidationAttributeUsage,
    DiagnosticDescriptor invalidValidationAttributeArguments,
    DiagnosticDescriptor eachValidationRequiresCollection,
    DiagnosticDescriptor conflictingValidationAttributes)
{
    /// <summary>
    /// Gets the diagnostic reported when a validation attribute is used on an unsupported property type.
    /// </summary>
    public DiagnosticDescriptor UnsupportedValidationAttributeUsage { get; } = unsupportedValidationAttributeUsage;

    /// <summary>
    /// Gets the diagnostic reported when a validation attribute constructor argument shape is invalid.
    /// </summary>
    public DiagnosticDescriptor InvalidValidationAttributeArguments { get; } = invalidValidationAttributeArguments;

    /// <summary>
    /// Gets the diagnostic reported when an element-validation attribute is used on a non-collection property.
    /// </summary>
    public DiagnosticDescriptor EachValidationRequiresCollection { get; } = eachValidationRequiresCollection;

    /// <summary>
    /// Gets the diagnostic reported when incompatible validation attributes are combined on the same property.
    /// </summary>
    public DiagnosticDescriptor ConflictingValidationAttributes { get; } = conflictingValidationAttributes;
}
