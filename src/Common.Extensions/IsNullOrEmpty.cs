// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static bool IsNullOrEmpty<TSource>(this IEnumerable<TSource> source) // TODO: or SafeAny()?
    {
        return source?.Any() != true;
    }

    [DebuggerStepThrough]
    public static bool IsNullOrEmpty<TSource>(this ICollection<TSource> source) // TODO: or SafeAny()?
    {
        return source?.Any() != true;
    }

    [DebuggerStepThrough]
    public static bool IsNullOrEmpty(this Stream source)
    {
        return source is null || source.Length == 0;
    }

    [DebuggerStepThrough]
    public static bool IsNullOrEmpty(this Guid source)
    {
        return source == Guid.Empty;
    }

    //public static bool IsNullOrEmpty<TSource>(this IReadOnlyCollection<TSource> source)
    //{
    //    return source is null || !source.Any();
    //}
}