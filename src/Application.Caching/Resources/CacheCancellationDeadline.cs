// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Maps a fixed shared cancellation deadline to cache-owned notification-fault observation.</summary>
/// <remarks>A cancellation request does not release actual work or admission.</remarks>
/// <example><code>using var deadline = new CacheCancellationDeadline(timeout, time, tasks); await operation(deadline.Token);</code></example>
public sealed class CacheCancellationDeadline : IDisposable
{
    private readonly CancellationDeadline deadline;

    /// <summary>Starts one deadline without renewing its elapsed budget.</summary>
    /// <param name="timeout">The nonnegative duration until cancellation.</param>
    /// <param name="time">The clock that owns the deadline timer.</param>
    /// <param name="tasks">An optional existing observer for notification faults.</param>
    /// <example><code>using var deadline = new CacheCancellationDeadline(TimeSpan.FromSeconds(5), time, tasks);</code></example>
    public CacheCancellationDeadline(TimeSpan timeout, TimeProvider time, CacheRuntimeTasks tasks = null)
    {
        this.deadline = new(timeout, time, tasks is null ? null : exception =>
            tasks.Track(Task.FromException(exception), "cancellation-notification-failure"));
    }

    /// <summary>Gets cancellation signaled by expiry or an explicit request.</summary>
    public CancellationToken Token => this.deadline.Token;

    /// <summary>Gets whether cancellation has been requested.</summary>
    public bool IsCancellationRequested => this.deadline.IsCancellationRequested;

    /// <summary>Requests cancellation without allowing notification callback failures to escape.</summary>
    /// <example><code>deadline.Cancel();</code></example>
    public void Cancel() => this.deadline.Cancel();

    /// <summary>Disposes timer ownership after actual work completes.</summary>
    /// <example><code>deadline.Dispose();</code></example>
    public void Dispose() => this.deadline.Dispose();
}