// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public interface IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default);

    Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begins a new transaction and returns it for manual control.
    ///     Use this when you need explicit control over commit/rollback (e.g., with ResultOperationScope).
    /// </summary>
    Task<IRepositoryTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Represents a transaction scope with explicit commit/rollback control.
/// </summary>
public interface IRepositoryTransactionScope
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}