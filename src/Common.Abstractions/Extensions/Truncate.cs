// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

/// <summary>
/// Provides string truncation extension methods that preserve either the beginning or end of a value.
/// </summary>
/// <example>
/// <code>
/// var suffix = "archives/2026/report.pdf".TruncateLeft(10);
/// var prefix = "archives/2026/report.pdf".TruncateRight(10);
/// // suffix is "report.pdf" and prefix is "archives/2".
/// </code>
/// </example>
public static partial class Extensions
{
    /// <summary>
    /// Retains at most the specified number of characters from the end of a string.
    /// </summary>
    /// <param name="source">The string to truncate.</param>
    /// <param name="length">The maximum number of terminal characters to retain.</param>
    /// <returns>The original string when it fits; otherwise its terminal characters. Null and empty values are returned unchanged.</returns>
    /// <remarks>A negative <paramref name="length" /> is treated as zero.</remarks>
    /// <example>
    /// <code>
    /// var fileName = "archives/2026/report.pdf".TruncateLeft(10);
    /// // "report.pdf"
    /// </code>
    /// </example>
    [DebuggerStepThrough]
    public static string TruncateLeft(this string source, int length)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (length < 0)
        {
            length = 0;
        }

        if (source.Length > length)
        {
            return source[^length..];
        }

        return source;
    }

    /// <summary>
    /// Retains at most the specified number of characters from the beginning of a string.
    /// </summary>
    /// <param name="source">The string to truncate.</param>
    /// <param name="length">The maximum number of initial characters to retain.</param>
    /// <returns>The original string when it fits; otherwise its initial characters. Null and empty values are returned unchanged.</returns>
    /// <remarks>A negative <paramref name="length" /> is treated as zero.</remarks>
    /// <example>
    /// <code>
    /// var prefix = "archives/2026/report.pdf".TruncateRight(10);
    /// // "archives/2"
    /// </code>
    /// </example>
    [DebuggerStepThrough]
    public static string TruncateRight(this string source, int length)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (length < 0)
        {
            length = 0;
        }

        if (source.Length > length)
        {
            return source[..length];
        }

        return source;
    }
}
