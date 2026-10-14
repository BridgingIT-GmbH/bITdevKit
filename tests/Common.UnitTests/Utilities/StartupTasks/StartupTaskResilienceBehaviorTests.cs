// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

public class StartupTaskResilienceBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_WhenTaskFailsTwice_RetriesUntilSuccess()
    {
        // Arrange
        var sut = new RetryStartupTaskBehavior(NullLoggerFactory.Instance);
        var task = new TestStartupTask { Retry = new RetryStartupTaskOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        await sut.Execute(task, CancellationToken.None, async () =>
        {
            await Task.Yield();
            if (++calls < 3)
            {
                throw new InvalidOperationException("boom");
            }
        });

        // Assert
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task RetryBehavior_WhenBackoffIsExponential_DoublesDelayBetweenAttempts()
    {
        // Arrange
        var sut = new RetryStartupTaskBehavior(NullLoggerFactory.Instance);
        var task = new TestStartupTask
        {
            Retry = new RetryStartupTaskOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(40), BackoffExponential = true }
        };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Execute(task, CancellationToken.None, () => throw new InvalidOperationException("boom"));
        await Should.ThrowAsync<InvalidOperationException>(act);

        // Assert (40 + 80 + 160 ms)
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(270);
    }

    [Fact]
    public async Task TimeoutBehavior_WhenTaskIgnoresCancellation_ThrowsTimeoutRejectedWithoutWaiting()
    {
        // Arrange
        var sut = new TimeoutStartupTaskBehavior(NullLoggerFactory.Instance);
        var task = new TestStartupTask { Timeout = new TimeoutStartupTaskOptions { Timeout = TimeSpan.FromMilliseconds(100) } };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Execute(task, CancellationToken.None, () => Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        await Should.ThrowAsync<TimeoutRejectedException>(act);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CircuitBreakerBehavior_WhenConsecutiveFailuresReachThreshold_OpensCircuitUntilBreakDurationElapsed()
    {
        // Arrange
        var sut = new CircuitBreakerStartupTaskBehavior(NullLoggerFactory.Instance);
        var task = new TestStartupTask
        {
            CircuitBreaker = new CircuitBreakerTaskOptions
            {
                Attempts = 3,
                Backoff = TimeSpan.FromMilliseconds(20),
                BreakDuration = TimeSpan.FromMilliseconds(600)
            }
        };
        var calls = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act
        await sut.Execute(task, CancellationToken.None, async () =>
        {
            await Task.Yield();
            if (++calls <= 3)
            {
                throw new InvalidOperationException("boom");
            }
        });

        // Assert
        calls.ShouldBe(4);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsOne_ThrowsConfiguredFaultWithoutCallingNext()
    {
        // Arrange
        var sut = new ChaosExceptionStartupTaskBehavior(NullLoggerFactory.Instance);
        var fault = new ArgumentException("chaos");
        var task = new TestStartupTask { Chaos = new ChaosExceptionStartupTaskOptions { InjectionRate = 1, Fault = fault } };
        var calls = 0;

        // Act
        var act = () => sut.Execute(task, CancellationToken.None, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        (await Should.ThrowAsync<ArgumentException>(act)).ShouldBeSameAs(fault);
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsZero_CallsNext()
    {
        // Arrange
        var sut = new ChaosExceptionStartupTaskBehavior(NullLoggerFactory.Instance);
        var task = new TestStartupTask { Chaos = new ChaosExceptionStartupTaskOptions { InjectionRate = 0 } };
        var calls = 0;

        // Act
        await sut.Execute(task, CancellationToken.None, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        calls.ShouldBe(1);
    }

    private sealed class TestStartupTask : IStartupTask, IRetryStartupTask, ITimeoutStartupTask, ICircuitBreakerStartupTask, IChaosExceptionStartupTask
    {
        public RetryStartupTaskOptions Retry { get; init; } = new();

        public TimeoutStartupTaskOptions Timeout { get; init; } = new();

        public CircuitBreakerTaskOptions CircuitBreaker { get; init; } = new();

        public ChaosExceptionStartupTaskOptions Chaos { get; init; } = new();

        RetryStartupTaskOptions IRetryStartupTask.Options => this.Retry;

        TimeoutStartupTaskOptions ITimeoutStartupTask.Options => this.Timeout;

        CircuitBreakerTaskOptions ICircuitBreakerStartupTask.Options => this.CircuitBreaker;

        ChaosExceptionStartupTaskOptions IChaosExceptionStartupTask.Options => this.Chaos;

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
