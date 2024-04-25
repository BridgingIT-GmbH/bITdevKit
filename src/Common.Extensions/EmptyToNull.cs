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
    public static IEnumerable<T> EmptyToNull<T>(this IEnumerable<T> source)
    {
        if (source.IsNullOrEmpty())
        {
            return null;
        }

        return source;
    }

    [DebuggerStepThrough]
    public static string EmptyToNull(this string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        return source;
    }

    [DebuggerStepThrough]
    public static string Default(this string source, string defaultValue)
    {
        if (string.IsNullOrEmpty(source))
        {
            return defaultValue;
        }

        return source;
    }
}