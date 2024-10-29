// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Provides functionality to build a list of <see cref="OrderOption{TEntity}"/>
/// from a collection of <see cref="FilterOrderCriteria"/>.
/// </summary>
public static class OrderOptionBuilder
{
    /// <summary>
    /// Builds a list of order options from the provided filter order criteria.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="criterias">The collection of filter order criteria used to build order options.</param>
    /// <returns>A list of OrderOption objects for the specified entity type.</returns>
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