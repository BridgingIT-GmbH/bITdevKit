// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Represents entity framework database transaction.
/// </summary>
public class EntityFrameworkDatabaseTransaction : IDatabaseTransaction
{
    private readonly DbContext context;

    /// <summary>
    /// Initializes a new instance of the <c>EntityFrameworkDatabaseTransaction</c> class.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    public EntityFrameworkDatabaseTransaction(DbContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(action, nameof(action));

        await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext(), cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        EnsureArg.IsNotNull(action, nameof(action));

        return await ResilientTransaction.Create(this.context)
            .ExecuteAsync(async () => await action().AnyContext(), cancellationToken).AnyContext();
    }
}
