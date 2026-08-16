// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the where expression operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expression">The expression used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<TSource> WhereExpression<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> expression)
    {
        if (expression is not null)
        {
            return source.Where(expression);
        }

        return source;
    }

    /// <summary>
    /// Executes the where expressions operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expressions">The expressions used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<TSource> WhereExpressions<TSource>(
        this IQueryable<TSource> source,
        IEnumerable<Expression<Func<TSource, bool>>> expressions)
    {
        if (expressions?.Any() == true)
        {
            foreach (var expression in expressions)
            {
                source = source.Where(expression);
            }
        }

        return source;
    }
}
