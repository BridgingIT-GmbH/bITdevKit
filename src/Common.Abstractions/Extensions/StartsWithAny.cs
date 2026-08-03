// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
    /// <summary>Determines whether a string begins with any supplied prefix.</summary>
    /// <param name="source">The string to inspect.</param>
    /// <param name="items">The prefixes to compare.</param>
    /// <param name="comp">The comparison mode.</param>
    /// <returns><see langword="true" /> when a prefix matches; otherwise <see langword="false" />.</returns>
    /// <example><code>var isHttp = url.StartsWithAny(["http://", "https://"]);</code></example>
    public static bool StartsWithAny(
        this string source,
        IEnumerable<string> items,
        StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (items.IsNullOrEmpty())
        {
            return false;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (source.StartsWith(item, comp))
            {
                return true;
            }
        }

        return false;
    }
}
