// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using System.Linq.Expressions;
using Common;
using Domain.Model;
using Domain.Repositories;
using Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public class EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context)
    : EntityFrameworkReadOnlyGenericRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext { }

public class EntityFrameworkReadOnlyGenericRepository<TEntity>
    : // TODO: rename to EntityFrameworkReadOnlykRepository + Obsolete
        IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    public EntityFrameworkReadOnlyGenericRepository(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    public EntityFrameworkReadOnlyGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    public EntityFrameworkReadOnlyGenericRepository(ILoggerFactory loggerFactory, DbContext context)
        : this(o => o.LoggerFactory(loggerFactory).DbContext(context)) { }

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
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await this.Options.DbContext.Set<TEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .DistinctIf(options)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext();
        }

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options)
            .WhereExpressions(expressions)
            .DistinctIf(options)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToListAsyncSafe(cancellationToken)
            .AnyContext();
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
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await this.Options.DbContext.Set<TEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .Select(projection)
                .DistinctIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext();
        }

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options)
            .WhereExpressions(expressions)
            .Select(projection)
            .DistinctIf(options, this.Options.Mapper)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToListAsyncSafe(cancellationToken)
            .AnyContext();
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

        return await this.Options.DbContext.FindAsync(this.ConvertEntityId(id), options, cancellationToken)
            .AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync(new[] { specification }, options, cancellationToken).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options)
            .WhereExpressions(expressions)
            .FirstOrDefaultAsync(cancellationToken)
            .AnyContext();
    }

    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        var result = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken)
            .AnyContext() is not null;

        return result;
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
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTracking()
            .WhereExpressions(expressions)
            .LongCountAsync(cancellationToken)
            .AnyContext();
    }

    protected DbSet<TEntity> GetDbSet()
    {
        return this.Options.DbContext.Set<TEntity>();
    }

    protected IDbConnection GetDbConnection()
    {
        return this.Options.DbContext.Database.GetDbConnection();
    }

    protected IDbTransaction GetDbTransaction()
    {
        return this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();
    }

    private object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) &&
            value?.GetType() == typeof(string))
        {
            // string to guid conversion
            return Guid.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(int) &&
            value?.GetType() == typeof(string))
        {
            // int to guid conversion
            return int.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(long) &&
            value?.GetType() == typeof(string))
        {
            // long to guid conversion
            return long.Parse(value.ToString());
        }

        return value;
    }
}