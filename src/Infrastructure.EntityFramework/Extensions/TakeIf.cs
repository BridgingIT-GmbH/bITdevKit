// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the take if operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="take">The take used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IQueryable<TSource> TakeIf<TSource>(this IQueryable<TSource> source, int? take)
    {
        return take > 0 ? source.Take(take.Value) : source;
    }

    //public static IEnumerable<T> TakeIf<T>(
    //    this IEnumerable<T> source, int? take)
    //    => take > 0 ? source.Take(take.Value) : source;
}
