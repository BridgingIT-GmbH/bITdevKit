// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

public static partial class Extensions
{
    public static ILiteQueryableResult<T> SkipIf<T>(this ILiteQueryable<T> source, int? skip)
    {
        return skip > 0 ? source.Skip(skip.Value) : source;
    }

    public static ILiteQueryableResult<T> SkipIf<T>(this ILiteQueryableResult<T> source, int? skip)
    {
        return skip > 0 ? source.Skip(skip.Value) : source;
    }

    //public static IEnumerable<T> SkipIf<T>(
    //    this IEnumerable<T> source, int? skip)
    //    => skip.HasValue && skip.Value > 0 ? source.Skip(skip.Value) : source;
}