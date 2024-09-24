// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Text.RegularExpressions;

public static partial class Extensions
{
    /// <summary>
    ///     Compares string with usage of pattern *.
    /// </summary>
    /// <param name="source">the source string.</param>
    /// <param name="pattern">the value string to compare to.</param>
    /// <param name="ignoreCase">Ignore case.</param>
    /// <returns>true if equal, otherwhise false.</returns>
    [DebuggerStepThrough]
    public static bool EqualsPattern(this string source, string pattern, bool ignoreCase = true)
    {
        if (source is null && pattern is null)
        {
            return true;
        }

        if (source is null)
        {
            return false;
        }

        if (pattern is null)
        {
            return false;
        }

        var regex = Regex.Escape(pattern).Replace("\\*", ".*");

        return Regex.IsMatch(source,
            "^" + (ignoreCase ? "(?i)" : string.Empty) + regex + "$",
            ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None,
            new TimeSpan(0, 0, 3));
    }

    /// <summary>
    ///     Compares string with usage of pattern *.
    /// </summary>
    /// <param name="source">the source string.</param>
    /// <param name="patterns">the value strings to compare to.</param>
    /// <param name="ignoreCase">Ignore case.</param>
    /// <returns>true if equal, otherwhise false.</returns>
    [DebuggerStepThrough]
    public static bool EqualsPatternAny(this string source, IEnumerable<string> patterns, bool ignoreCase = true)
    {
        if (source is null && patterns is null)
        {
            return true;
        }

        if (patterns.IsNullOrEmpty())
        {
            return false;
        }

        foreach (var pattern in patterns.SafeNull())
        {
            if (pattern is null)
            {
                continue;
            }

            if (source.EqualsPattern(pattern, ignoreCase))
            {
                return true;
            }
        }

        return false;
    }
}