// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>Uses actual source transaction outcomes and post-commit row membership rather than save success or savepoint names.</summary>
/// <example><code>options.AddInterceptors(transactionObserver);</code></example>
public sealed class AuditTransactionObserver<TContext>(ChangeHistoryAuditBridge<TContext> bridge) : DbTransactionInterceptor where TContext : DbContext
{
    /// <inheritdoc />
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        bridge.Started(eventData.Context, eventData.TransactionId);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        bridge.Started(eventData.Context, eventData.TransactionId);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => bridge.Committed(eventData.Context, eventData.TransactionId);

    /// <inheritdoc />
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        => bridge.CommittedAsync(eventData.Context, eventData.TransactionId);

    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => bridge.RolledBack(eventData.Context, eventData.TransactionId);

    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        bridge.RolledBack(eventData.Context, eventData.TransactionId);
        return Task.CompletedTask;
    }
}
