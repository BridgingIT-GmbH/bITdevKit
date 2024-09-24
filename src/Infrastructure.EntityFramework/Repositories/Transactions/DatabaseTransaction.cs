// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Common;
using Domain.Model;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

public class DatabaseTransaction : IDatabaseTransaction
{
    private readonly DbContext context;

    public DatabaseTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    [Obsolete("Please use ctor which only accepts a dbContext from now on")]
    public DatabaseTransaction(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.context = options.DbContext;
    }

    [Obsolete("Please use ctor which only accepts a dbContext from now on")]
    public EntityFrameworkRepositoryOptions Options { get; }

    public async Task ExecuteScopedAsync(Func<Task> action)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext())
            .AnyContext();
    }

    [Obsolete("Please use ExecuteScopedAsync from now on")]
    public async Task<TEntity> ExecuteScopedWithResultAsync<TEntity>(Func<Task<TEntity>> action)
        where TEntity : class, IEntity
    {
        return await this.ExecuteScopedAsync(action).AnyContext();
    }

    public async Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(action, nameof(action));

        return await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext())
            .AnyContext();
    }
}