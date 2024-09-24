// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;

using System.Linq.Expressions;

public static partial class Extensions
{
    public static IQueryable<T> OrderByIf<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> expression,
        bool descending = false)
    {
        if (expression is not null)
        {
            if (descending)
            {
                return source.OrderByDescending(expression);
            }

            return source.OrderBy(expression);
        }

        return source;
    }
}