// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using EnsureThat;

[Obsolete("Use GenericRepositoryIncludeBehavior instead")]
public class GenericRepositoryIncludeDecorator<TEntity> : RepositoryIncludeBehavior<TEntity>
    where TEntity : class, IEntity
{
    public GenericRepositoryIncludeDecorator(Expression<Func<TEntity, object>> expression, IGenericRepository<TEntity> inner)
        : base(expression, inner)
    {
    }

    public GenericRepositoryIncludeDecorator(IEnumerable<Expression<Func<TEntity, object>>> expressions, IGenericRepository<TEntity> inner)
        : base(expressions, inner)
    {
    }
}

/// <summary>
/// <para>Decorates an <see cref="IGenericRepository{TEntity}"/>.</para>
/// <para>
///    .-----------.
///    | Decorator |
///    .-----------.        .------------.
///          `------------> | decoratee  |
///            (forward)    .------------.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <seealso cref="IGenericRepository{TEntity}" />
public class RepositoryIncludeBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public RepositoryIncludeBehavior(
        Expression<Func<TEntity, object>> expression,
        IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Expressions = new[] { expression };
        this.Inner = inner;
    }

    public RepositoryIncludeBehavior(
        IEnumerable<Expression<Func<TEntity, object>>> expressions,
        IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(expressions, nameof(expressions));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Expressions = expressions;
        this.Inner = inner;
    }

    protected IGenericRepository<TEntity> Inner { get; }

    protected IEnumerable<Expression<Func<TEntity, object>>> Expressions { get; }

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);
        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new[] { specification }, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    private IFindOptions<TEntity> EnsureOptions(IFindOptions<TEntity> options)
    {
        if (options is null)
        {
            options = new FindOptions<TEntity>();
        }

        if (options.Includes is null)
        {
            options.Includes = new List<IncludeOption<TEntity>>();
        }

        options.Includes = options.Includes.InsertRange(
            this.Expressions.Select(e => new IncludeOption<TEntity>(e)));

        return options;
    }
}