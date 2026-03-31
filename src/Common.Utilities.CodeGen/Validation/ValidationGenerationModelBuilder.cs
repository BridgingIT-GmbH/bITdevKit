// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

/// <summary>
/// Builds normalized validation-rule models from reusable property validation attributes.
/// </summary>
public static class ValidationGenerationModelBuilder
{
    private static readonly Dictionary<string, ValidationRuleMetadata> SupportedAttributes =
        new Dictionary<string, ValidationRuleMetadata>(StringComparer.Ordinal)
        {
            ["BridgingIT.DevKit.Common.ValidateNotNullAttribute"] = new(ValidationRuleKind.NotNull, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateNotEmptyAttribute"] = new(ValidationRuleKind.NotEmpty, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateEmptyAttribute"] = new(ValidationRuleKind.Empty, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateLengthAttribute"] = new(ValidationRuleKind.Length, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateMinLengthAttribute"] = new(ValidationRuleKind.MinLength, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateMaxLengthAttribute"] = new(ValidationRuleKind.MaxLength, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateGreaterThanAttribute"] = new(ValidationRuleKind.GreaterThan, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateGreaterThanOrEqualToAttribute"] = new(ValidationRuleKind.GreaterThanOrEqualTo, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateLessThanAttribute"] = new(ValidationRuleKind.LessThan, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateLessThanOrEqualToAttribute"] = new(ValidationRuleKind.LessThanOrEqualTo, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateEqualAttribute"] = new(ValidationRuleKind.Equal, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateNotEqualAttribute"] = new(ValidationRuleKind.NotEqual, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateInclusiveBetweenAttribute"] = new(ValidationRuleKind.InclusiveBetween, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateExclusiveBetweenAttribute"] = new(ValidationRuleKind.ExclusiveBetween, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateNotEmptyGuidAttribute"] = new(ValidationRuleKind.NotEmptyGuid, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateNotDefaultOrEmptyGuidAttribute"] = new(ValidationRuleKind.NotDefaultOrEmptyGuid, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateValidGuidAttribute"] = new(ValidationRuleKind.ValidGuid, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateEmptyGuidAttribute"] = new(ValidationRuleKind.EmptyGuid, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateDefaultOrEmptyGuidAttribute"] = new(ValidationRuleKind.DefaultOrEmptyGuid, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateGuidFormatAttribute"] = new(ValidationRuleKind.GuidFormat, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateEmailAttribute"] = new(ValidationRuleKind.Email, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateMatchesAttribute"] = new(ValidationRuleKind.Matches, ValidationRuleTargetKind.Property),
            ["BridgingIT.DevKit.Common.ValidateEachNotNullAttribute"] = new(ValidationRuleKind.NotNull, ValidationRuleTargetKind.EachElement),
            ["BridgingIT.DevKit.Common.ValidateEachNotEmptyAttribute"] = new(ValidationRuleKind.NotEmpty, ValidationRuleTargetKind.EachElement),
        };

    /// <summary>
    /// Collects validation-rule models for the properties declared on a source-generated request type.
    /// </summary>
    /// <param name="context">The source-production context used to report diagnostics.</param>
    /// <param name="classSymbol">The request type being analyzed.</param>
    /// <param name="rules">The collected validation rules when successful.</param>
    /// <returns><see langword="true"/> when validation metadata was collected successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        out ImmutableArray<ValidationPropertyRuleModel> rules)
    {
        var builder = ImmutableArray.CreateBuilder<ValidationPropertyRuleModel>();

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>()
                     .Where(static property => !property.IsStatic && !property.IsIndexer))
        {
            foreach (var attribute in property.GetAttributes().OrderBy(static attribute => attribute.ApplicationSyntaxReference?.Span.Start ?? int.MaxValue))
            {
                if (!SupportedAttributes.TryGetValue(attribute.AttributeClass?.ToDisplayString(), out var metadata))
                {
                    continue;
                }

                if (!TryCreateRule(context, classSymbol, property, attribute, metadata, out var rule))
                {
                    rules = [];
                    return false;
                }

                builder.Add(rule);
            }

            if (!TryValidateRuleConflicts(context, classSymbol, property, builder))
            {
                rules = [];
                return false;
            }
        }

        rules = builder.ToImmutable();
        return true;
    }

