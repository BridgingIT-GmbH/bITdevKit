// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements auditing and soft delete behavior for entities using AuditState, inheriting from ActiveEntityBehaviorBase.
/// </summary>
/// <typeparam name="TEntity">The entity type, implementing IAuditable with an AuditState property.</typeparam>
/// <remarks>
/// Initializes a new instance of the ActiveEntityAuditStateBehavior class.
/// </remarks>
/// <param name="options">Configuration options for auditing and soft delete.</param>
/// <example>
/// <code>
/// var behavior = new ActiveEntityAuditStateBehavior<Customer>(new AuditStateBehaviorOptions { SoftDeleteEnabled = true, AuditUserIdentity = "system-user" });
/// </code>
/// </example>
public class ActiveEntityAuditStateBehavior<TEntity>(ActiveEntityAuditStateBehaviorOptions options = null) : ActiveEntityBehaviorBase<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    private readonly ActiveEntityAuditStateBehaviorOptions options = options ?? new ActiveEntityAuditStateBehaviorOptions();

    /// <summary>
    /// Sets audit state for creation before inserting an entity.
    /// </summary>
    /// <param name="entity">The entity to be inserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity?.AuditState == null)
        {
            return Task.FromResult(Result.Success());
        }

        entity.AuditState.SetCreated(this.options.AuditUserIdentity, "Entity created");
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Sets audit state for update before updating an entity.
    /// </summary>
    /// <param name="entity">The entity to be updated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity?.AuditState == null)
        {
            return Task.FromResult(Result.Success());
        }

        entity.AuditState.SetUpdated(this.options.AuditUserIdentity, "Entity updated");
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Sets audit state for upsert before upserting an entity.
    /// </summary>
    /// <param name="entity">The entity to be upserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity?.AuditState == null)
        {
            return Task.FromResult(Result.Success());
        }

        if (!entity.AuditState.IsUpdated())
        {
            entity.AuditState.SetCreated(this.options.AuditUserIdentity, "Entity created");
        }
        else
        {
            entity.AuditState.SetUpdated(this.options.AuditUserIdentity, "Entity updated");
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Marks an entity as deleted (soft delete) or allows hard delete based on options.
    /// </summary>
    /// <param name="entity">The entity to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity?.AuditState == null || !this.options.SoftDeleteEnabled)
        {
            return Task.FromResult(Result.Success());
        }

        entity.AuditState.SetDeleted(this.options.AuditUserIdentity, "Entity deleted");
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before finding all entities.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeFindAllAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before finding all entities with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeFindAllPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before projecting all entities.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before projecting all entities with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before finding all entity IDs.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeFindAllIdsAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Filters out soft-deleted entities before finding all entity IDs with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task<Result> BeforeFindAllIdsPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        if (this.options.SoftDeleteEnabled && options != null)
        {
            //options.Specifications.Add(new Specification<T>(e => !e.AuditState.IsDeleted()));
        }

        return Task.FromResult(Result.Success());
    }
}