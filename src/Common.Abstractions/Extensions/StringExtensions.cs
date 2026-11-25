// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Linq;
using System.Text;

/// <summary>Provides string-specific extension methods.</summary>
/// <example><code>var value = "red red blue".Distinct();</code></example>
public static class StringExtensions
{
    /// <summary>Removes duplicate space-delimited words while retaining their first occurrence and order.</summary>
    /// <param name="source">The string containing space-delimited words.</param>
    /// <returns>A string containing distinct words separated by one space.</returns>
    /// <example><code>var value = "red red blue".Distinct(); // "red blue"</code></example>
    public static string Distinct(this string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var words = source.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var distinctWords = words.Distinct();

        return string.Join(" ", distinctWords);
    }

    /// <summary>Returns the longest prefix that fits within a UTF-8 byte limit without splitting a Unicode scalar.</summary>
    /// <param name="source">The text to truncate.</param>
    /// <param name="maximumBytes">The maximum number of UTF-8 bytes.</param>
    /// <returns>The original text if it fits, including null or empty input. Otherwise, the prefix that fits without an added marker.</returns>
    /// <remarks>UTF-8 byte counting treats unpaired surrogates as U+FFFD. When truncation is needed, any retained unpaired surrogate is returned as U+FFFD.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is negative.</exception>
    /// <example><code>var label = "Café 😀".TruncateUtf8(8); // "Café "</code></example>
    public static string TruncateUtf8(this string source, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        // UTF-8 needs at least one byte per UTF-16 code unit, including surrogate pairs.
        if (source is null || (source.Length <= maximumBytes && Encoding.UTF8.GetByteCount(source) <= maximumBytes))
        {
            return source;
        }

        var builder = new StringBuilder();
        Span<char> encodedRune = stackalloc char[2];
        var remainingBytes = maximumBytes;
        foreach (var rune in source.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > remainingBytes)
            {
                break;
            }

            builder.Append(encodedRune[..rune.EncodeToUtf16(encodedRune)]);
            remainingBytes -= rune.Utf8SequenceLength;
        }

        return builder.ToString();
    }
}
