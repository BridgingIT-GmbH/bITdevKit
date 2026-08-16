// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System.Linq.Expressions;

public static partial class Extensions
{
    /// <summary>
    /// Executes the where expression operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expression">The expression used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static ILiteQueryable<TSource> WhereExpression<TSource>(
        this ILiteCollection<TSource> source,
        Expression<Func<TSource, bool>> expression)
    {
        if (expression is not null)
        {
            return source.Query().Where(expression);
        }

        return source.Query();
    }

    /// <summary>
    /// Executes the where expressions operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expressions">The expressions used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static ILiteQueryable<TSource> WhereExpressions<TSource>(
        this ILiteCollection<TSource> source,
        IEnumerable<Expression<Func<TSource, bool>>> expressions)
    {
        var query = source.Query();
        if (expressions?.Any() == true)
        {
            foreach (var expression in expressions)
            {
                query = query.Where(expression);
            }
        }

        return query;
    }
}
