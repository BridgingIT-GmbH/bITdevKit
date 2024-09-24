// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Model;

/// <summary>
///     Defines a generic repository interface for performing CRUD operations on entities.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IGenericRepository<TEntity> : IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, with the inserted entity.</returns>
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">An optional token to cancel the operation.</param>
    /// <returns>The updated entity.</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken">
    ///     Optional. A <see cref="CancellationToken" /> to observe while waiting for the task to
    ///     complete.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> that represents the asynchronous operation, containing a tuple with the entity and the
    ///     action result.
    /// </returns>
    Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the entity for the provided id.
    /// </summary>
    /// <param name="id">The entity id to delete.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <return>The result of the delete operation.</return>
    Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the provided entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <return>A Task representing the asynchronous operation, containing the result of the action.</return>
    Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}