// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class DatabaseTransaction : IDatabaseTransaction
{
    private readonly DbContext context;

    public DatabaseTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext(), cancellationToken).AnyContext();
    }

    public async Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(action, nameof(action));

        return await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext(), cancellationToken).AnyContext();
    }
}