// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Text.RegularExpressions;

public static partial class Extensions
{
    /// <param name="source">the source string.</param>
    extension(string source)
    {
        /// <summary>
        ///     Compares string with usage of pattern *.
        /// </summary>
        /// <param name="pattern">the value string to compare to.</param>
        /// <param name="ignoreCase">Ignore case.</param>
        /// <returns>true if equal, otherwhise false.</returns>
        /// <remarks>Only * is a wildcard, and it includes line breaks. Question marks and backslashes are literal.
        /// Ignore-case matching follows the current culture.</remarks>
        /// <example><code>var matches = "first\nsecond".Match("first*second");</code></example>
        [DebuggerStepThrough]
        public bool Match(string pattern, bool ignoreCase = true)
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
                (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.Singleline,
                new TimeSpan(0, 0, 3));
        }

        /// <summary>
        ///     Compares string with usage of pattern *.
        /// </summary>
        /// <param name="patterns">the value strings to compare to.</param>
        /// <param name="ignoreCase">Ignore case.</param>
        /// <returns>true if equal, otherwhise false.</returns>
        /// <remarks>Uses the same simple-wildcard, line-break, and culture rules as <c>Match</c>.</remarks>
        /// <example><code>var matches = "report.json".MatchAny(["*.json", "*.csv"]);</code></example>
        //[DebuggerStepThrough]
        public bool MatchAny(IEnumerable<string> patterns, bool ignoreCase = true)
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

                if (source.Match(pattern, ignoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}