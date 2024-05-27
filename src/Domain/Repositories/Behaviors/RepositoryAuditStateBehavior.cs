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
public class RepositoryAuditStateBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    private readonly AuditStateByType byType;
    private readonly ICurrentUserAccessor currentUserAccessor;

    public RepositoryAuditStateBehavior(
        IGenericRepository<TEntity> ínner,
        AuditStateByType byType = AuditStateByType.ByUserName,
        ICurrentUserAccessor currentUserAccessor = null)
        : this(ínner)
    {
        this.byType = byType;
        this.currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();
    }

    public RepositoryAuditStateBehavior(
        IGenericRepository<TEntity> ínner)
    {
        EnsureArg.IsNotNull(ínner, nameof(ínner));

        this.Inner = ínner;
    }

    protected IGenericRepository<TEntity> Inner { get; }

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken: cancellationToken).AnyContext();
        if (entity is not null)
        {
            return await this.DeleteAsync(entity, cancellationToken).AnyContext();
        }

        return RepositoryActionResult.None;
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetDeleted(this.GetByValue());

        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(
            [],
            options,
            cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(
            new List<ISpecification<TEntity>>(new[] { specification }),
            options,
            cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(
            specifications,
            options,
            cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync(
            [],
            projection,
            options,
            cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync(
            new List<ISpecification<TEntity>>(new[] { specification }),
            projection,
            options,
            cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(
            specifications,
            projection,
            options,
            cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetCreated(this.GetByValue());

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specification, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    private string GetByValue()
    {
        switch (this.byType)
        {
            case AuditStateByType.ByUserName:
                return this.currentUserAccessor.UserName;
            case AuditStateByType.ByEmail:
                return this.currentUserAccessor.Email;
            case AuditStateByType.ByUserId:
                break;
            default:
                return this.currentUserAccessor.UserId;
        }

        return this.currentUserAccessor.UserId;
    }
}

public enum AuditStateByType
{
    ByUserId = 0,
    ByUserName = 1,
    ByEmail = 2
}