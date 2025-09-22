// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a base implementation for Active Entity behaviors with no-op methods for all hooks.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class ActiveEntityBehaviorBase<TEntity> : IActiveEntityBehavior<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes before an entity is inserted. No-op by default.
    /// </summary>
    /// <param name="entity">The entity to be inserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an entity is inserted. No-op by default.
    /// </summary>
    /// <param name="entity">The inserted entity.</param>
    /// <param name="success">Indicates if the insert was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before an entity is updated. No-op by default.
    /// </summary>
    /// <param name="entity">The entity to be updated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an entity is updated. No-op by default.
    /// </summary>
    /// <param name="entity">The updated entity.</param>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before a set is updated. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeUpdateSetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an set is updated. No-op by default.
    /// </summary>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterUpdateSetAsync(bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before an entity is upserted. No-op by default.
    /// </summary>
    /// <param name="entity">The entity to be upserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an entity is upserted. No-op by default.
    /// </summary>
    /// <param name="entity">The upserted entity.</param>
    /// <param name="action">The action performed (Inserted/Updated).</param>
    /// <param name="success">Indicates if the upsert was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before an entity is deleted. No-op by default.
    /// </summary>
    /// <param name="entity">The entity to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an entity is deleted. No-op by default.
    /// </summary>
    /// <param name="entity">The deleted entity.</param>
    /// <param name="success">Indicates if the delete was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before an entity is deleted by its ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the entity to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeDeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an entity is deleted by its ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the deleted entity.</param>
    /// <param name="success">Indicates if the delete was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterDeleteAsync(object id, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before a set is deleted. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeDeleteSetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after an set is deleted. No-op by default.
    /// </summary>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterDeleteSetAsync(bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding a single entity by ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindOneAsync(object id, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding a single entity by ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindOneAsync(object id, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding a single entity by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding a single entity by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding a single entity by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding a single entity by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding a single entity by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding a single entity by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entities. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entities. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The found entities.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllAsync(IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entities by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entities by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The found entities.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entities with pagination. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entities with pagination. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entities and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllPagedAsync(IFindOptions<TEntity> options, ResultPaged<TEntity> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before projecting all entities. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after projecting all entities. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The projected entities.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before projecting all entities by specification. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after projecting all entities by specification. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The projected entities.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before projecting all entities by a collection of specifications. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after projecting all entities by a collection of specifications. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The projected entities.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before projecting all entities with pagination. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after projecting all entities with pagination. No-op by default.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing projected entities and count.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, ResultPaged<TProjection> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before checking if entities exist. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after checking if entities exist. No-op by default.
    /// </summary>
    /// <param name="exists">Indicates if entities exist.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterExistsAsync(bool exists, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before checking if an entity exists by ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the entity to check.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after checking if an entity exists by ID. No-op by default.
    /// </summary>
    /// <param name="id">The ID of the entity to check.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterExistsAsync(object id, bool exists, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before checking if an entity exists by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeExistsAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after checking if an entity exists by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterExistsAsync(ISpecification<TEntity> specification, bool exists, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before checking if an entity exists by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after checking if an entity exists by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, bool exists, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before checking if an entity exists by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeExistsAsync(FilterModel filter, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after checking if an entity exists by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterExistsAsync(FilterModel filter, bool exists, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before counting entities. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after counting entities. No-op by default.
    /// </summary>
    /// <param name="count">The count of entities.</param>
    /// <param name="success">Indicates if the count was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterCountAsync(long count, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before counting entities by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeCountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after counting entities by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="count">The count of entities.</param>
    /// <param name="success">Indicates if the count was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterCountAsync(ISpecification<TEntity> specification, long count, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsAsync<TId>(IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by specification. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsAsync<TId>(ISpecification<TEntity> specification, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by a collection of specifications. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsAsync<TId>(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by a filter model. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsAsync<TId>(FilterModel filter, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs with pagination. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs with pagination. No-op by default.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsPagedAsync<TId>(IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by specification with pagination. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by specification with pagination. No-op by default.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsPagedAsync<TId>(ISpecification<TEntity> specification, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by a collection of specifications with pagination. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by a collection of specifications with pagination. No-op by default.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsPagedAsync<TId>(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before finding all entity IDs by a filter model with pagination. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeFindAllIdsPagedAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after finding all entity IDs by a filter model with pagination. No-op by default.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterFindAllIdsPagedAsync<TId>(FilterModel filter, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes before a transaction is started. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> BeforeTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Executes after a transaction is completed (committed or rolled back). No-op by default.
    /// </summary>
    /// <param name="success">Indicates if the transaction was successful (committed).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual Task<Result> AfterTransactionAsync(bool success, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}