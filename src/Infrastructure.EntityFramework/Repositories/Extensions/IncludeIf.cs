// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class Extensions
{
    /// <summary>
    ///    Includes the specified navigation properties if the options specify includes.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> IncludeIf<TEntity>(this IQueryable<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasIncludes() == false)
        {
            return source;
        }

        foreach (var include in (options.Includes.EmptyToNull() ?? []).Insert(options.Include))
        {
            if (include.Expression is not null)
            {
                source = source.Include(include.Expression);
            }

            if (!include.Path.IsNullOrEmpty())
            {
                source = source.Include(include.Path);
            }
        }

        return source;
    }

    /// <summary>
    ///   Includes the specified navigation properties if the options specify includes.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TDatabaseEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <param name="mapper"></param>
    /// <returns></returns>
    public static IQueryable<TDatabaseEntity> IncludeIf<TEntity, TDatabaseEntity>(
        this IQueryable<TDatabaseEntity> source,
        IFindOptions<TEntity> options,
        IEntityMapper mapper)
        where TEntity : class, IEntity
        where TDatabaseEntity : class
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (options is null || options?.HasIncludes() == false)
        {
            return source;
        }

        foreach (var include in (options.Includes.EmptyToNull() ?? []).Insert(options.Include))
        {
            if (include.Expression is not null)
            {
                source = source.Include(
                    mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(include.Expression));
            }

            if (!include.Path.IsNullOrEmpty())
            {
                source = source.Include(include.Path);
            }
        }

        return source;
    }
}