// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    [DebuggerStepThrough]
    public static IEnumerable<TProjection> DistinctIf<TProjection>(
        this IEnumerable<TProjection> source,
        Func<TProjection, object> distinct)
    {
        if (distinct is not null)
        {
            source = source.GroupBy(distinct).Select(g => g.FirstOrDefault()).AsQueryable();
        }

        return source;
    }
}