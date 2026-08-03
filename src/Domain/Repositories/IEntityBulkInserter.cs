// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Inserts entity batches through an explicitly configured high-performance persistence capability.
/// </summary>
/// <typeparam name="TEntity">The entity type to insert.</typeparam>
/// <example>
/// <code>
/// public sealed class ImportHandler(IEntityBulkInserter&lt;Order&gt; bulkInserter)
/// {
///     public Task&lt;Result&lt;long&gt;&gt; HandleAsync(
///         IEnumerable&lt;Order&gt; orders,
///         CancellationToken cancellationToken = default)
///     {
///         return bulkInserter.InsertAsync(orders, cancellationToken);
///     }
/// }
/// </code>
/// </example>
public interface IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Inserts the provided entities using the configured provider-specific bulk insert implementation.
    /// </summary>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result contains the number of inserted rows.</returns>
    /// <example>
    /// <code>
    /// var result = await bulkInserter.InsertAsync(entities, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<long>> InsertAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default
    );
}
