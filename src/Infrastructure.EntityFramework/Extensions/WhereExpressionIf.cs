// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the where expression if operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expression">The expression used by the operation.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Executes the where expressions if operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="expressions">The expressions used by the operation.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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
