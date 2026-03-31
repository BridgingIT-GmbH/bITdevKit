// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text;
using Microsoft.CodeAnalysis;

/// <summary>
/// Provides small symbol and string helpers shared by pipeline source generation.
/// </summary>
public static class PipelineGeneratorSymbolHelper
{
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
    /// Converts a Roslyn type symbol into the fully qualified type name used in generated code.
    /// </summary>
    /// <param name="symbol">The type symbol to render.</param>
    /// <returns>The fully qualified generated-code type name.</returns>
    public static string ToTypeDisplay(ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Produces the generated field name for an injected dependency parameter.
    /// </summary>
    /// <param name="parameterName">The original parameter name.</param>
    /// <returns>The generated backing-field name.</returns>
    public static string GetFieldName(string parameterName)
    {
        return "_" + char.ToLowerInvariant(parameterName[0]) + parameterName.Substring(1);
    }

    /// <summary>
    /// Escapes a string so it can be embedded inside generated C# source.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped C# string literal content.</returns>
    public static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
