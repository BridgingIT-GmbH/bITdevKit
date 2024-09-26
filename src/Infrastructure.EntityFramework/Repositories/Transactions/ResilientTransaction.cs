// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Common;
using Domain.Model;
using Microsoft.EntityFrameworkCore;

public class ResilientTransaction
{
    private readonly DbContext context;

    private ResilientTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    public static ResilientTransaction Create(DbContext dbContext)
    {
        return new ResilientTransaction(dbContext);
    }

    [Obsolete("Please use ResilientTransaction.For() from now on")]
    public static ResilientTransaction New(DbContext dbContext)
    {
        return new ResilientTransaction(dbContext);
    }

    public async Task ExecuteAsync(Func<Task> action)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        if (this.context.Database.CurrentTransaction is null)
        {
            // Use of an EF Core resiliency strategy when using multiple DbContexts
            // within an explicit BeginTransaction():
            // https://docs.microsoft.com/ef/core/miscellaneous/connection-resiliency
            var strategy = this.context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = this.context.Database.BeginTransaction();
                    try
                    {
                        await action().AnyContext();
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync().AnyContext();

                        throw;
                    }
                })
                .AnyContext();
        }
        else
        {
            await action().AnyContext();
        }
    }

    public async Task<TEntity> ExecuteAsync<TEntity>(Func<Task<TEntity>> action)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(action, nameof(action));

        if (this.context.Database.CurrentTransaction is null)
        {
            // Use of an EF Core resiliency strategy when using multiple DbContexts
            // within an explicit BeginTransaction():
            // https://docs.microsoft.com/ef/core/miscellaneous/connection-resiliency
            var strategy = this.context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = this.context.Database.BeginTransaction();
                    try
                    {
                        var result = await action().AnyContext();
                        transaction.Commit();

                        return result;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync().AnyContext();

                        throw;
                    }
                })
                .AnyContext();
        }

        return await action().AnyContext();
    }
}