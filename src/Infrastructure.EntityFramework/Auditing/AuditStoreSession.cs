// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>Holds the database-scoped publication gate through the whole owned audit transaction.</summary>
/// <example><code>await using var session = await AuditStoreSession.BeginAsync(context, cancellationToken);</code></example>
public sealed class AuditStoreSession : IAsyncDisposable
{
    private readonly IDbContextTransaction transaction;
    private AuditStoreSession(DbContext context, AuditSqlDialect dialect, IDbContextTransaction transaction, AuditStoreStateEntity state)
    {
        this.Context = context;
        this.Sql = dialect;
        this.transaction = transaction;
        this.State = state;
    }

    /// <summary>The fresh operation-owned audit context.</summary>
    /// <example><code>var context = session.Context;</code></example>
    public DbContext Context { get; }

    /// <summary>Trusted backend/name/parameter mapping for this context.</summary>
    /// <example><code>var sql = session.Sql;</code></example>
    public AuditSqlDialect Sql { get; }

    /// <summary>The tracked durable singleton, or null for an explicitly uninitialized store.</summary>
    /// <example><code>var published = session.State.PublishedPosition;</code></example>
    public AuditStoreStateEntity State { get; }

    /// <summary>Acquires the proven backend gate and reads durable control state without reseeding it.</summary>
    /// <example><code>await using var session = await AuditStoreSession.BeginAsync(context, ct);</code></example>
    public static async Task<AuditStoreSession> BeginAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var dialect = new AuditSqlDialect(context);
        // Microsoft.Data.Sqlite uses deferred=false for Serializable; this is BEGIN IMMEDIATE.
        var transaction = await context.Database.BeginTransactionAsync(dialect.Backend == "Sqlite" ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        try
        {
            // Stable identity coordinates aliases/default schemas that resolve to the same physical store.
            // ReadCommitted releases this preliminary read before waiting on the transaction-owned gate.
            var identity = await context.Set<AuditStoreStateEntity>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(item => item.Singleton == 1, cancellationToken).ConfigureAwait(false);
            if (identity is null) return new(context, dialect, transaction, null);
            var resource = "bdk.auditing.store:" + identity.StoreId.ToString("D");
            if (dialect.Backend == "SqlServer")
            {
                await using var command = dialect.Command("""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource = @p0,
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @result < 0 THROW 51000, 'AuditPublicationLockUnavailable', 1;
                    """, [resource]);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (dialect.Backend == "Postgres")
            {
                var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));
                await using var command = dialect.Command("SELECT pg_advisory_xact_lock(@p0)", [key]);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var state = await context.Set<AuditStoreStateEntity>().IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(item => item.Singleton == 1, cancellationToken).ConfigureAwait(false);
            if (state?.StoreId != identity.StoreId) throw new InvalidOperationException("AuditStoreIdentityChanged");
            return new(context, dialect, transaction, state);
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Commits the complete graph/control update or a bounded read snapshot.</summary>
    /// <example><code>await session.CommitAsync(cancellationToken);</code></example>
    public Task CommitAsync(CancellationToken cancellationToken = default) => this.transaction.CommitAsync(cancellationToken);

    /// <summary>Releases the gate by disposing the owned transaction, rolling back any uncommitted work.</summary>
    /// <example><code>await session.DisposeAsync();</code></example>
    public ValueTask DisposeAsync() => this.transaction.DisposeAsync();
}
