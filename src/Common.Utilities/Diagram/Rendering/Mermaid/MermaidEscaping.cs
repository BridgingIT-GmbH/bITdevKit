// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides Mermaid-safe escaping helpers.
/// </summary>
internal static class MermaidEscaping
{
    /// <summary>
    /// Escapes a state-diagram label.
    /// </summary>
    /// <param name="value">The raw label.</param>
    /// <returns>The escaped label.</returns>
    public static string EscapeStateLabel(string value)
    {
        return MermaidNaming.NormalizeLabel(value);
    }

    /// <summary>
    /// Escapes state-diagram note content.
    /// </summary>
    /// <param name="value">The raw note text.</param>
    /// <returns>The escaped note text.</returns>
    public static string EscapeStateNote(string value)
    {
        return MermaidNaming.NormalizeLabel(value);
    }

    /// <summary>
    /// Escapes Mermaid text while preserving readable spacing.
    /// </summary>
    /// <param name="value">The raw text.</param>
    /// <returns>The escaped text.</returns>
    public static string EscapeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    /// <summary>
    /// Escapes Mermaid text that will be emitted in quotes.
    /// </summary>
    /// <param name="value">The raw text.</param>
    /// <returns>The escaped text.</returns>
    public static string EscapeQuotedText(string value)
    {
        var text = EscapeText(value);
        return text?.Replace('"', '\'');
    }
}