// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    /// Returns null when an enumerable is null or empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The enumerable to inspect.</param>
    /// <returns>The original enumerable when it has elements; otherwise null.</returns>
    /// <example><code>var items = Array.Empty&lt;int&gt;().EmptyToNull();</code></example>
    [DebuggerStepThrough]
    public static IEnumerable<T> EmptyToNull<T>(this IEnumerable<T> source)
    {
        return source.IsNullOrEmpty() ? null : source;
    }

    /// <summary>
    /// Returns null when a string is null or empty.
    /// </summary>
    /// <param name="source">The string to inspect.</param>
    /// <returns>The original string when it has content; otherwise null.</returns>
    /// <example><code>var value = "".EmptyToNull();</code></example>
    [DebuggerStepThrough]
    public static string EmptyToNull(this string source)
    {
        return string.IsNullOrEmpty(source) ? null : source;
    }
}
