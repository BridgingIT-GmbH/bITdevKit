// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public class NullRepositoryTransaction<TEntity> : IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellation = default)
    {
        await action();
    }

    public async Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellation = default)
    {
        return await action();
    }

    public Task<IRepositoryTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IRepositoryTransactionScope>(new NullRepositoryTransactionScope());
    }
}

internal class NullRepositoryTransactionScope : IRepositoryTransactionScope
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}