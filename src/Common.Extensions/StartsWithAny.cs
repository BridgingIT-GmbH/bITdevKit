// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Extensions
{
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