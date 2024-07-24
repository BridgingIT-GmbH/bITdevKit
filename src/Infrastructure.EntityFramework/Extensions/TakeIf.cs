// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Linq;

public static partial class Extensions
{
    public static IQueryable<TSource> TakeIf<TSource>(
        this IQueryable<TSource> source, int? take)
        => take > 0 ? source.Take(take.Value) : source;

    //public static IEnumerable<T> TakeIf<T>(
    //    this IEnumerable<T> source, int? take)
    //    => take > 0 ? source.Take(take.Value) : source;
}