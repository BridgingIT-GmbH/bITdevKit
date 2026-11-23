// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Owns the transaction state shared by LiteDB providers in one dependency injection scope.</summary>
/// <param name="owner">The named database owner. The session never disposes this shared dependency.</param>
/// <remarks>Dispose asynchronously to roll back unfinished work. Independent scopes use different sessions. Supplied callbacks may reenter their owning session.</remarks>
/// <example><code>var session = services.GetRequiredKeyedService&lt;LiteDbDatabaseSession&gt;("default");</code></example>
/// <param name="observers">Lazy mutation observers in the same dependency injection scope.</param>
public sealed class LiteDbDatabaseSession(LiteDbDatabaseOwner owner, Func<IEnumerable<ILiteDbEntityMutationObserver>> observers = null) : IAsyncDisposable
{
    private readonly LiteDbDatabaseOwner databaseOwner = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly AsyncLocal<object> operationToken = new();
    private readonly Dictionary<IConcurrency, Guid> versions = new(ReferenceEqualityComparer.Instance);
    private readonly List<Action> committed = [];
    private object activeToken;
    private bool reserved;
    private bool nativeTransaction;
    private bool explicitTransaction;
    private bool failed;
    private bool disposed;

    /// <summary>Gets the database's mapper. Configure it before opening the database and do not mutate it during operations.</summary>
    public BsonMapper Mapper => this.databaseOwner.Mapper;

    /// <summary>Gets whether this session owns a live explicit transaction.</summary>
    public bool HasTransaction => this.explicitTransaction;

    /// <summary>Gets or sets provenance for an explicitly orchestrated restore in this session.</summary>
    public LiteDbMutationCaptureContext CaptureContext { get; set; }

    /// <summary>Gets or sets whether an explicit bulk outbox decorator has enabled domain-event capture.</summary>
    public bool CaptureBulkDomainEvents { get; set; }

    /// <summary>Captures feature data inside the current native write transaction.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="database">The owned native database.</param>
    /// <param name="entity">The caller entity.</param>
    /// <param name="before">Stored state before the mutation.</param>
    /// <param name="after">Stored state after the mutation.</param>
    /// <param name="collection">The collection name.</param>
    /// <param name="operation">The mutation action.</param>
    /// <param name="source">The persistence source.</param>
    /// <param name="affectedEntityCount">Optional selected set or batch size.</param>
    /// <param name="operationId">The shared multi-row operation identity.</param>
    public void CaptureMutation<TEntity>(LiteDatabase database, TEntity entity, BsonDocument before, BsonDocument after,
        string collection, string operation, string source, Guid? operationId = null, int? affectedEntityCount = null) where TEntity : class, IEntity
    {
        if (!this.nativeTransaction) { throw new InvalidOperationException("Feature capture requires a write transaction."); }

        var mutation = new LiteDbEntityMutation(typeof(TEntity), entity, before, after, collection, operation, this.CaptureContext?.Source ?? source, this.CaptureContext?.ChangeSetId ?? operationId ?? Guid.NewGuid(), affectedEntityCount, this.CaptureContext?.Reason, this.CaptureContext?.RestoredChangeSetId);
        foreach (var observer in observers?.Invoke() ?? []) { observer.Capture(this, database, mutation); }
    }

    /// <summary>Registers a memory-only feature acknowledgement to run after successful native commit.</summary>
    /// <param name="acknowledge">An acknowledgement that must not perform external I/O or fail.</param>
    public void OnCommitted(Action acknowledge)
    {
        ArgumentNullException.ThrowIfNull(acknowledge);
        if (!this.nativeTransaction) { throw new InvalidOperationException("Commit acknowledgement requires a write transaction."); }

        this.committed.Add(acknowledge);
    }

    /// <summary>Runs synchronous native work on the database thread while this session owns its reservation.</summary>
    /// <typeparam name="T">The command result type.</typeparam>
    /// <param name="action">Native database work. Materialize and close any cursors before returning.</param>
    /// <param name="cancellationToken">Cancels waiting or work that has not started, without interrupting an executing native call.</param>
    /// <returns>The completed native command result.</returns>
    /// <exception cref="LiteDbProviderException">This session does not own a database reservation.</exception>
    public Task<T> NativeAsync<T>(Func<LiteDatabase, T> action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.reserved)
        {
            throw new LiteDbProviderException(new InvalidOperationError("Native commands require an active session operation or transaction."));
        }

