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
        /// <summary>Returns the portion of a string before its first matching delimiter.</summary>
        /// <param name="till">The delimiter before which the slice ends.</param>
        /// <param name="comparison">The comparison mode for the delimiter.</param>
        /// <returns>The portion before the delimiter, or the original string when it is not found.</returns>
        /// <example><code>var directory = "archive/report.pdf".SliceTill("/"); // "archive"</code></example>
        [DebuggerStepThrough]
        public string SliceTill(
            string till,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (source.IsNullOrEmpty() || till.IsNullOrEmpty())
            {
                return source;
            }

            return SliceTillInternal(source, source.IndexOf(till, comparison));
        }

        /// <summary>Returns the portion of a string before its last matching delimiter.</summary>
        /// <param name="till">The delimiter before which the slice ends.</param>
        /// <param name="comparison">The comparison mode for the delimiter.</param>
        /// <returns>The portion before the delimiter, or the original string when it is not found.</returns>
        /// <example><code>var directory = "archive/2026/report.pdf".SliceTillLast("/"); // "archive/2026"</code></example>
        [DebuggerStepThrough]
        public string SliceTillLast(
            string till,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (source.IsNullOrEmpty() || till.IsNullOrEmpty())
            {
                return source;
            }

            return SliceTillInternal(source, source.LastIndexOf(till, comparison));
        }

        private string SliceTillInternal(int tillIndex)
        {
            if (source.IsNullOrEmpty())
            {
                return source;
            }

            if (tillIndex == 0)
            {
                return string.Empty;
            }

            if (tillIndex > 0)
            {
                return source[..tillIndex];
            }

            return source;
        }
    }
}
