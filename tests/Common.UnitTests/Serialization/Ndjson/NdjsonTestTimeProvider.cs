// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Testing.Ndjson;

internal sealed class NdjsonTestTimeProvider : TimeProvider
{
    private readonly object sync = new();
    private readonly List<ManualTimer> timers = [];
    private readonly List<(int Count, TaskCompletionSource Completion)> waiters = [];
    private long ticks;
    private int created;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (this.sync) { return this.ticks; } }

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(this.GetTimestamp());

    internal Task WaitForTimersAsync(int count)
    {
        lock (this.sync)
        {
            if (this.created >= count) { return Task.CompletedTask; }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.waiters.Add((count, completion));
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
    {
        lock (this.sync)
        {
            var timer = new ManualTimer(this, callback, state);
            this.timers.Add(timer);
            timer.Change(dueTime, period);
            this.created++;
            foreach (var waiter in this.waiters.Where(w => w.Count <= this.created).ToArray())
            {
                waiter.Completion.TrySetResult();
                this.waiters.Remove(waiter);
            }

            return timer;
        }
    }

    internal void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration.Ticks);
        List<(TimerCallback Callback, object State)> callbacks = [];
        lock (this.sync)
        {
            this.ticks += duration.Ticks;
            foreach (var timer in this.timers.ToArray())
            {
                if (!timer.Disposed && timer.Due <= this.ticks)
                {
                    callbacks.Add((timer.Callback, timer.State));
                    timer.Due = timer.Period > TimeSpan.Zero ? this.ticks + timer.Period.Ticks : long.MaxValue;
                }
            }
        }

        foreach (var (callback, state) in callbacks) { callback(state); }
    }

    private sealed class ManualTimer(NdjsonTestTimeProvider owner, TimerCallback callback, object state) : ITimer
    {
        internal TimerCallback Callback => callback;
        internal object State => state;
        internal long Due { get; set; }

        internal TimeSpan Period { get; private set; }

        internal bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.sync)
            {
                if (this.Disposed) { return false; }

                this.Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.ticks + dueTime.Ticks;
                this.Period = period;
                return true;
            }
        }

        public void Dispose()
        {
            lock (owner.sync) { this.Disposed = true; owner.timers.Remove(this); }
        }

        public ValueTask DisposeAsync() { this.Dispose(); return ValueTask.CompletedTask; }
    }
}
