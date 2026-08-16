// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;

using System.Linq.Expressions;

public static partial class Extensions
{
    /// <summary>
    /// Executes the order by if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expression">The expression used by the operation.</param>
    /// <param name="descending">The descending used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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
