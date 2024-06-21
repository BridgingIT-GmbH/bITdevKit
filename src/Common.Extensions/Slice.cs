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
    public static string Slice(this string source, string start, string end,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        return SliceFrom(source, start, comparison)
            .SliceTill(end, comparison);
    }

    [DebuggerStepThrough]
    public static string Slice(this string source, int start, int end)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (end < start)
        {
            end = source.Length;
        }

        return source[start..end];
    }
}