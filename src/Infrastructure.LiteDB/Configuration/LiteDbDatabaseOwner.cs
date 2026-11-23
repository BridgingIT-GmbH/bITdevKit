// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

/// <summary>Owns one named LiteDB context, native worker, mapper, and transaction reservation.</summary>
/// <remarks>Normally registered by AddLiteDbDatabase. Dispose asynchronously after provider scopes complete to release the database file.</remarks>
/// <example><code>var owner = services.GetRequiredKeyedService&lt;LiteDbDatabaseOwner&gt;("default");</code></example>
public sealed class LiteDbDatabaseOwner : IAsyncDisposable
{
    private readonly LiteDbDatabaseRegistry.Registration registration;
    private readonly LiteDbDatabaseExecutor executor;
    private readonly SemaphoreSlim ownership = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private readonly TaskCompletionSource shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private LiteDbContext context;
    private int disposed;

    /// <summary>Creates a database owner with its configured mapper and a dedicated native worker.</summary>
    /// <param name="registration">The registration snapshot. Mapping configuration is evaluated before a worker starts.</param>
    public LiteDbDatabaseOwner(LiteDbDatabaseRegistry.Registration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        this.registration = registration;
        this.Mapper = registration.CreateMapper();
        this.executor = new LiteDbDatabaseExecutor();
    }

    /// <summary>Gets the registration's mapper. Its configuration must remain unchanged after database operations begin.</summary>
    public BsonMapper Mapper { get; }

    /// <summary>Reserves the native executor so another session cannot join the current transaction.</summary>
    /// <param name="cancellationToken">Cancels waiting. Shutdown also cancels pending ownership waits.</param>
    /// <returns>A task that completes when the caller owns the reservation.</returns>
    /// <remarks>Every successful acquisition must be paired with Release, after commit or rollback.</remarks>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.stopping.Token);
        await this.ownership.WaitAsync(linked.Token).ConfigureAwait(false);
        if (Volatile.Read(ref this.disposed) != 0)
        {
            this.ownership.Release();
            throw new ObjectDisposedException(nameof(LiteDbDatabaseOwner));
        }
    }

    /// <summary>Releases a previously acquired reservation after all native work has completed.</summary>
    public void Release() => this.ownership.Release();

    /// <summary>Rolls back native work on the owning thread, or waits for owner shutdown to finish its rollback.</summary>
    /// <returns>A task representing rollback completion.</returns>
    /// <remarks>The caller must own the current reservation. This cleanup method does not observe the operation's canceled token.</remarks>
    public async Task RollbackAsync()
    {
        if (Volatile.Read(ref this.disposed) != 0)
        {
            await this.shutdown.Task.ConfigureAwait(false);
            return;
        }

        try { await this.RunAsync(db => db.Rollback()).ConfigureAwait(false); }
        catch (ObjectDisposedException) when (Volatile.Read(ref this.disposed) != 0)
        {
            await this.shutdown.Task.ConfigureAwait(false);
        }
    }

    /// <summary>Runs synchronous native database work on the owner thread and opens the database on first use.</summary>
    /// <typeparam name="T">The command result type.</typeparam>
    /// <param name="action">Native work. Complete cursor enumeration and disposal before returning.</param>
    /// <param name="cancellationToken">Cancels admission or a command that has not started.</param>
    /// <returns>The completed native command result.</returns>
    /// <remarks>The caller must hold the reservation acquired through AcquireAsync. Prefer a scoped session for provider operations.</remarks>
    public Task<T> RunAsync<T>(Func<LiteDatabase, T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        return this.executor.RunAsync(() =>
        {
            if (this.context is null)
            {
                this.context = new LiteDbContext(this.registration.Connection.ToStringWithPassword(), this.Mapper);
                this.context.Database.UtcDate = true;
            }

            return action(this.context.Database);
        }, cancellationToken);
    }

    /// <summary>Stops admission, drains accepted native commands, rolls back remaining work, closes the context, and stops its worker.</summary>
    /// <returns>A task representing shutdown and file release.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            await this.shutdown.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            await this.stopping.CancelAsync().ConfigureAwait(false);
            await this.executor.RunAsync(() =>
            {
                try { this.context?.Database.Rollback(); }
                finally { this.context?.Dispose(); }

                return true;
            }).ConfigureAwait(false);
            await this.executor.DisposeAsync().ConfigureAwait(false);
            this.shutdown.TrySetResult();
        }
        catch (Exception exception)
        {
            await this.executor.DisposeAsync().ConfigureAwait(false);
            this.shutdown.TrySetException(exception);
            throw;
        }
    }
}
