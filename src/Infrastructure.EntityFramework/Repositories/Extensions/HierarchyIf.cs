// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    ///    Includes the hierarchy if the options specify a hierarchy.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> HierarchyIf<TEntity>(this IQueryable<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasHierarchy() == false)
        {
            return source;
        }

        var query = source.Include(e => options.Hierarchy.Expression);
        for (var i = 1; i < options.Hierarchy.MaxDepth; i++)
        {
            query = query.ThenInclude(e => options.Hierarchy.Expression);
        }

        return query;
    }

    /// <summary>
    ///   Includes the hierarchy if the options specify a hierarchy.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TDatabaseEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <param name="mapper"></param>
    /// <returns></returns>
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

        var expr = mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(options.Hierarchy.Expression);
        var query = source.Include(e => expr);
        for (var i = 1; i < options.Hierarchy.MaxDepth; i++)
        {
            query = query.ThenInclude(e => expr);
        }

        return query;
    }
}