// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections.Concurrent;

/// <summary>Keeps native transaction locks and all cursors on one dedicated thread.</summary>
/// <remarks>Dispose asynchronously to drain accepted commands and stop the worker. This executor does not provide transaction isolation between sessions.</remarks>
/// <example><code>await using var executor = new LiteDbDatabaseExecutor(); var threadId = await executor.RunAsync(() => Environment.CurrentManagedThreadId);</code></example>
public sealed class LiteDbDatabaseExecutor : IAsyncDisposable
{
    private readonly BlockingCollection<Action> commands = new();
    private readonly SemaphoreSlim slots = new(256, 256);
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int disposed;

    /// <summary>Starts one native worker with capacity for 256 outstanding commands.</summary>
    public LiteDbDatabaseExecutor()
    {
        new Thread(this.Consume) { IsBackground = true, Name = "LiteDB provider" }.Start();
    }

    /// <summary>Queues synchronous work on the worker thread and waits for its completion.</summary>
    /// <typeparam name="T">The command result type.</typeparam>
    /// <param name="action">Synchronous work. Do not block on asynchronous operations inside this delegate.</param>
    /// <param name="cancellationToken">Cancels admission or a command that has not started. An executing native call finishes before returning.</param>
    /// <returns>The completed command result.</returns>
    /// <exception cref="OperationCanceledException">The command was canceled before execution.</exception>
    /// <exception cref="ObjectDisposedException">The executor has stopped accepting commands.</exception>
    public async Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        await this.slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionContext = ExecutionContext.Capture();
        try
        {
            this.commands.Add(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (executionContext is null) { completion.SetResult(action()); }
                    else { ExecutionContext.Run(executionContext, _ => completion.SetResult(action()), null); }
                }
                catch (OperationCanceledException exception)
                {
                    completion.SetCanceled(exception.CancellationToken);
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
                finally
                {
                    this.slots.Release();
                }
            });
        }
        catch
        {
            this.slots.Release();
            throw;
        }

        // Once started, a native call must finish before its outcome is returned.
        return await completion.Task.ConfigureAwait(false);
    }

    /// <summary>Stops accepting commands and waits until accepted work has drained and the worker has exited.</summary>
    /// <returns>A task representing worker shutdown.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            this.commands.CompleteAdding();
        }

        await this.stopped.Task.ConfigureAwait(false);
    }

    private void Consume()
    {
        try
        {
            foreach (var command in this.commands.GetConsumingEnumerable())
            {
                command();
            }
        }
        finally
        {
            this.stopped.TrySetResult();
        }
    }
}
