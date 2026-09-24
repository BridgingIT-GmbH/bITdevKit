// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <param name="source">The string to slice.</param>
    extension(string source)
    {
        /// <summary>Returns the portion of a string after its first matching delimiter.</summary>
        /// <param name="from">The delimiter after which the slice begins.</param>
        /// <param name="comparison">The comparison mode for the delimiter.</param>
        /// <returns>The portion after the delimiter, or an empty string when it is not found.</returns>
        /// <example><code>var fileName = "archive/report.pdf".SliceFrom("/"); // "report.pdf"</code></example>
        [DebuggerStepThrough]
        public string SliceFrom(
            string from,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (source.IsNullOrEmpty() || from.IsNullOrEmpty())
            {
                return source;
            }

            return SliceFromInternal(source, from, source.IndexOf(from, comparison));
        }

        /// <summary>Returns the portion of a string after its last matching delimiter.</summary>
        /// <param name="from">The delimiter after which the slice begins.</param>
        /// <param name="comparison">The comparison mode for the delimiter.</param>
        /// <returns>The portion after the delimiter, or an empty string when it is not found.</returns>
        /// <example><code>var fileName = "archive/2026/report.pdf".SliceFromLast("/"); // "report.pdf"</code></example>
        [DebuggerStepThrough]
        public string SliceFromLast(
            string from,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (source.IsNullOrEmpty() || from.IsNullOrEmpty())
            {
                return source;
            }

            return SliceFromInternal(source, from, source.LastIndexOf(from, comparison));
        }

        private string SliceFromInternal(string from, int fromIndex)
        {
            if (source.IsNullOrEmpty())
            {
                return source;
            }

            var sourceLength = source.Length;

            if (fromIndex == 0 && fromIndex + from.Length < sourceLength)
            {
                return source[(fromIndex + from.Length)..];
            }

            if (fromIndex > 0 && fromIndex == sourceLength)
            {
                return string.Empty;
            }

            if (fromIndex > 0 && fromIndex + from.Length < sourceLength)
            {
                return source[(fromIndex + from.Length)..];
            }

            return string.Empty;
        }
    }
}
