// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Coalesces profiling worker ticks without a finalizer on partially constructed injected timers.</summary>
/// <remarks>Workers contain constructor and disposal faults. One consumer is supported; ticks do not accumulate a work queue.</remarks>
/// <example><code>using var timer = new ProfilingPeriodicTimer(interval, clock); while (await timer.WaitForNextTickAsync(token)) { await TickAsync(token); }</code></example>
public sealed class ProfilingPeriodicTimer : IDisposable
{
    private readonly TickState state = new();
    private readonly ITimer timer;

    /// <summary>Creates an injected-clock timer; a failed timer factory leaves no finalizable partially initialized object.</summary>
    /// <example><code>using var timer = new ProfilingPeriodicTimer(TimeSpan.FromSeconds(1), TimeProvider.System);</code></example>
    public ProfilingPeriodicTimer(TimeSpan interval, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (interval.TotalMilliseconds < 1 || interval.TotalMilliseconds > uint.MaxValue - 1) { throw new ArgumentOutOfRangeException(nameof(interval)); }

        if (ExecutionContext.IsFlowSuppressed())
        {
            this.timer = clock.CreateTimer(static value => ((TickState)value).Signal(), this.state, interval, interval);
        }
        else
        {
            using var suppressed = ExecutionContext.SuppressFlow();
            this.timer = clock.CreateTimer(static value => ((TickState)value).Signal(), this.state, interval, interval);
        }

        if (this.timer is null) { throw new InvalidOperationException("The profiling clock returned no timer."); }
    }

    /// <summary>Waits for one coalesced tick, cancellation of this wait, or disposal; no replacement work is queued.</summary>
    /// <example><code>if (await timer.WaitForNextTickAsync(token)) { await TickAsync(token); }</code></example>
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        lock (this.state.Sync)
        {
            if (cancellationToken.IsCancellationRequested) { return ValueTask.FromCanceled<bool>(cancellationToken); }

            if (this.state.Pending is not null) { throw new InvalidOperationException("Only one profiling tick consumer is supported."); }

            if (this.state.Disposed) { return ValueTask.FromResult(false); }

            if (this.state.Signaled)
            {
                this.state.Signaled = false;
                return ValueTask.FromResult(true);
            }

            this.state.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return this.ConsumeAsync(this.state.Pending, cancellationToken);
        }
    }

    private async ValueTask<bool> ConsumeAsync(TaskCompletionSource<bool> pending, CancellationToken token)
    {
        try
        {
            await pending.Task.WaitAsync(token).ConfigureAwait(false);
            lock (this.state.Sync) { return !this.state.Disposed; }
        }
        finally
        {
            lock (this.state.Sync)
            {
                this.state.Pending = null;
                this.state.Signaled = false;
            }
        }
    }

    /// <summary>Stops observation and completes a pending wait before disposing the underlying timer.</summary>
    /// <example><code>timer.Dispose();</code></example>
    public void Dispose()
    {
        lock (this.state.Sync)
        {
            if (this.state.Disposed) { return; }

            this.state.Disposed = true;
            this.state.Pending?.TrySetResult(false);
        }

        this.timer.Dispose();
    }

    private sealed class TickState
    {
        /// <summary>Serializes callback, consumer and disposal state.</summary>
        public object Sync { get; } = new();
        /// <summary>Prevents subsequent callback observations.</summary>
        public bool Disposed { get; set; }
        /// <summary>Retains at most one unconsumed tick.</summary>
        public bool Signaled { get; set; }
        /// <summary>Contains the one active waiter without retaining the timer owner.</summary>
        public TaskCompletionSource<bool> Pending { get; set; }

        /// <summary>Completes an active wait asynchronously or coalesces its next tick.</summary>
        public void Signal()
        {
            lock (this.Sync)
            {
                if (this.Disposed) { return; }

                if (this.Pending is null) { this.Signaled = true; }
                else { this.Pending.TrySetResult(true); }
            }
        }
    }
}
