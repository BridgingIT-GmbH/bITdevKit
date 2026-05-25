// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides Mermaid-safe naming helpers.
/// </summary>
internal static class MermaidNaming
{
    private const string StartOrEndIdentifier = "[*]";

    /// <summary>
    /// Normalizes a diagram identifier to a Mermaid-safe identifier.
    /// </summary>
    /// <param name="value">The raw identifier.</param>
    /// <returns>The normalized identifier.</returns>
    public static string NormalizeIdentifier(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (string.Equals(value.Trim(), StartOrEndIdentifier, StringComparison.Ordinal))
        {
            return StartOrEndIdentifier;
        }

        var normalized = NormalizeCore(value, allowLeadingDigit: false);
        return string.IsNullOrWhiteSpace(normalized)
            ? "_"
            : normalized;
    }

    /// <summary>
    /// Normalizes a diagram label to a Mermaid-safe label.
    /// </summary>
    /// <param name="value">The raw label.</param>
    /// <returns>The normalized label.</returns>
    public static string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeCore(value, allowLeadingDigit: true);
    }

    private static string NormalizeCore(string value, bool allowLeadingDigit)
    {
        Span<char> buffer = stackalloc char[value.Length + 1];
        var length = 0;
        var previousUnderscore = false;

        foreach (var character in value.Trim())
        {
            var normalized = char.IsLetterOrDigit(character) || character == '_'
                ? character
                : '_';

            if (normalized == '_')
            {
                if (previousUnderscore)
                {
                    continue;
                }

                previousUnderscore = true;
            }
            else
            {
                previousUnderscore = false;
            }

            buffer[length++] = normalized;
        }

        while (length > 0 && buffer[length - 1] == '_')
        {
            length--;
        }

        if (length == 0)
        {
            return allowLeadingDigit ? "label" : "_";
        }

        if (!allowLeadingDigit && !char.IsLetter(buffer[0]) && buffer[0] != '_')
        {
            for (var index = length; index > 0; index--)
            {
                buffer[index] = buffer[index - 1];
            }

            buffer[0] = '_';
            length++;
        }

        return new string(buffer[..length]);
    }
}