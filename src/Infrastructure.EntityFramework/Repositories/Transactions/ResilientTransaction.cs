// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class ResilientTransaction
{
    private readonly DbContext context;

    private ResilientTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    public static ResilientTransaction Create(DbContext context)
    {
        return new ResilientTransaction(context);
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        if (this.context.Database.CurrentTransaction is null) // No current transaction
        {
            // Use of an EF Core resiliency strategy when using multiple DbContexts
            // within an explicit BeginTransaction():
            // https://docs.microsoft.com/ef/core/miscellaneous/connection-resiliency
            var strategy = this.context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await this.context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        await action().AnyContext();
                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(cancellationToken).AnyContext();

                        throw;
                    }
                }).AnyContext();
        }
        else
        {
            await action().AnyContext();
        }
    }

    public async Task<TEntity> ExecuteAsync<TEntity>(Func<Task<TEntity>> action, CancellationToken cancellationToken = default)
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
                    await using var transaction = await this.context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var result = await action().AnyContext();
                        await transaction.CommitAsync(cancellationToken);

                        return result;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(cancellationToken).AnyContext();

                        throw;
                    }
                }).AnyContext();
        }

        return await action().AnyContext();
    }
}