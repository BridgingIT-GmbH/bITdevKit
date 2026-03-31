// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Provides symbol-formatting helpers shared by validation source generation.
/// </summary>
public static class ValidationGeneratorSymbolHelper
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// Converts a Roslyn type symbol into a fully qualified generated-code type name.
    /// </summary>
    /// <param name="type">The type symbol to format.</param>
    /// <returns>The generated-code type name.</returns>
    public static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(TypeDisplayFormat);
    }

    /// <summary>
    /// Escapes identifiers that would otherwise conflict with C# keywords.
    /// </summary>
    /// <param name="identifier">The identifier to escape.</param>
    /// <returns>A keyword-safe identifier.</returns>
    public static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ? "@" + identifier : identifier;
    }
}
