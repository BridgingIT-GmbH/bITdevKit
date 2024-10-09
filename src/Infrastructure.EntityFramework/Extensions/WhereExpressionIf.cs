// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    public static IQueryable<TSource> WhereExpressionIf<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> expression,
        bool? condition = true)
    {
        if (condition == true && expression is not null)
        {
            return source.Where(expression);
        }

        return source;
    }

    public static IQueryable<TSource> WhereExpressionsIf<TSource>(
        this IQueryable<TSource> source,
        IEnumerable<Expression<Func<TSource, bool>>> expressions,
        bool? condition = true)
    {
        if (condition == true && expressions?.Any() == true)
        {
            foreach (var predicate in expressions)
            {
                source = source.Where(predicate);
            }
        }

        return source;
    }

    //public static IEnumerable<T> WhereIf<T>(
    //    this IEnumerable<T> source,
    //    Func<T, bool> predicate,
    //    bool? condition = true)
    //{
    //    if (condition == true && predicate is not null)
    //    {
    //        return source.Where(predicate);
    //    }

    //    return source;
    //}

    //public static IEnumerable<T> WhereIf<T>(
    //    this IEnumerable<T> source,
    //    IEnumerable<Func<T, bool>> predicates,
    //    bool? condition = true)
    //{
    //    if (condition == true && predicates?.Any() == true)
    //    {
    //        foreach (var predicate in predicates)
    //        {
    //            source = source.Where(predicate);
    //        }
    //    }

    //    return source;
    //}
}