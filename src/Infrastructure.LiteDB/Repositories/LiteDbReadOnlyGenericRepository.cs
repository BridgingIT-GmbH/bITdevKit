// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using Microsoft.Extensions.Logging;

public class LiteDbReadOnlyGenericRepository<TEntity> :
    IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    public LiteDbReadOnlyGenericRepository(ILiteDbRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    public LiteDbReadOnlyGenericRepository(Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build())
    {
    }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    protected ILiteDbRepositoryOptions Options { get; }

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
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                //.DistinctIf(options)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
        }
        else
        {
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                //.DistinctIf(options)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
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
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .Select(projection)
                //.DistinctIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
        }
        else
        {
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                .Select(projection)
                //.DistinctIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
        }
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

        return await this.FindOneAsync(
            new Specification<TEntity>(e => e.Id == this.ConvertEntityId(id)),
            options,
            cancellationToken).AnyContext();
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
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.SafeNull().ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            //.IncludeIf(options)
            .WhereExpressions(expressions)
            .FirstOrDefault());
    }

    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext() is not null;
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

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            .WhereExpressions(expressions)
            .Count());
    }

    private object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) && value?.GetType() == typeof(string))
        {
            // string to guid conversion
            return Guid.Parse(value.ToString());
        }

        return value;
    }
}