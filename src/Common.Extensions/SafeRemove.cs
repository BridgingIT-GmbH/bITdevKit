// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    ///     Safely removes the item from the list.
    /// </summary>
    [DebuggerStepThrough]
    public static bool SafeRemove<T>(this IList<T> source, T item)
    {
        if (source.IsNullOrEmpty() || item is null)
        {
            return false;
        }

        return source.Remove(item);
    }

    /// <summary>
    ///     Safely removes the item from the collection.
    /// </summary>
    [DebuggerStepThrough]
    public static bool SafeRemove<T>(this ICollection<T> source, T item)
    {
        if (source.IsNullOrEmpty() || item is null)
        {
            return false;
        }

        return source.Remove(item);
    }

    /// <summary>
    ///     Adds or updates the entry in the dictionary.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="key">The key.</param>
    [DebuggerStepThrough]
    public static IDictionary<TKey, TValue> SafeRemove<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
    {
        if (source is null || key is null)
        {
            return source;
        }

        source.Remove(key);

        return source;
    }
}