// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Builds include options from navigation-property paths.
/// </summary>
public static class IncludeOptionBuilder
{
    /// <summary>
    /// Builds a list of IncludeOptions for the given entity type based on the provided include paths.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="includes">A collection of string paths representing the properties to include.</param>
    /// <returns>A list of <see cref="IncludeOption{TEntity}"/> instances based on the paths, or an empty list when no paths are supplied.</returns>
    public static List<IncludeOption<TEntity>> Build<TEntity>(IEnumerable<string> includes)
        where TEntity : class, IEntity
    {
        if (includes == null || !includes.Any())
        {
            return [];
        }

        return includes.Select(BuildIncludeOption<TEntity>).ToList();
    }

    private static IncludeOption<TEntity> BuildIncludeOption<TEntity>(string include)
        where TEntity : class, IEntity
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = BuildPropertyExpression(parameter, include);
        var lambda = Expression.Lambda<Func<TEntity, object>>(property, parameter);

        return new IncludeOption<TEntity>(lambda);
    }

    private static Expression BuildPropertyExpression(ParameterExpression parameter, string include)
    {
        return include.Split('.').Aggregate((Expression)parameter, Expression.Property);
    }
}
