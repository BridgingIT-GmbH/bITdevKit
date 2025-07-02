// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// A builder class for creating a list of <see cref="IncludeOption{TEntity}"/> instances from a collection of include paths.
/// </summary>
public static class HierarchyOptionBuilder
{
    /// <summary>
    /// Builds a list of IncludeOptions for the given entity type based on the provided include paths.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="hierarchy">A collection of string paths representing the properties to include.</param>
    /// <returns>A list of IncludeOption<TEntity> based on the provided include paths. If the includes collection is null or empty, an empty list is returned.</returns>
    public static HierarchyOption<TEntity> Build<TEntity>(string hierarchy, int maxDepth)
        where TEntity : class, IEntity
    {
        if (hierarchy == null || !hierarchy.Any())
        {
            return null;
        }

        return BuildHierarchyOption<TEntity>(hierarchy, maxDepth);
    }

    private static HierarchyOption<TEntity> BuildHierarchyOption<TEntity>(string hierarchy, int maxDepth)
        where TEntity : class, IEntity
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = BuildPropertyExpression(parameter, hierarchy);
        var lambda = Expression.Lambda<Func<TEntity, object>>(property, parameter);

        return new HierarchyOption<TEntity>(lambda, maxDepth);
    }

    private static Expression BuildPropertyExpression(ParameterExpression parameter, string include)
    {
        return include.Split('.').Aggregate((Expression)parameter, Expression.Property);
    }
}