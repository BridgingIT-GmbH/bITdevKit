// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    /// Determines whether the specified string contains any of the items.
    /// </summary>
    [DebuggerStepThrough]
    public static bool ContainsAny(
        this string source,
        string[] items,
        StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (items is null)
        {
            return false;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (source.Contains(item, comp))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the source contains any of the specified items using the provided equality comparer.
    /// </summary>
    [DebuggerStepThrough]
    public static bool ContainsAny<T>(
        this IEnumerable<T> source,
        IEnumerable<T> items,
        IEqualityComparer<T> comparer = null)
    {
        if (source is null)
        {
            return false;
        }

        if (items is null)
        {
            return false;
        }

        var set = new HashSet<T>(source, comparer);
        foreach (var item in items)
        {
            if (set.Contains(item))
            {
                return true;
            }
        }

        return false;
    }
}