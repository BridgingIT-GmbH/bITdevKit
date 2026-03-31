// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Provides symbol-formatting and rendering helpers shared by Requester source generation.
/// </summary>
public static class RequesterGeneratorSymbolHelper
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly HashSet<string> ReservedIdentifiers =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    ];

    private static readonly HashSet<string> SupportedPolicyAttributes =
    [
        "BridgingIT.DevKit.Common.HandlerAuthorizePolicyAttribute",
        "BridgingIT.DevKit.Common.HandlerAuthorizeRolesAttribute",
        "BridgingIT.DevKit.Common.HandlerRetryAttribute",
        "BridgingIT.DevKit.Common.HandlerTimeoutAttribute",
        "BridgingIT.DevKit.Common.HandlerChaosAttribute",
        "BridgingIT.DevKit.Common.HandlerCircuitBreakerAttribute",
        "BridgingIT.DevKit.Common.HandlerCacheInvalidateAttribute",
        "BridgingIT.DevKit.Common.HandlerDatabaseTransactionAttribute",
        "BridgingIT.DevKit.Common.HandlerDatabaseTransactionAttribute`1",
    ];

    /// <summary>
    /// Converts a Roslyn type symbol into the fully qualified type name used in generated code.
    /// </summary>
    /// <param name="type">The type symbol to render.</param>
    /// <returns>The fully qualified generated-code type name.</returns>
    public static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(TypeDisplayFormat);
    }

    /// <summary>
    /// Gets the generated accessibility keyword for a type symbol.
    /// </summary>
    /// <param name="symbol">The type symbol whose visibility should be mirrored.</param>
    /// <returns><c>public</c> when the symbol is public; otherwise <c>internal</c>.</returns>
    public static string GetAccessibilityKeyword(INamedTypeSymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
    }

    /// <summary>
    /// Escapes identifiers that would otherwise conflict with C# keywords.
    /// </summary>
    /// <param name="identifier">The identifier to render.</param>
    /// <returns>A keyword-safe identifier.</returns>
    public static string EscapeIdentifier(string identifier)
    {
        return ReservedIdentifiers.Contains(identifier) ? "@" + identifier : identifier;
    }

    /// <summary>
    /// Produces a source-file hint name safe for generated file output.
    /// </summary>
    /// <param name="value">The original hint name source.</param>
    /// <returns>A sanitized hint name containing only letters, digits, and underscores.</returns>
    public static string SanitizeHintName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Collects supported handler policy attributes from the authored request type.
    /// </summary>
    /// <param name="classSymbol">The authored request type symbol.</param>
    /// <returns>The supported policy attributes in source order.</returns>
    public static ImmutableArray<AttributeData> GetPolicyAttributes(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .Where(static attribute => IsSupportedPolicyAttribute(attribute.AttributeClass))
            .OrderBy(static attribute => attribute.ApplicationSyntaxReference?.Span.Start ?? int.MaxValue)
            .ToImmutableArray();
    }

    /// <summary>
    /// Determines whether an attribute type is one of the supported handler policy attributes.
    /// </summary>
    /// <param name="attributeType">The attribute type to inspect.</param>
    /// <returns><see langword="true"/> when the attribute should be copied to the generated handler; otherwise <see langword="false"/>.</returns>
    public static bool IsSupportedPolicyAttribute(INamedTypeSymbol attributeType)
    {
        if (attributeType is null)
        {
            return false;
        }

        var metadataName = attributeType.IsGenericType
            ? attributeType.ConstructedFrom.ToDisplayString()
            : attributeType.ToDisplayString();

        return SupportedPolicyAttributes.Contains(metadataName);
    }

    /// <summary>
    /// Renders an attribute into source code suitable for the generated handler type.
    /// </summary>
    /// <param name="attribute">The attribute to render.</param>
    /// <returns>The rendered attribute source.</returns>
    public static string RenderAttribute(AttributeData attribute)
    {
        var typeName = GetTypeName(attribute.AttributeClass);
        var arguments = new List<string>();

        foreach (var argument in attribute.ConstructorArguments)
        {
            arguments.Add(RenderTypedConstant(argument));
        }

        foreach (var namedArgument in attribute.NamedArguments.OrderBy(static argument => argument.Key, StringComparer.Ordinal))
        {
            arguments.Add($"{namedArgument.Key} = {RenderTypedConstant(namedArgument.Value)}");
        }

        return arguments.Count == 0
            ? $"[{typeName}]"
            : $"[{typeName}({string.Join(", ", arguments)})]";
    }

    /// <summary>
    /// Renders a Roslyn typed constant into source code.
    /// </summary>
    /// <param name="constant">The constant value to render.</param>
    /// <returns>The generated source representation of the constant.</returns>
    public static string RenderTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        if (constant.Kind == TypedConstantKind.Type)
        {
            return $"typeof({GetTypeName((ITypeSymbol)constant.Value)})";
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            var elementType = constant.Type is IArrayTypeSymbol arrayType
                ? GetTypeName(arrayType.ElementType)
                : GetTypeName(constant.Values.FirstOrDefault().Type);

            return $"new {elementType}[] {{ {string.Join(", ", constant.Values.Select(RenderTypedConstant))} }}";
        }

        if (constant.Type?.TypeKind == TypeKind.Enum)
        {
            return RenderEnumConstant(constant);
        }

        return constant.Value switch
        {
            string value => SymbolDisplay.FormatLiteral(value, quote: true),
            char value => SymbolDisplay.FormatLiteral(value, quote: true),
            bool value => value ? "true" : "false",
            float value => value.ToString("R", CultureInfo.InvariantCulture) + "F",
            double value => value.ToString("R", CultureInfo.InvariantCulture) + "D",
            decimal value => value.ToString(CultureInfo.InvariantCulture) + "M",
            long value => value.ToString(CultureInfo.InvariantCulture) + "L",
            ulong value => value.ToString(CultureInfo.InvariantCulture) + "UL",
            uint value => value.ToString(CultureInfo.InvariantCulture) + "U",
            short value => value.ToString(CultureInfo.InvariantCulture),
            ushort value => value.ToString(CultureInfo.InvariantCulture),
            byte value => value.ToString(CultureInfo.InvariantCulture),
            sbyte value => value.ToString(CultureInfo.InvariantCulture),
            int value => value.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(constant.Value, CultureInfo.InvariantCulture) ?? "null",
        };
    }

    private static string RenderEnumConstant(TypedConstant constant)
    {
        var enumType = (INamedTypeSymbol)constant.Type;
        var enumName = GetTypeName(enumType);
        var fields = enumType.GetMembers().OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue)
            .ToArray();

        foreach (var field in fields)
        {
            if (Equals(field.ConstantValue, constant.Value))
            {
                return $"{enumName}.{field.Name}";
            }
        }

        return $"({enumName}){Convert.ToString(constant.Value, CultureInfo.InvariantCulture)}";
    }
}
