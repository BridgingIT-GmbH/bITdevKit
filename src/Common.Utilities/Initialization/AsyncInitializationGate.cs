// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Coalesces concurrent asynchronous initialization and retries after failure or cancellation.
/// </summary>
/// <example>
/// <code>
/// var gate = new AsyncInitializationGate();
/// await gate.EnsureInitializedAsync(ct => InitializeAsync(ct), cancellationToken);
/// </code>
/// </example>
public sealed class AsyncInitializationGate
{
    private readonly object syncRoot = new();
    private Task initializationTask;
    private bool initialized;

    /// <summary>Ensures the supplied asynchronous initializer has completed successfully.</summary>
    /// <param name="initializer">The initializer to execute.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>A task that completes after initialization.</returns>
    /// <example><code>await gate.EnsureInitializedAsync(ct => client.CreateIfNotExistsAsync(ct), cancellationToken);</code></example>
    public async Task EnsureInitializedAsync(
        Func<CancellationToken, Task> initializer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        cancellationToken.ThrowIfCancellationRequested();

        Task task;
        lock (this.syncRoot)
        {
            if (this.initialized)
            {
                return;
            }

            task = this.initializationTask ??= this.InitializeAsync(initializer, cancellationToken);
        }

        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializeAsync(Func<CancellationToken, Task> initializer, CancellationToken cancellationToken)
    {
        // Ensure initializationTask is assigned before a synchronously failing initializer reaches the cleanup path.
        await Task.Yield();
        try
        {
            await initializer(cancellationToken).ConfigureAwait(false);
            lock (this.syncRoot)
            {
                this.initialized = true;
            }
        }
        finally
        {
            lock (this.syncRoot)
            {
                if (!this.initialized)
                {
                    this.initializationTask = null;
                }
            }
        }
    }
}
