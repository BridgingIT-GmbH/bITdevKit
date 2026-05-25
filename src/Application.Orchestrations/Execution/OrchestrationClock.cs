// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Provides the orchestration runtime clock used for timestamps, leases, and deterministic waiting.
/// </summary>
public interface IOrchestrationClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Delays until the specified duration has elapsed according to the clock.
    /// </summary>
    /// <param name="delay">The delay duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the delay has elapsed.</returns>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the production orchestration clock backed by system UTC time.
/// </summary>
public class SystemOrchestrationClock : IOrchestrationClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
    }
}

/// <summary>
/// Provides a controllable orchestration clock for deterministic test execution.
/// </summary>
public class FakeOrchestrationClock : IOrchestrationClock
{
    private readonly object sync = new();
    private readonly List<DelayRegistration> delays = [];
    private DateTimeOffset utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeOrchestrationClock"/> class.
    /// </summary>
    /// <param name="utcNow">The optional initial UTC time.</param>
    public FakeOrchestrationClock(DateTimeOffset? utcNow = null)
    {
        this.utcNow = utcNow ?? new DateTimeOffset(2026, 05, 07, 12, 00, 00, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow
    {
        get
        {
            lock (this.sync)
            {
                return this.utcNow;
            }
        }
    }

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (delay <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        lock (this.sync)
        {
            var dueUtc = this.utcNow.Add(delay);
            if (dueUtc <= this.utcNow)
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = new DelayRegistration(dueUtc, completion);
            if (cancellationToken.CanBeCanceled)
            {
                registration.Cancellation = cancellationToken.Register(static state =>
                {
                    var item = (DelayRegistration)state!;
                    item.Completion.TrySetCanceled();
                }, registration);
            }

            this.delays.Add(registration);
            return completion.Task;
        }
    }

    /// <summary>
    /// Advances the fake clock by the specified duration.
    /// </summary>
    /// <param name="duration">The duration to advance.</param>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The fake clock cannot move backwards.");
        }

        this.AdvanceTo(this.UtcNow.Add(duration));
    }

    /// <summary>
    /// Advances the fake clock to the specified UTC timestamp.
    /// </summary>
    /// <param name="utcNow">The target UTC time.</param>
    public void AdvanceTo(DateTimeOffset utcNow)
    {
        List<DelayRegistration> due;

        lock (this.sync)
        {
            if (utcNow < this.utcNow)
            {
                throw new ArgumentOutOfRangeException(nameof(utcNow), "The fake clock cannot move backwards.");
            }

            this.utcNow = utcNow;
            due = this.delays
                .Where(item => item.DueUtc <= this.utcNow)
                .ToList();

            foreach (var item in due)
            {
                this.delays.Remove(item);
            }
        }

        foreach (var item in due)
        {
            item.Cancellation.Dispose();
            item.Completion.TrySetResult();
        }
    }

    private class DelayRegistration(DateTimeOffset dueUtc, TaskCompletionSource completion)
    {
        public DateTimeOffset DueUtc { get; } = dueUtc;

        public TaskCompletionSource Completion { get; } = completion;

        public CancellationTokenRegistration Cancellation { get; set; }
    }
}