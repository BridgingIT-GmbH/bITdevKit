// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public static class OrderOptionBuilder
{
    public static List<OrderOption<TEntity>> Build<TEntity>(IEnumerable<FilterOrderCriteria> criterias)
        where TEntity : class, IEntity
    {
        if (criterias == null || !criterias.Any())
        {
            return [];
        }

        return criterias.Select(criteria => BuildOrderOption<TEntity>(criteria)).ToList();
    }

    private static OrderOption<TEntity> BuildOrderOption<TEntity>(FilterOrderCriteria criteria)
        where TEntity : class, IEntity
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = BuildPropertyExpression(parameter, criteria.Field);
        var conversion = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda<Func<TEntity, object>>(conversion, parameter);

        return new OrderOption<TEntity>(lambda, criteria.Direction);
    }

    private static Expression BuildPropertyExpression(ParameterExpression parameter, string name)
    {
        return name.Split('.').Aggregate((Expression)parameter, Expression.Property);
    }
}