// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;

/// <summary>
/// Inserts flat entity batches through an explicit high-performance provider path.
/// </summary>
/// <typeparam name="TEntity">The entity type to insert.</typeparam>
/// <example>
/// <code>
/// var result = await bulkInserter.InsertAsync(entities, cancellationToken);
/// </code>
/// </example>
public interface IEntityBulkInserter<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Inserts the provided entities using a provider-specific bulk insert implementation.
    /// </summary>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the number of inserted rows.</returns>
    /// <example>
    /// <code>
    /// var result = await bulkInserter.InsertAsync(new[] { entity1, entity2 });
    /// </code>
    /// </example>
    Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}
