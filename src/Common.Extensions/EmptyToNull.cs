// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static IEnumerable<T> EmptyToNull<T>(this IEnumerable<T> source)
    {
        return source.IsNullOrEmpty() ? null : source;
    }

    [DebuggerStepThrough]
    public static string EmptyToNull(this string source)
    {
        return string.IsNullOrEmpty(source) ? null : source;
    }
}