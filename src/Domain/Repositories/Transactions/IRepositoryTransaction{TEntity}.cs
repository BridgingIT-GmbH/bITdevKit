// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Defines operations for i repository transaction.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begins a new transaction and returns it for manual control.
    ///     Use this when you need explicit control over commit/rollback (e.g., with ResultOperationScope).
    /// </summary>
    Task<ITransactionOperationScope> BeginAsync(CancellationToken cancellationToken = default);
}
