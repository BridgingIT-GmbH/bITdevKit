// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Runs delegates inside an existing session transaction without automatically committing or retrying them.</summary>
/// <param name="session">The session that owns the active native transaction.</param>
/// <remarks>Complete the transaction through its provider or session. An exception rolls back native work and leaves the session awaiting explicit rollback.</remarks>
public sealed class LiteDbDatabaseTransaction(LiteDbDatabaseSession session) : IDatabaseTransaction
{
    /// <inheritdoc />
    public async Task ExecuteScopedAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        session.EnsureTransaction();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await session.AbortScopedAsync(exception).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TEntity> ExecuteScopedAsync<TEntity>(Func<Task<TEntity>> action, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(action);
        session.EnsureTransaction();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await session.AbortScopedAsync(exception).ConfigureAwait(false);
            throw;
        }
    }
}
