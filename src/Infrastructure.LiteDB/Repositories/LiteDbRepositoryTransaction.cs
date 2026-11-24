// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Preserves repository transaction ownership across awaited operations in one scoped session.</summary>
/// <typeparam name="TEntity">The repository entity type.</typeparam>
/// <param name="session">The session shared by the repositories participating in this transaction.</param>
public sealed class LiteDbRepositoryTransaction<TEntity>(LiteDbDatabaseSession session) : IRepositoryTransaction<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await this.ExecuteAsync(async () => { await action().ConfigureAwait(false); return (TEntity)null; }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<TEntity> ExecuteScopedAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken = default) => this.ExecuteAsync(action, cancellationToken);

    /// <inheritdoc />
    public async Task<ITransactionOperationScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        var owns = !session.HasTransaction;
        await session.BeginAsync(cancellationToken).ConfigureAwait(false);
        return new LiteDbRepositoryOperationScope(session, owns);
    }

    private async Task<TEntity> ExecuteAsync(Func<Task<TEntity>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var scope = await this.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await action().ConfigureAwait(false);
            await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            try { await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception rollback) { exception.Data["LiteDbRollbackFailure"] = rollback; }

            throw;
        }
    }
}

/// <summary>Completes only its own repository transaction and rolls back an abandoned scope.</summary>
/// <param name="session">The shared session.</param>
/// <param name="ownsTransaction">Whether this operation opened the transaction.</param>
public sealed class LiteDbRepositoryOperationScope(LiteDbDatabaseSession session, bool ownsTransaction) : ITransactionOperationScope, IAsyncDisposable
{
    private bool completed;

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (this.completed) { return; }

        if (ownsTransaction) { await session.CompleteAsync(true, cancellationToken).ConfigureAwait(false); }

        this.completed = true;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (this.completed) { return; }

        if (ownsTransaction) { await session.CompleteAsync(false, CancellationToken.None).ConfigureAwait(false); }
        else { await session.AbortScopedAsync(new InvalidOperationException("A nested repository scope failed.")).ConfigureAwait(false); }

        this.completed = true;
    }

    /// <summary>Rolls back unfinished work without observing a canceled caller token.</summary>
    /// <returns>The cleanup task.</returns>
    public async ValueTask DisposeAsync() => await this.RollbackAsync().ConfigureAwait(false);
}
