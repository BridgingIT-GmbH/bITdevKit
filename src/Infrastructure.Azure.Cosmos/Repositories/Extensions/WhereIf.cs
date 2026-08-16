// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Cosmos.Repositories;

using System.Linq.Expressions;

public static partial class Extensions
{
    /// <summary>
    /// Executes the where if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="predicate">The expression used to test each value.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        bool? condition = true)
    {
        if (condition == true && predicate is not null)
        {
            return source.Where(predicate);
        }

        return source;
    }

    /// <summary>
    /// Executes the where if operation.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="predicates">The predicates used by the operation.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        IEnumerable<Expression<Func<T, bool>>> predicates,
        bool? condition = true)
    {
        if (condition == true && predicates?.Any() == true)
        {
            foreach (var predicate in predicates)
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
