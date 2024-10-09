// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public static partial class Extensions
{
    public static IQueryable<TEntity> IncludeIf<TEntity>(this IQueryable<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options is null || options?.HasIncludes() == false)
        {
            return source;
        }

        foreach (var include in (options.Includes.EmptyToNull() ?? new List<IncludeOption<TEntity>>()).Insert(
                     options.Include))
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

        foreach (var include in (options.Includes.EmptyToNull() ?? new List<IncludeOption<TEntity>>()).Insert(
                     options.Include))
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