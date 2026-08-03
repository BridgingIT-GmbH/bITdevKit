// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>Determines whether an enumerable contains at least one non-null element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The enumerable to inspect.</param>
    /// <returns><see langword="true" /> when a non-null element exists; otherwise <see langword="false" />.</returns>
    /// <example><code>var hasValues = items.SafeAny();</code></example>
    [DebuggerStepThrough]
    public static bool SafeAny<T>(this IEnumerable<T> source)
    {
        if (source.IsNullOrEmpty())
        {
            return false;
        }

        return source.Any(i => i is not null);
    }

    /// <summary>Determines whether an enumerable contains an element that satisfies a predicate.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The enumerable to inspect.</param>
    /// <param name="predicate">The predicate to evaluate. When null, non-null elements are checked.</param>
    /// <returns><see langword="true" /> when a matching element exists; otherwise <see langword="false" />.</returns>
    /// <example><code>var hasActive = users.SafeAny(user => user.IsActive);</code></example>
    [DebuggerStepThrough]
    public static bool SafeAny<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        if (source.IsNullOrEmpty())
        {
            return false;
        }

        if (predicate is not null)
        {
            return source.Any(predicate);
        }

        return source.Any(i => i is not null);
    }
}
