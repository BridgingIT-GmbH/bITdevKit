// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Linq;

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
}
