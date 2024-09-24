// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain.Model;
using Domain.Repositories;

public static partial class Extensions
{
    public static ILiteQueryable<TEntity> OrderByIf<TEntity>(
        this ILiteQueryable<TEntity> source,
        IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasOrders() == false)
        {
            return source;
        }

        var result = source;
        foreach (var order in (options.Orders.EmptyToNull() ?? new List<OrderOption<TEntity>>()).Insert(options?.Order)
                 ?.Where(o => o.Expression is not null))
        {
            result = order.Direction == OrderDirection.Ascending
                ? result.OrderBy(order
                    .Expression) // replace wit CompileFast()? https://github.com/dadhi/FastExpressionCompiler
                : result.OrderByDescending(order.Expression);
        }

        foreach (var order in (options.Orders.EmptyToNull() ?? new List<OrderOption<TEntity>>()).Insert(options?.Order)
                 ?.Where(o => !o.Ordering.IsNullOrEmpty()))
        {
            result = result is null
                ? result = source.OrderBy(order.Ordering) // of the form >   fieldname [ascending|descending], ...
                : result = result.OrderBy(order.Ordering);
        }

        return result;
    }
}