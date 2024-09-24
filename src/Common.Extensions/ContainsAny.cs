// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    ///     Determines whether the specified string contains any of the items.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <param name="comp">The comp.</param>
    /// <returns>
    ///     <c>true</c> if the specified items contains any; otherwise, <c>false</c>.
    /// </returns>
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
}