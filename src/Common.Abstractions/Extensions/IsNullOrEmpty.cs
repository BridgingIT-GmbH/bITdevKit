// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>Determines whether an enumerable is null or has no elements.</summary>
    /// <typeparam name="TSource">The element type.</typeparam>
    /// <param name="source">The enumerable to inspect.</param>
    /// <returns><see langword="true" /> when <paramref name="source" /> is null or empty; otherwise <see langword="false" />.</returns>
    /// <example><code>var empty = items.IsNullOrEmpty();</code></example>
    [DebuggerStepThrough]
    public static bool IsNullOrEmpty<TSource>(this IEnumerable<TSource> source) // TODO: or SafeAny()?
    {
        return source?.Any() != true;
    }

    /// <summary>Determines whether a collection is null or has no elements.</summary>
    /// <typeparam name="TSource">The element type.</typeparam>
    /// <param name="source">The collection to inspect.</param>
    /// <returns><see langword="true" /> when <paramref name="source" /> is null or empty; otherwise <see langword="false" />.</returns>
    /// <example><code>var empty = items.IsNullOrEmpty();</code></example>
    [DebuggerStepThrough]
    public static bool IsNullOrEmpty<TSource>(this ICollection<TSource> source) // TODO: or SafeAny()?
    {
        return source?.Any() != true;
    }

    /// <summary>Determines whether a stream is null or has no bytes.</summary>
    /// <param name="source">The stream to inspect.</param>
    /// <returns><see langword="true" /> when <paramref name="source" /> is null or its length is zero; otherwise <see langword="false" />.</returns>
    /// <example><code>var empty = stream.IsNullOrEmpty();</code></example>
    [DebuggerStepThrough]
    public static bool IsNullOrEmpty(this Stream source)
    {
        return source is null || source.Length == 0;
    }

    /// <summary>Determines whether a GUID is <see cref="Guid.Empty" />.</summary>
    /// <param name="source">The GUID to inspect.</param>
    /// <returns><see langword="true" /> when the GUID is empty; otherwise <see langword="false" />.</returns>
    /// <example><code>var empty = id.IsEmpty();</code></example>
    [DebuggerStepThrough]
    public static bool IsEmpty(this Guid source)
    {
        return source == Guid.Empty;
    }

    //public static bool IsNullOrEmpty<TSource>(this IReadOnlyCollection<TSource> source)
    //{
    //    return source is null || !source.Any();
    //}
}
