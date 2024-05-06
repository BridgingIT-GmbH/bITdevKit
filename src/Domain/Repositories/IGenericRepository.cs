// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Model;

public interface IGenericRepository<TEntity> : IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the entity for the provided id.
    /// </summary>
    /// <param name="id">The entity id to delete.</param>
    Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the provided entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}