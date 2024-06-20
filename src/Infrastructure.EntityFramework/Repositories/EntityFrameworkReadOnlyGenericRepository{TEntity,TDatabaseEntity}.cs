// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public class EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TDatabaseEntity, TContext>(ILoggerFactory loggerFactory, TContext context) : EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext
    where TDatabaseEntity : class
{
}

public class EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity> : // TODO: rename to EntityFrameworkReadOnlykRepository + Obsolete
    IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    public EntityFrameworkReadOnlyGenericRepository(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));
        EnsureArg.IsNotNull(options.Mapper, nameof(options.Mapper));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    public EntityFrameworkReadOnlyGenericRepository(Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build())
    {
    }

    public EntityFrameworkReadOnlyGenericRepository(ILoggerFactory loggerFactory, DbContext context, IEntityMapper mapper = null)
        : this(o => o.LoggerFactory(loggerFactory).DbContext(context).Mapper(mapper))
    {
    }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    protected EntityFrameworkRepositoryOptions Options { get; }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(new[] { specification }, options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        if (options?.HasOrders() == true)
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .OrderByIf(options, this.Options.Mapper)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take).ToListAsyncSafe(cancellationToken).AnyContext())
                    .Select(d => this.Options.Mapper.Map<TEntity>(d));
        }
        else
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take).ToListAsyncSafe(cancellationToken).AnyContext())
                    .Select(d => this.Options.Mapper.Map<TEntity>(d)); // mapping needs to be done client-side, otherwise ef core sql translation error
        }
    }

    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([], projection, options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync(new[] { specification }, projection, options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.SafeNull().ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        if (options?.HasOrders() == true)
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .OrderByIf(options, this.Options.Mapper)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take).ToListAsyncSafe(cancellationToken).AnyContext())
                    .Select(d => this.Options.Mapper.Map<TEntity>(d)) // mapping needs to be done client-side, otherwise ef core sql translation error
                    .Select(e => projection.Compile().Invoke(e));     // thus the projection can also be only done client-side
        }
        else
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken).AnyContext())
                    .Select(d => this.Options.Mapper.Map<TEntity>(d)) // mapping needs to be done client-side, otherwise ef core sql translation error
                    .Select(e => projection.Compile().Invoke(e));     // thus the projection can also be only done client-side
        }
    }

    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public virtual async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new[] { specification }, cancellationToken).AnyContext();
    }

    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        return await this.Options.DbContext.Set<TDatabaseEntity>()
            .AsNoTracking()
            .WhereExpressions(expressions)
            .LongCountAsync(cancellationToken).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return null;
        }

        return this.Options.Mapper.Map<TEntity>(
            await this.Options.DbContext.FindAsync<TEntity, TDatabaseEntity>(
                this.ConvertEntityId(id),
                options,
                this.Options.Mapper,
                cancellationToken)
            .AnyContext());
    }

    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync(
            new[] { specification },
            options,
            cancellationToken).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s =>
            this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s)).ToList();

        return this.Options.Mapper.Map<TEntity>(
            await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .FirstOrDefaultAsync(cancellationToken).AnyContext());
    }

    public virtual async Task<bool> ExistsAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken: cancellationToken).AnyContext() is not null;
    }

    protected DbSet<TDatabaseEntity> GetDbSet() =>
        this.Options.DbContext.Set<TDatabaseEntity>();

    protected IDbConnection GetDbConnection() =>
        this.Options.DbContext.Database.GetDbConnection();

    protected IDbTransaction GetDbTransaction() =>
        this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();

    protected object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) && value?.GetType() == typeof(string))
        {
            // string to guid conversion
            return Guid.Parse(value.ToString());
        }
        else if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(int) && value?.GetType() == typeof(string))
        {
            // int to guid conversion
            return int.Parse(value.ToString());
        }
        else if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(long) && value?.GetType() == typeof(string))
        {
            // long to guid conversion
            return long.Parse(value.ToString());
        }

        return value;
    }
}