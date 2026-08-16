// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Defines operations for i database transaction.
/// </summary>
public interface IDatabaseTransaction
{
    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Represents execute scoped.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;
}
