// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public static partial class Extensions
{
    public static IQueryable<TEntity> AsNoTrackingIf<TEntity>(this DbSet<TEntity> source, IFindOptions<TEntity> options)
        where TEntity : class, IEntity
    {
        if (options?.NoTracking == true)
        {
            return source.AsNoTracking();
        }

        return source;
    }

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