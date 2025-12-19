// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class ListExtensions
{
    /// <summary>
    ///     Adds or updates the entry in the list.
    /// </summary>
    [DebuggerStepThrough]
    public static void AddOrUpdate<T>(this IList<T> source, T item)
    {
        if (source is null || item is null)
        {
            return;
        }

        if (source.Contains(item))
        {
            source.Remove(item);
        }

        source.Add(item);
    }

    /// <summary>
    ///     Adds or updates the entry in the collection.
    /// </summary>
    [DebuggerStepThrough]
    public static void AddOrUpdate<T>(this ICollection<T> source, T item)
    {
        if (source is null || item is null)
        {
            return;
        }

        if (source.Contains(item))
        {
            source.Remove(item);
        }

        source.Add(item);
    }
}