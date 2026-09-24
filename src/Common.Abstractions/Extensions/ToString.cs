// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <param name="source">The values to join.</param>
    /// <typeparam name="T">The element type.</typeparam>
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>Joins an enumerable into a string using the specified separator.</summary>
        /// <param name="separator">The separator placed between values.</param>
        /// <returns>The joined values, or an empty string when <paramref name="source" /> is null or empty.</returns>
        /// <example><code>var csv = new[] { 1, 2, 3 }.ToString(","); // "1,2,3"</code></example>
        [DebuggerStepThrough]
        public string ToString(string separator)
        {
            return source.IsNullOrEmpty() ? string.Empty : string.Join(separator, source);
        }

        /// <summary>Joins an enumerable into a string using the specified character separator.</summary>
        /// <param name="seperator">The separator placed between values.</param>
        /// <returns>The joined values, or an empty string when <paramref name="source" /> is null or empty.</returns>
        /// <example><code>var csv = new[] { 1, 2, 3 }.ToString(','); // "1,2,3"</code></example>
        [DebuggerStepThrough]
        public string ToString(char seperator)
        {
            return ToString(source, seperator.ToString());
        }
    }
}
