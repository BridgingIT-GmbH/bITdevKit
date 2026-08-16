// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    /// Executes the as no tracking if operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IQueryable<TEntity> AsNoTrackingIf<TEntity>(this DbSet<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options?.NoTracking == true)
        {
            return source.AsNoTracking();
        }

        return source;
    }

    /// <summary>
    /// Executes the as no tracking if operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="condition">The condition used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static IQueryable<TEntity> AsNoTrackingIf<TEntity>(
        this IQueryable<TEntity> source,
        bool condition) where TEntity : class, IEntity
    {
        return condition ? source.AsNoTracking() : source;
    }

    /// <summary>
    /// Executes the as no tracking if operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="mapper">The mapper used to transform values.</param>
    /// <returns>The result of the operation.</returns>
    public static IQueryable<TDatabaseEntity> AsNoTrackingIf<TEntity, TDatabaseEntity>(
        this DbSet<TDatabaseEntity> source,
        IFindOptions<TEntity> options,
        IEntityMapper mapper)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
    {
        if (options?.NoTracking == true)
        {
            return source.AsNoTracking();
        }

        return source;
    }
}
