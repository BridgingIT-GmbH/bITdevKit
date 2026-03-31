// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Represents the validated source-generation model for a pipeline context validator.
/// </summary>
public sealed class PipelineContextValidationGenerationModel(
    INamedTypeSymbol classSymbol,
    IMethodSymbol validateMethod,
    ImmutableArray<ValidationPropertyRuleModel> propertyValidationRules,
    string namespaceName,
    string accessibilityKeyword)
{
    /// <summary>
    /// Gets the authored pipeline context type.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;

    /// <summary>
    /// Gets the optional explicit <c>[Validate]</c> method.
    /// </summary>
    public IMethodSymbol ValidateMethod { get; } = validateMethod;

    /// <summary>
    /// Gets the property-validation rules inferred from validation attributes.
    /// </summary>
    public ImmutableArray<ValidationPropertyRuleModel> PropertyValidationRules { get; } = propertyValidationRules;

    /// <summary>
    /// Gets the namespace of the authored pipeline context.
    /// </summary>
    public string NamespaceName { get; } = namespaceName;

    /// <summary>
    /// Gets the generated accessibility keyword mirrored from the authored pipeline context.
    /// </summary>
    public string AccessibilityKeyword { get; } = accessibilityKeyword;

    /// <summary>
    /// Gets a value indicating whether the generated partial class should also include the <c>sealed</c> modifier.
    /// </summary>
    public bool IsSealed { get; } = classSymbol.IsSealed;

    /// <summary>
    /// Gets the fully qualified pipeline-context type name.
    /// </summary>
    public string ContextTypeName => this.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// Gets a value indicating whether any generated validation metadata exists for the pipeline context.
    /// </summary>
    public bool HasValidation => this.ValidateMethod is not null || this.PropertyValidationRules.Length > 0;
}
