// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using EnsureThat;
using Microsoft.Extensions.Logging;

[Obsolete("Use GenericRepositoryNoTrackingBehavior instead")]
public class GenericRepositoryNoTrackingDecorator<TEntity>(IGenericRepository<TEntity> inner) : RepositoryNoTrackingBehavior<TEntity>(inner)
    where TEntity : class, IEntity
{
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
public class RepositoryNoTrackingBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public RepositoryNoTrackingBehavior(IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Inner = inner;
    }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    protected IGenericRepository<TEntity> Inner { get; }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
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

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
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
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new FindOptions<TEntity>();
        options.NoTracking = true;

        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
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
}