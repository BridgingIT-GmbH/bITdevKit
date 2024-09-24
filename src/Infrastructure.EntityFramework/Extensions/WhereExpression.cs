// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Linq.Expressions;

public static partial class Extensions
{
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