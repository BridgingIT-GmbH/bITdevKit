// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    ///     Applies a predicate to a list, handling null lists by returning an empty list.
    ///     Avoids null reference exceptions.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="predicate">The predicate to apply to each element.</param>
    /// <returns>A collection of elements that satisfy the predicate.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<TSource> SafeWhere<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate)
    {
        return (source ?? []).Where(predicate);
    }

    /// <summary>
    ///     Applies a predicate to a collection, handling null collections by returning an empty collection.
    ///     Avoids null reference exceptions.
    /// </summary>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="predicate">The predicate to apply to each element.</param>
    /// <returns>A collection of elements that satisfy the predicate.</returns>
    [DebuggerStepThrough]
    public static ICollection<TSource> SafeWhere<TSource>(
        this ICollection<TSource> source,
        Func<TSource, bool> predicate)
    {
        return (source ?? []).Where(predicate).ToList();
    }

    /// <summary>
    ///     Applies a predicate to a dictionary, handling null dictionaries by returning an empty dictionary.
    ///     Avoids null reference exceptions.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the source dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the source dictionary.</typeparam>
    /// <param name="source">The source dictionary.</param>
    /// <param name="predicate">The predicate to apply to each element.</param>
    /// <returns>A dictionary of elements that satisfy the predicate.</returns>
    [DebuggerStepThrough]
    public static IDictionary<TKey, TValue> SafeWhere<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        Func<KeyValuePair<TKey, TValue>, bool> predicate)
    {
        return (source ?? new Dictionary<TKey, TValue>()).Where(predicate)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}