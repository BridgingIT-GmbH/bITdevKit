// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Linq.Dynamic.Core;

public static partial class Extensions
{
    public static IOrderedQueryable<TEntity> OrderByIf<TEntity>(
        this IQueryable<TEntity> source,
        IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasOrders() == false)
        {
            return
                source as IOrderedQueryable<TEntity>; // TODO: this returns null, find a way to return an IOrderedQueryable event if no orders are provided. possible?
        }

        IOrderedQueryable<TEntity> result = null;
        foreach (var order in (options.Orders.EmptyToNull() ?? []).Insert(options?.Order)
                 ?.Where(o => o.Expression is not null))
        {
            result = result is null ? order.Direction == OrderDirection.Ascending
                    ? source.OrderBy(order
                        .Expression) // replace wit CompileFast()? https://github.com/dadhi/FastExpressionCompiler
                    : source.OrderByDescending(order.Expression) :
                order.Direction == OrderDirection.Ascending ? result.ThenBy(order.Expression) :
                result.ThenByDescending(order.Expression);
        }

        foreach (var order in (options.Orders.EmptyToNull() ?? []).Insert(options?.Order)
                 ?.Where(o => !o.Ordering.IsNullOrEmpty()))
        {
            result = result is null
                ? result = source.OrderBy(order.Ordering) // of the form >   fieldname [ascending|descending], ...
                : result = result.OrderBy(order.Ordering);
        }

        return result;
    }

    public static IOrderedQueryable<TDatabaseEntity> OrderByIf<TEntity, TDatabaseEntity>(
        this IQueryable<TDatabaseEntity> source,
        IFindOptions<TEntity> options,
        IEntityMapper mapper)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasOrders() == false)
        {
            return
                source as IOrderedQueryable<TDatabaseEntity>; // TODO: this returns null, find a way to return an IOrderedQueryable event if no orders are provided. possible?
        }

        IOrderedQueryable<TDatabaseEntity> result = null;
        foreach (var order in (options.Orders.EmptyToNull() ?? []).Insert(options?.Order))
        {
            result = result is null
                ? order.Direction == OrderDirection.Ascending
                    ? source.OrderBy(mapper
                        .MapExpression<
                            Expression<Func<TDatabaseEntity, object>>>(order
                            .Expression)) // replace wit CompileFast()? https://github.com/dadhi/FastExpressionCompiler
                    : source.OrderByDescending(
                        mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression))
                : order.Direction == OrderDirection.Ascending
                    ? result.ThenBy(mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression))
                    : result.ThenByDescending(
                        mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression));
        }

        return result;
    }
}