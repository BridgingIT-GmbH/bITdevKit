﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class EntityFrameworkTransactionWrapper<TEntity, TContext>(TContext context)
    : EntityFrameworkTransaction<TEntity>(context)
    where TEntity : class, IEntity
    where TContext : DbContext
{ }

[Obsolete("Use EntityFrameworkTransaction instead")]
public class GenericRepositoryTransaction<TEntity>(DbContext context) : EntityFrameworkTransaction<TEntity>(context)
    where TEntity : class, IEntity
{ }

public class EntityFrameworkTransaction<TEntity> : IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    private readonly DbContext context;

    public EntityFrameworkTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    public async Task ExecuteScopedAsync(Func<Task> action)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext())
            .AnyContext();
    }

    public async Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        return await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext())
            .AnyContext();
    }
}