        return this.databaseOwner.RunAsync(action, cancellationToken);
    }

    /// <summary>Records a caller entity's current version so rollback can restore it.</summary>
    /// <param name="entity">The concurrency-aware entity about to change inside this session's write transaction.</param>
    public void TrackVersion(IConcurrency entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!this.nativeTransaction)
        {
            throw new LiteDbProviderException(new InvalidOperationError("Version tracking requires a native write transaction."));
        }

        this.versions.TryAdd(entity, entity.ConcurrencyVersion);
    }

    /// <summary>Executes a result-producing provider operation under session ownership and commits a standalone write on success.</summary>
    /// <typeparam name="T">The provider result type.</typeparam>
    /// <param name="action">The operation, including awaited callbacks. This delegate runs outside the native worker.</param>
    /// <param name="write">Whether to start a native write transaction when no explicit transaction exists.</param>
    /// <param name="cancellationToken">Cancels admission or pending commands. Rollback ignores this token.</param>
    /// <returns>The operation result after required commit or rollback completes.</returns>
    /// <exception cref="LiteDbProviderException">The session is busy, failed, or cannot complete its native transaction.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled. Its native writes are rolled back.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, bool write, CancellationToken cancellationToken = default)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.operationToken.Value is not null && ReferenceEquals(this.operationToken.Value, this.activeToken))
        {
            if (this.failed)
            {
                throw new LiteDbProviderException(new InvalidOperationError("The transaction requires rollback."));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nested = await action().ConfigureAwait(false);
                if (nested.IsFailure) { this.failed = true; }

                return nested;
            }
            catch { this.failed = true; throw; }
        }

        if (!this.operation.Wait(0))
        {
            throw new LiteDbProviderException(new InvalidOperationError("The LiteDB session already has an operation in progress."));
        }

        var previous = this.operationToken.Value;
        this.activeToken = new object();
        this.operationToken.Value = this.activeToken;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.failed)
            {
                throw new LiteDbProviderException(new InvalidOperationError("The transaction requires rollback."));
            }

            if (!this.reserved)
            {
                await this.databaseOwner.AcquireAsync(cancellationToken).ConfigureAwait(false);
                this.reserved = true;
            }

            if (write && !this.nativeTransaction)
            {
                if (!await this.NativeAsync(db => db.BeginTrans(), cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The native worker already owns a transaction.");
                }

                this.nativeTransaction = true;
            }

            var result = await action().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailure || this.failed)
            {
                this.failed = true;
                await this.AbortNativeAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    throw new LiteDbProviderException(new InvalidOperationError("A nested operation failed; the transaction was rolled back."));
                }
            }
            else if (!this.explicitTransaction && this.nativeTransaction)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await this.CommitNativeAsync().ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception exception)
        {
            this.failed = true;
            try { await this.AbortNativeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { exception.Data["LiteDbRollbackFailure"] = cleanup; }

            throw;
        }
        finally
        {
            if (!this.explicitTransaction)
            {
                this.failed = false;
                this.Release();
            }

            this.operationToken.Value = previous;
            this.activeToken = null;
            this.operation.Release();
        }
    }

    /// <summary>Starts an explicit transaction or joins this session's existing transaction.</summary>
    /// <param name="cancellationToken">Cancels waiting for database ownership or a pending begin command.</param>
    /// <returns>A non-null adapter for executing transaction delegates without automatic commit.</returns>
    /// <exception cref="LiteDbProviderException">The session is busy or requires rollback.</exception>
    public async Task<IDatabaseTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.operation.Wait(0))
        {
            throw new LiteDbProviderException(new InvalidOperationError("Cannot begin during a session operation."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.failed)
            {
                throw new LiteDbProviderException(new InvalidOperationError("The transaction requires rollback."));
            }

            if (!this.explicitTransaction)
            {
                await this.databaseOwner.AcquireAsync(cancellationToken).ConfigureAwait(false);
                this.reserved = true;
                try
                {
                    if (!await this.NativeAsync(db => db.BeginTrans(), cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("The native worker already owns a transaction.");
                    }

                    this.nativeTransaction = true;
                    this.explicitTransaction = true;
                }
                catch { this.Release(); throw; }
            }

            return new LiteDbDatabaseTransaction(this);
        }
        finally { this.operation.Release(); }
    }

    /// <summary>Commits or rolls back this session's explicit transaction.</summary>
    /// <param name="commit">True to commit, or false to roll back and clear failed state.</param>
    /// <param name="cancellationToken">Checked before commit. Rollback always performs cleanup even when this token is canceled.</param>
    /// <returns>A task that completes after native completion and ownership release.</returns>
    /// <exception cref="LiteDbProviderException">No transaction exists, the session is busy, or a failed transaction cannot commit.</exception>
    public async Task CompleteAsync(bool commit, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.operation.Wait(0))
        {
            throw new LiteDbProviderException(new InvalidOperationError("Cannot complete during a session operation."));
        }

        try
        {
            if (!this.explicitTransaction)
            {
                throw new LiteDbProviderException(new InvalidOperationError("There is no active transaction."));
            }

            if (commit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (this.failed)
                {
                    throw new LiteDbProviderException(new InvalidOperationError("The failed transaction cannot commit; roll it back."));
                }

                await this.CommitNativeAsync().ConfigureAwait(false);
            }
            else
            {
                await this.AbortNativeAsync().ConfigureAwait(false);
            }

            this.explicitTransaction = false;
            this.failed = false;
        }
        catch (Exception exception)
        {
            this.failed = true;
            try { await this.AbortNativeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { exception.Data["LiteDbRollbackFailure"] = cleanup; }

            throw;
        }
        finally { this.operation.Release(); }
    }

    /// <summary>Waits for the current operation and rolls back any unfinished session transaction.</summary>
    /// <returns>A task representing session cleanup. The shared database remains open.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.operation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!this.disposed)
            {
                this.disposed = true;
                await this.AbortNativeAsync().ConfigureAwait(false);
                this.explicitTransaction = false;
            }
        }
        finally { this.operation.Release(); }
    }

    /// <summary>Checks that this session has an active transaction that has not failed.</summary>
    /// <exception cref="LiteDbProviderException">The transaction has completed or requires rollback.</exception>
    public void EnsureTransaction()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.explicitTransaction || this.failed)
        {
            throw new LiteDbProviderException(new InvalidOperationError("The transaction is no longer active."));
        }
    }

    /// <summary>Rolls back a failed scoped delegate while preserving logical state for the caller's explicit rollback.</summary>
    /// <param name="exception">The original exception. A cleanup failure is attached to its data instead of replacing it.</param>
    /// <returns>A task representing rollback and ownership release.</returns>
    public async Task AbortScopedAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        await this.operation.WaitAsync().ConfigureAwait(false);
        try
        {
            this.failed = true;
            try { await this.AbortNativeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { exception.Data["LiteDbRollbackFailure"] = cleanup; }
        }
        finally { this.operation.Release(); }
    }

    private async Task CommitNativeAsync()
    {
        if (!this.nativeTransaction || !await this.NativeAsync(db => db.Commit()).ConfigureAwait(false))
        {
            throw new LiteDbProviderException(new InvalidOperationError("The native transaction did not commit."));
        }

        this.nativeTransaction = false;
        this.versions.Clear();
        var acknowledgements = this.committed.ToArray();
        this.committed.Clear();
        this.Release();
        foreach (var acknowledge in acknowledgements) { acknowledge(); }
    }

    private async Task AbortNativeAsync()
    {
        try
        {
            if (this.nativeTransaction)
            {
                await this.databaseOwner.RollbackAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            this.nativeTransaction = false;
            foreach (var entry in this.versions)
            {
                entry.Key.ConcurrencyVersion = entry.Value;
            }

            this.versions.Clear();
            this.committed.Clear();
            this.Release();
        }
    }

    private void Release()
    {
        if (this.reserved)
        {
            this.reserved = false;
            this.databaseOwner.Release();
        }
    }
}
