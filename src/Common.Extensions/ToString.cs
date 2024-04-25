// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static string ToString<T>(this IEnumerable<T> source, string separator)
    {
        if (source.IsNullOrEmpty())
        {
            return string.Empty;
        }

        return string.Join(separator, source);
    }

    [DebuggerStepThrough]
    public static string ToString<T>(this IEnumerable<T> source, char seperator)
        => ToString(source, seperator.ToString());
}