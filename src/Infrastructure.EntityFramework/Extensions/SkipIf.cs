// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Linq;

public static partial class Extensions
{
    public static IQueryable<TSource> SkipIf<TSource>(
        this IQueryable<TSource> source, int? skip)
        => skip > 0 ? source.Skip(skip.Value) : source;

    //public static IEnumerable<T> SkipIf<T>(
    //    this IEnumerable<T> source, int? skip)
    //    => skip > 0 ? source.Skip(skip.Value) : source;
}