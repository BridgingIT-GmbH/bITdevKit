// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static partial class Extensions
{
    /// <summary>
    /// Retains the first item for each projected key when a key selector is supplied.
    /// </summary>
    /// <typeparam name="TProjection">The sequence element type.</typeparam>
    /// <param name="source">The sequence to filter.</param>
    /// <param name="distinct">The optional key selector; a null selector leaves the source unchanged.</param>
    /// <returns>The original source or a deferred sequence containing the first item from each key group.</returns>
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
