// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static string TruncateLeft(this string source, int length)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (length < 0)
        {
            length = 0;
        }

        if (source.Length > length)
        {
            return source[^length..];
        }

        return source;
    }

    [DebuggerStepThrough]
    public static string TruncateRight(this string source, int length)
    {
        if (source.IsNullOrEmpty())
        {
            return source;
        }

        if (length < 0)
        {
            length = 0;
        }

        if (source.Length > length)
        {
            return source[..length];
        }

        return source;
    }
}