// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Tracks transfer inactivity and absolute lifetime using the configured clock.</summary>
/// <remarks>Expiry cancels the token and invokes the callback once. Callback faults cannot escape the timer. Progress renews the idle deadline without extending the absolute lifetime.</remarks>
/// <example><code>using var budget = new TransferDeadline(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), StopTransfer, time); budget.Start();</code></example>
public sealed class TransferDeadline : IDisposable
{
    private readonly Lock gate = new();
    private readonly TimeProvider time;
    private readonly TimeSpan idle;
    private readonly TimeSpan lifetime;
    private readonly long started;
    private readonly ITimer timer;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Action expired;
    private long progress;
    private bool disposed;
    private bool cancelled;
    private bool cancelling;

    /// <summary>Captures transfer limits and creates an initially unscheduled deadline timer.</summary>
    /// <param name="idleTimeout">The positive finite duration allowed without successful progress.</param>
    /// <param name="lifetime">The positive finite duration from construction until absolute expiry.</param>
    /// <param name="expired">An optional callback invoked once after expiry cancellation is signaled.</param>
    /// <param name="timeProvider">The monotonic observation clock and timer factory, or the system clock.</param>
    /// <example><code>using var deadline = new TransferDeadline(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));</code></example>
    public TransferDeadline(TimeSpan idleTimeout, TimeSpan lifetime, Action expired = null, TimeProvider timeProvider = null)
    {
        ValidateDuration(idleTimeout, nameof(idleTimeout));
        ValidateDuration(lifetime, nameof(lifetime));
        this.time = timeProvider ?? TimeProvider.System;
        this.idle = idleTimeout;
        this.lifetime = lifetime;
        this.expired = expired;
        this.started = this.progress = this.time.GetTimestamp();
        this.timer = this.time.CreateTimer(_ => this.Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Schedules the first deadline check using elapsed time since construction.</summary>
    /// <example><code>deadline.Start();</code></example>
    public void Start()
    {
        lock (this.gate)
        {
            this.Schedule();
        }
    }

    /// <summary>Gets the token canceled when the idle or absolute deadline expires.</summary>
    /// <example><code>await stream.ReadAsync(buffer, deadline.Token);</code></example>
    public CancellationToken Token => this.cancellation.Token;

    /// <summary>Records successful progress and reschedules the remaining idle deadline while the transfer is active.</summary>
    /// <example><code>deadline.Progress();</code></example>
    public void Progress()
    {
        lock (this.gate)
        {
            if (this.disposed || this.cancelled)
            {
                return;
            }

            this.progress = this.time.GetTimestamp();
            this.Schedule();
        }
    }

    /// <summary>Checks current deadlines and reports whether expiry has been signaled.</summary>
    /// <example><code>if (deadline.HasExpired) { return; }</code></example>
    public bool HasExpired
    {
        get
        {
            this.Check();
            lock (this.gate) { return this.cancelled; }
        }
    }

    /// <summary>Releases timer ownership when actual transfer work finishes.</summary>
    /// <example><code>deadline.Dispose();</code></example>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.timer.Dispose();
            if (!this.cancelling)
            {
                this.cancellation.Dispose();
            }
        }
    }

    private void Check()
    {
        var notify = false;
        lock (this.gate)
        {
            if (this.disposed || this.cancelled)
            {
                return;
            }

            if (this.time.GetElapsedTime(this.started) >= this.lifetime || this.time.GetElapsedTime(this.progress) >= this.idle)
            {
                this.cancelled = this.cancelling = true;
                notify = true;
            }
            else
            {
                this.Schedule();
            }
        }

        if (notify)
        {
            try
            {
                this.cancellation.Cancel();
            }
            catch (AggregateException)
            {
                // Callback faults cannot escape a timer or prevent the transfer's terminal notification.
            }
            finally
            {
                lock (this.gate)
                {
                    this.cancelling = false;
                    if (this.disposed)
                    {
                        this.cancellation.Dispose();
                    }
                }
            }

            try { this.expired?.Invoke(); }
            catch (Exception) { /* Expiry remains authoritative even when an observer fails. */ }
        }
    }

    private void Schedule()
    {
        var remaining = Min(this.idle - this.time.GetElapsedTime(this.progress), this.lifetime - this.time.GetElapsedTime(this.started));
        this.timer.Change(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private static void ValidateDuration(TimeSpan duration, string parameter)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameter, "A positive finite timer duration is required.");
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}