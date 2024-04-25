// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static bool SafeAny<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate = null)
    {
        if (source.IsNullOrEmpty())
        {
            return false;
        }

        if (predicate is not null)
        {
            return source.Any(predicate);
        }
        else
        {
            return source.Any(i => i is not null);
        }
    }
}