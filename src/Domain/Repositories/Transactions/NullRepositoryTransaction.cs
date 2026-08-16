// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Represents null repository transaction.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class NullRepositoryTransaction<TEntity> : IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellation">The cancellation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellation = default)
    {
        await action();
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellation">The cancellation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellation = default)
    {
        return await action();
    }

    /// <summary>
    /// Executes the begin operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<ITransactionOperationScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITransactionOperationScope>(new NullTransactionScope());
    }
}
