// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the skip if operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="skip">The skip used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IQueryable<TSource> SkipIf<TSource>(this IQueryable<TSource> source, int? skip)
    {
        return skip > 0 ? source.Skip(skip.Value) : source;
    }

    //public static IEnumerable<T> SkipIf<T>(
    //    this IEnumerable<T> source, int? skip)
    //    => skip > 0 ? source.Skip(skip.Value) : source;
}
