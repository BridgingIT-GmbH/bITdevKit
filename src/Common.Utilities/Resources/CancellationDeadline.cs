// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Owns one fixed cancellation deadline and contains faults raised by cancellation callbacks.</summary>
/// <remarks>Cancellation requests do not release native work or its admission. Dispose this owner only when the actual work ends.</remarks>
/// <example><code>using var deadline = new CancellationDeadline(timeout, time);
/// await operation(deadline.Token);</code></example>
public sealed class CancellationDeadline : IDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource source = new();
    private readonly ITimer timer;
    private readonly Action<Exception> notificationFault;
    private int cancelling;
    private bool disposed;

    /// <summary>Starts a single deadline using the supplied clock without renewing its budget.</summary>
    /// <param name="timeout">The nonnegative duration until cancellation.</param>
    /// <param name="time">The clock that owns the deadline timer.</param>
    /// <param name="notificationFault">An optional observer of callback failures; observer exceptions are contained.</param>
    /// <example><code>using var deadline = new CancellationDeadline(TimeSpan.FromSeconds(5), TimeProvider.System);</code></example>
    public CancellationDeadline(TimeSpan timeout, TimeProvider time, Action<Exception> notificationFault = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(time);
        this.notificationFault = notificationFault;
        this.timer = time.CreateTimer(static state => ((CancellationDeadline)state).Cancel(), this, timeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Gets the token cancelled when this fixed deadline expires or cancellation is requested explicitly.</summary>
    /// <example><code>await operation(deadline.Token);</code></example>
    public CancellationToken Token => this.source.Token;

    /// <summary>Gets whether cancellation was requested, including when a notification callback failed.</summary>
    /// <example><code>var timedOut = deadline.IsCancellationRequested;</code></example>
    public bool IsCancellationRequested => this.source.IsCancellationRequested;

    /// <summary>Requests cancellation while preventing callback exceptions from escaping a timer or interrupting cleanup.</summary>
    /// <remarks>Notification faults are observed through the supplied observer when present.</remarks>
    /// <example><code>deadline.Cancel();</code></example>
    public void Cancel()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.cancelling++;
        }

        try { this.source.Cancel(); }
        catch (AggregateException exception)
        {
            try { this.notificationFault?.Invoke(exception); }
            catch (Exception) { /* A failing observer must not escape the timer callback either. */ }
        }
        finally
        {
            lock (this.gate)
            {
                this.cancelling--;
                if (this.disposed && this.cancelling == 0) { this.source.Dispose(); }
            }
        }
    }

    /// <summary>Disposes the timer and defers token-source disposal until any active cancellation notification finishes.</summary>
    /// <example><code>deadline.Dispose();</code></example>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            this.timer.Dispose();
            if (this.cancelling == 0) { this.source.Dispose(); }
        }
    }
}