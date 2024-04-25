// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

public static partial class Extensions
{
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