    private static bool TryCreateRule(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        IPropertySymbol property,
        AttributeData attribute,
        ValidationRuleMetadata metadata,
        out ValidationPropertyRuleModel rule)
    {
        rule = null;
        var attributeName = attribute.AttributeClass.Name;
        var propertyName = property.Name;
        var validatedType = property.Type;

        if (metadata.TargetKind == ValidationRuleTargetKind.EachElement)
        {
            if (!TryGetEnumerableElementType(property.Type, out validatedType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.EachValidationRequiresCollection,
                    attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? property.Locations.FirstOrDefault(),
                    attributeName,
                    propertyName));
                return false;
            }
        }

        var arguments = GetArguments(attribute);
        var message = GetOptionalMessage(attribute, metadata.Kind, arguments);

        if (!TryValidateAttributeArguments(context, attribute, property, arguments, message, metadata.Kind, attributeName))
        {
            return false;
        }

        if (!TryValidateTargetType(context, classSymbol, property, validatedType, metadata.Kind, metadata.TargetKind, attributeName))
        {
            return false;
        }

        rule = new ValidationPropertyRuleModel(
            property,
            validatedType,
            metadata.Kind,
            metadata.TargetKind,
            arguments,
            message,
            attributeName);
        return true;
    }

    private static ImmutableArray<string> GetArguments(AttributeData attribute)
    {
        return attribute.ConstructorArguments
            .Select(static argument => argument.Value switch
            {
                null => null,
                string value => value,
                char value => value.ToString(CultureInfo.InvariantCulture),
                bool value => value ? "true" : "false",
                _ => Convert.ToString(argument.Value, CultureInfo.InvariantCulture),
            })
            .ToImmutableArray();
    }

    private static string GetOptionalMessage(AttributeData attribute, ValidationRuleKind kind, ImmutableArray<string> arguments)
    {
        return kind switch
        {
            ValidationRuleKind.NotNull or ValidationRuleKind.NotEmpty or ValidationRuleKind.Empty or ValidationRuleKind.NotEmptyGuid or ValidationRuleKind.NotDefaultOrEmptyGuid or ValidationRuleKind.ValidGuid or ValidationRuleKind.EmptyGuid or ValidationRuleKind.DefaultOrEmptyGuid or ValidationRuleKind.GuidFormat or ValidationRuleKind.Email
                => arguments.Length == 1 ? arguments[0] : null,
            ValidationRuleKind.Length
                => arguments.Length == 3 ? arguments[2] : null,
            ValidationRuleKind.MinLength or ValidationRuleKind.MaxLength or ValidationRuleKind.Matches or
            ValidationRuleKind.GreaterThan or ValidationRuleKind.GreaterThanOrEqualTo or ValidationRuleKind.LessThan or
            ValidationRuleKind.LessThanOrEqualTo or ValidationRuleKind.Equal or ValidationRuleKind.NotEqual
                => arguments.Length == 2 ? arguments[1] : null,
            ValidationRuleKind.InclusiveBetween or ValidationRuleKind.ExclusiveBetween
                => arguments.Length == 3 ? arguments[2] : null,
            _ => null,
        };
    }

    private static bool TryValidateAttributeArguments(
        SourceProductionContext context,
        AttributeData attribute,
        IPropertySymbol property,
        ImmutableArray<string> arguments,
        string message,
        ValidationRuleKind kind,
        string attributeName)
    {
        var syntaxLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? property.Locations.FirstOrDefault();

        static bool HasInvalidString(string value) => string.IsNullOrWhiteSpace(value);

        var hasInvalidArguments = kind switch
        {
            ValidationRuleKind.Length => arguments.Length is not 2 and not 3 || !int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !int.TryParse(arguments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ValidationRuleKind.MinLength or ValidationRuleKind.MaxLength => arguments.Length is not 1 and not 2 || !int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ValidationRuleKind.Matches => arguments.Length is not 1 and not 2 || HasInvalidString(arguments[0]),
            ValidationRuleKind.GreaterThan or ValidationRuleKind.GreaterThanOrEqualTo or ValidationRuleKind.LessThan or ValidationRuleKind.LessThanOrEqualTo or ValidationRuleKind.Equal or ValidationRuleKind.NotEqual
                => arguments.Length is not 1 and not 2 || HasInvalidString(arguments[0]),
            ValidationRuleKind.InclusiveBetween or ValidationRuleKind.ExclusiveBetween
                => arguments.Length is not 2 and not 3 || HasInvalidString(arguments[0]) || HasInvalidString(arguments[1]),
            _ => arguments.Length > 1 || (arguments.Length == 1 && kind is not (ValidationRuleKind.NotNull or ValidationRuleKind.NotEmpty or ValidationRuleKind.Empty or ValidationRuleKind.NotEmptyGuid or ValidationRuleKind.NotDefaultOrEmptyGuid or ValidationRuleKind.ValidGuid or ValidationRuleKind.EmptyGuid or ValidationRuleKind.DefaultOrEmptyGuid or ValidationRuleKind.GuidFormat or ValidationRuleKind.Email)),
        };

        if (!hasInvalidArguments && message is not null && string.IsNullOrWhiteSpace(message))
        {
            hasInvalidArguments = true;
        }

        if (!hasInvalidArguments)
        {
            return true;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            RequesterSourceGeneratorDiagnostics.InvalidValidationAttributeArguments,
            syntaxLocation,
            attributeName,
            property.Name));
        return false;
    }

    private static bool TryValidateTargetType(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        IPropertySymbol property,
        ITypeSymbol validatedType,
        ValidationRuleKind kind,
        ValidationRuleTargetKind targetKind,
        string attributeName)
    {
        var syntaxLocation = property.Locations.FirstOrDefault();

        var isSupported = kind switch
        {
            ValidationRuleKind.Length or ValidationRuleKind.MinLength or ValidationRuleKind.MaxLength or ValidationRuleKind.Email or ValidationRuleKind.Matches or ValidationRuleKind.NotEmptyGuid or ValidationRuleKind.NotDefaultOrEmptyGuid or ValidationRuleKind.ValidGuid or ValidationRuleKind.EmptyGuid or ValidationRuleKind.DefaultOrEmptyGuid or ValidationRuleKind.GuidFormat
                => validatedType.SpecialType == SpecialType.System_String,
            ValidationRuleKind.GreaterThan or ValidationRuleKind.GreaterThanOrEqualTo or ValidationRuleKind.LessThan or ValidationRuleKind.LessThanOrEqualTo or ValidationRuleKind.InclusiveBetween or ValidationRuleKind.ExclusiveBetween
                => IsComparableSupported(validatedType),
            ValidationRuleKind.Equal or ValidationRuleKind.NotEqual
                => IsEqualitySupported(validatedType),
            ValidationRuleKind.NotNull when targetKind == ValidationRuleTargetKind.Property
                => !validatedType.IsValueType || IsNullableValueType(validatedType),
            _ => true,
        };

        if (isSupported)
        {
            return true;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            RequesterSourceGeneratorDiagnostics.UnsupportedValidationAttributeUsage,
            syntaxLocation,
            attributeName,
            property.Name,
            ValidationGeneratorSymbolHelper.GetTypeName(property.Type),
            classSymbol.Name));
        return false;
    }

    private static bool TryValidateRuleConflicts(
        SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        IPropertySymbol property,
        ImmutableArray<ValidationPropertyRuleModel>.Builder rules)
    {
        var propertyRules = rules.Where(rule => SymbolEqualityComparer.Default.Equals(rule.PropertySymbol, property)).ToArray();
        if (propertyRules.Length < 2)
        {
            return true;
        }

        foreach (var group in propertyRules.GroupBy(static rule => rule.TargetKind))
        {
            var hasEmpty = group.Any(static rule => rule.Kind == ValidationRuleKind.Empty);
            var hasNotEmpty = group.Any(static rule => rule.Kind == ValidationRuleKind.NotEmpty);

            if (hasEmpty && hasNotEmpty)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequesterSourceGeneratorDiagnostics.ConflictingValidationAttributes,
                    property.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    property.Name,
                    "ValidateEmpty",
                    "ValidateNotEmpty"));
                return false;
            }
        }

        return true;
    }

    private static bool TryGetEnumerableElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        elementType = null;
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
        {
            elementType = namedType.TypeArguments[0];
            return true;
        }

        var enumerableType = type.AllInterfaces
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(static iface =>
                iface.IsGenericType &&
                iface.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

        if (enumerableType is null)
        {
            return false;
        }

        elementType = enumerableType.TypeArguments[0];
        return true;
    }

    private static bool IsComparableSupported(ITypeSymbol type)
    {
        if (IsEqualitySupported(type))
        {
            return true;
        }

        return type.SpecialType is SpecialType.System_DateTime;
    }

    private static bool IsEqualitySupported(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        return type.SpecialType is
            SpecialType.System_String or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal or
            SpecialType.System_Boolean or
            SpecialType.System_DateTime ||
            type.ToDisplayString() is "System.Guid" or "System.DateTimeOffset" or "System.TimeSpan";
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private sealed class ValidationRuleMetadata
    {
        public ValidationRuleMetadata(ValidationRuleKind kind, ValidationRuleTargetKind targetKind)
        {
            this.Kind = kind;
            this.TargetKind = targetKind;
        }

        public ValidationRuleKind Kind { get; }

        public ValidationRuleTargetKind TargetKind { get; }
    }
}
