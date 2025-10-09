// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides extension methods for performing result-based operations on repositories.
/// </summary>
public static class GenericRepositoryResultExtensions
{
    /// <summary>
    /// Inserts an entity and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the inserted entity.</returns>
    public static async Task<Result<TEntity>> InsertResultAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var insertedEntity = await source.InsertAsync(entity, cancellationToken).AnyContext();
            return Result<TEntity>.Success(insertedEntity);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Updates an entity and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the updated entity.</returns>
    public static async Task<Result<TEntity>> UpdateResultAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var updatedEntity = await source.UpdateAsync(entity, cancellationToken).AnyContext();
            return Result<TEntity>.Success(updatedEntity);
        }
        catch (Exception cex) when (cex.IsConcurrencyException())
        {
            return Result<TEntity>.Failure(cex.GetFullMessage(), new ConcurrencyError() { EntityType = typeof(TEntity).Name, EntityId = entity.Id?.ToString() });
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Upserts an entity and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the upserted entity and the action result.</returns>
    public static async Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertResultAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var (upsertedEntity, action) = await source.UpsertAsync(entity, cancellationToken).AnyContext();
            return Result<(TEntity, RepositoryActionResult)>.Success((upsertedEntity, action));
        }
        catch (Exception cex) when (cex.IsConcurrencyException())
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure(cex.GetFullMessage(), new ConcurrencyError() { EntityType = typeof(TEntity).Name, EntityId = entity.Id?.ToString() });
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Deletes an entity by its id and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="id">The id of the entity to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the deletion action result.</returns>
    public static async Task<Result<RepositoryActionResult>> DeleteResultAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        object id,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var result = await source.DeleteAsync(id, cancellationToken).AnyContext();
            if (result == RepositoryActionResult.Deleted)
            {
                return Result<RepositoryActionResult>.Success(result);
            }
            else if (result == RepositoryActionResult.NotFound)
            {
                return Result<RepositoryActionResult>.Failure("Entity not found", new NotFoundError());
            }
            else
            {
                return Result<RepositoryActionResult>.Failure("Entity not deleted", new Error("Entity not deleted"));
            }
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<RepositoryActionResult>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Deletes an entity and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the deletion action result.</returns>
    public static async Task<Result<(RepositoryActionResult, TEntity)>> DeleteResultAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var result = await source.DeleteAsync(entity, cancellationToken).AnyContext();
            if (result == RepositoryActionResult.Deleted)
            {
                return Result<(RepositoryActionResult, TEntity)>.Success((result, entity));
            }
            else if (result == RepositoryActionResult.NotFound)
            {
                return Result<(RepositoryActionResult, TEntity)>.Failure("Entity not found", new NotFoundError());
            }
            else
            {
                return Result<(RepositoryActionResult, TEntity)>.Failure("Entity not deleted", new Error("Entity not deleted"));
            }
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<(RepositoryActionResult, TEntity)>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    public static bool IsConcurrencyException(this Exception ex) =>
        ex is DBConcurrencyException || ex is ConcurrencyException ||
        ex.GetType().Name == "DbUpdateConcurrencyException" || ex.GetType().Name == "OptimisticConcurrencyException"; // check for EF Core by string
}