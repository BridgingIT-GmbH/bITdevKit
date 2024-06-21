// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static string SliceFrom(this string source, string from,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (source.IsNullOrEmpty() || from.IsNullOrEmpty())
        {
            return source;
        }

        return SliceFromInternal(source, from, source.IndexOf(from, comparison));
    }

    [DebuggerStepThrough]
    public static string SliceFromLast(this string source, string from,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (source.IsNullOrEmpty() || from.IsNullOrEmpty())
        {
            return source;
        }

        return SliceFromInternal(source, from, source.LastIndexOf(from, comparison));
    }

    private static string SliceFromInternal(this string source, string from, int fromIndex)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        var sourceLength = source.Length;

        if (fromIndex == 0 && fromIndex + from.Length < sourceLength)
        {
            return source[(fromIndex + from.Length)..];
        }

        if (fromIndex > 0 && fromIndex == sourceLength)
        {
            return string.Empty;
        }

        if (fromIndex > 0 && fromIndex + from.Length < sourceLength)
        {
            return source[(fromIndex + from.Length)..];
        }

        return string.Empty;
    }
}