// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;

public static class EnumerableExtensions
{
    /// <summary>
    /// Adds the item to the collection.
    /// </summary>
    /// <typeparam name="T">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <param name="item">The item to add.</param>
    public static IEnumerable<T> Add<T>(this IEnumerable<T> source, T item)
    {
        return source.Insert(item, -1);
    }

    /// <summary>
    /// Adds the items to the collection.
    /// </summary>
    /// <typeparam name="T">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <param name="items">The items to add.</param>
    public static IEnumerable<T> Add<T>(this IEnumerable<T> source, IEnumerable<T> items)
    {
        return source.InsertRange(items, -1);
    }

    /// <summary>
    /// Inserts the item in the collection.
    /// </summary>
    /// <typeparam name="T">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <param name="item">The item to insert.</param>
    /// <param name="index">the index at which the item should inserted.</param>
    public static IEnumerable<T> Insert<T>(
        this IEnumerable<T> source,
        T item,
        int index = 0)
    {
        if (item is null)
        {
            return source;
        }

        if (source is null)
        {
            return new List<T> { item };
        }

        var result = new List<T>(source);
        if (index >= 0)
        {
            result.Insert(index, item);
        }
        else
        {
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Inserts the items in the collection.
    /// </summary>
    /// <typeparam name="T">the source.</typeparam>
    /// <param name="source">the source collection.</param>
    /// <param name="items">The items to insert.</param>
    /// <param name="index">the index at which the item should inserted.</param>
    public static IEnumerable<T> InsertRange<T>(
        this IEnumerable<T> source,
        IEnumerable<T> items,
        int index = 0)
    {
        if (items is null)
        {
            return source;
        }

        if (source is null)
        {
            return new List<T>(items);
        }

        var result = new List<T>(source);
        if (index >= 0)
        {
            result.InsertRange(index, items);
        }
        else
        {
            result.AddRange(items);
        }

        return result;
    }
}