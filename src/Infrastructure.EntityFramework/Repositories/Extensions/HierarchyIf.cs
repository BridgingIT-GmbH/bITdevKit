// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Provides extension methods for Entity Framework queries.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Includes the hierarchy if the options specify a hierarchy.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="options">The find options containing hierarchy settings.</param>
    /// <returns>The queryable with included hierarchy.</returns>
    public static IQueryable<TEntity> HierarchyIf<TEntity>(
        this IQueryable<TEntity> source,
        IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasHierarchy() == false)
        {
            return source;
        }

        if (options.Hierarchy.Expression is not null)
        {
            var propertyName = GetPropertyNameFromExpression(options.Hierarchy.Expression);
            var path = string.Join(".", Enumerable.Repeat(propertyName, options.Hierarchy.MaxDepth));

            return source.Include(path);
        }

        if (!options.Hierarchy.Path.IsNullOrEmpty())
        {
            var pathSegment = options.Hierarchy.Path.Split('.').Last();
            var path = string.Join(".", Enumerable.Repeat(pathSegment, options.Hierarchy.MaxDepth));

            return source.Include(path);
        }

        return source;
    }

    /// <summary>
    /// Includes the hierarchy if the options specify a hierarchy.
    /// </summary>
    /// <typeparam name="TEntity">The domain entity type.</typeparam>
    /// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="options">The find options containing hierarchy settings.</param>
    /// <param name="mapper">The entity mapper.</param>
    /// <returns>The queryable with included hierarchy.</returns>
    public static IQueryable<TDatabaseEntity> HierarchyIf<TEntity, TDatabaseEntity>(
        this IQueryable<TDatabaseEntity> source,
        IFindOptions<TEntity> options,
        IEntityMapper mapper)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (options is null || options?.HasHierarchy() == false)
        {
            return source;
        }

        if (options.Hierarchy.Expression is not null)
        {
            var expr = mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(options.Hierarchy.Expression);
            var propertyName = GetPropertyNameFromExpression(expr);
            var path = string.Join(".", Enumerable.Repeat(propertyName, options.Hierarchy.MaxDepth));

            return source.Include(path);
        }

        if (!options.Hierarchy.Path.IsNullOrEmpty())
        {
            var pathSegment = options.Hierarchy.Path.Split('.').Last();
            var path = string.Join(".", Enumerable.Repeat(pathSegment, options.Hierarchy.MaxDepth));

            return source.Include(path);
        }

        return source;
    }

    private static string GetPropertyNameFromExpression<TSource, TProperty>(Expression<Func<TSource, TProperty>> expression)
    {
        if (expression.Body is UnaryExpression unaryExpression)
        {
            if (unaryExpression.Operand is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
        }
        else if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException("Expression must be a property access", nameof(expression));
    }
}