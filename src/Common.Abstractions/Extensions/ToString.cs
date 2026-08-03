// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>Joins an enumerable into a string using the specified separator.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The values to join.</param>
    /// <param name="separator">The separator placed between values.</param>
    /// <returns>The joined values, or an empty string when <paramref name="source" /> is null or empty.</returns>
    /// <example><code>var csv = new[] { 1, 2, 3 }.ToString(","); // "1,2,3"</code></example>
    [DebuggerStepThrough]
    public static string ToString<T>(this IEnumerable<T> source, string separator)
    {
        return source.IsNullOrEmpty() ? string.Empty : string.Join(separator, source);
    }

    /// <summary>Joins an enumerable into a string using the specified character separator.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The values to join.</param>
    /// <param name="seperator">The separator placed between values.</param>
    /// <returns>The joined values, or an empty string when <paramref name="source" /> is null or empty.</returns>
    /// <example><code>var csv = new[] { 1, 2, 3 }.ToString(','); // "1,2,3"</code></example>
    [DebuggerStepThrough]
    public static string ToString<T>(this IEnumerable<T> source, char seperator)
    {
        return ToString(source, seperator.ToString());
    }
}
