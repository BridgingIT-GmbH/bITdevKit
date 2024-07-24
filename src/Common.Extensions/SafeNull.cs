// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

public static partial class Extensions
{
    /// <summary>
    /// Converts a null list to an empty list. Also clears out possible 'null' items
    /// Avoids null ref exceptions.
    /// </summary>
    /// <typeparam name="TSource">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <returns>collection of sources.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<TSource> SafeNull<TSource>(this IEnumerable<TSource> source)
    {
        return (source ?? []).Where(i => i is not null);
    }

    [DebuggerStepThrough]
    public static string SafeNull(this string source)
    {
        return source ?? string.Empty;
    }

    /// <summary>
    /// Converts an null list to an empty list. Also clears out possible 'null' items
    /// Avoids null ref exceptions.
    /// </summary>
    /// <typeparam name="TSource">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <returns>collection of sources.</returns>
    [DebuggerStepThrough]
    public static ICollection<TSource> SafeNull<TSource>(this ICollection<TSource> source)
    {
        return (source ?? new Collection<TSource>()).Where(i => i is not null).ToList();
    }

    ///// <summary>
    ///// Converts an null list to an empty list. Also clears out possible 'null' items
    ///// Avoids null ref exceptions.
    ///// </summary>
    ///// <typeparam name="TSource">the source.</typeparam>
    ///// <param name="source">the source collection.</param>
    ///// <returns>collection of sources.</returns>
    //[DebuggerStepThrough]
    //public static IReadOnlyCollection<TSource> SafeNull<TSource>(this IReadOnlyCollection<TSource> source)
    //{
    //    return (source ?? new Collection<TSource>()).Where(i => i is not null).ToList().AsReadOnly();
    //}

    /// <summary>
    /// Converts an null dictionary to an empty dictionary. avoids null ref exceptions.
    /// </summary>
    /// <typeparam name="TKey">the source key type.</typeparam>
    /// <typeparam name="TValue">the source value type.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <returns>collection of sources.</returns>
    [DebuggerStepThrough]
    public static IDictionary<TKey, TValue> SafeNull<TKey, TValue>(this IDictionary<TKey, TValue> source)
    {
        return source ?? new Dictionary<TKey, TValue>();
    }
}