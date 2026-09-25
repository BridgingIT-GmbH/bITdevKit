// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Builds hierarchy options from navigation-property paths.
/// </summary>
public static class HierarchyOptionBuilder
{
    /// <summary>
    /// Builds a list of IncludeOptions for the given entity type based on the provided include paths.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="hierarchy">A collection of string paths representing the properties to include.</param>
    /// <param name="maxDepth">The maximum hierarchy depth to load.</param>
    /// <returns>A hierarchy option for the path, or <see langword="null"/> when the path is empty.</returns>
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