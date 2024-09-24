// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;

public static partial class Extensions
{
    public static IQueryable<T> SkipIf<T>(this IQueryable<T> source, int? count = null, bool? condition = true)
    {
        if (condition == true && count.HasValue && count.Value > 0)
        {
            return source.Skip(count.Value);
        }

        return source;
    }
}