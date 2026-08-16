// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

public static partial class Extensions
{
    /// <summary>
    /// Executes the take if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="take">The take used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ILiteQueryableResult<T> TakeIf<T>(this ILiteQueryable<T> source, int? take)
    {
        return take > 0 ? source.Limit(take.Value) : source;
    }

    /// <summary>
    /// Executes the take if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="take">The take used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ILiteQueryableResult<T> TakeIf<T>(this ILiteQueryableResult<T> source, int? take)
    {
        return take > 0 ? source.Limit(take.Value) : source;
    }

    //public static IEnumerable<T> TakeIf<T>(
    //    this ILiteQueryable<T> source, int? take)
    //    => take.HasValue && take.Value > 0 ? source.Limit(take.Value) : source;
}
