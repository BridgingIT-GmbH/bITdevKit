// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#pragma warning disable CS0618 // obsolete command request types are covered by these behaviors
namespace BridgingIT.DevKit.Application.UnitTests.Commands;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

public class CommandResilienceBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_WhenHandlerFailsTwice_RetriesAndReturnsResult()
    {
        // Arrange
        var sut = new RetryCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Retry = new RetryCommandOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        var response = await sut.Handle(command, async _ =>
        {
            await Task.Yield();
            return ++calls < 3 ? throw new InvalidOperationException("boom") : CommandResult.For(42);
        }, CancellationToken.None);

        // Assert
        calls.ShouldBe(3);
        response.Result.Value.ShouldBe(42);
    }

    [Fact]
    public async Task RetryBehavior_WhenHandlerAlwaysFails_RethrowsAfterAllAttempts()
    {
        // Arrange
        var sut = new RetryCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Retry = new RetryCommandOptions { Attempts = 2, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        var act = () => sut.Handle(command, async _ =>
        {
            await Task.Yield();
            calls++;
            throw new InvalidOperationException("boom");
        }, CancellationToken.None);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldBe("boom");
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task RetryBehavior_WhenBackoffIsExponential_DoublesDelayBetweenAttempts()
    {
        // Arrange
        var sut = new RetryCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand
        {
            Retry = new RetryCommandOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(40), BackoffExponential = true }
        };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Handle(command, _ => throw new InvalidOperationException("boom"), CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(act);

        // Assert (40 + 80 + 160 ms)
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(270);
    }

    [Fact]
    public async Task RetryBehavior_WhenRequestIsCancelled_DoesNotRetry()
    {
        // Arrange
        var sut = new RetryCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Retry = new RetryCommandOptions { Attempts = 5, Backoff = TimeSpan.FromMilliseconds(10) } };
        using var cts = new CancellationTokenSource();
        var calls = 0;

        // Act
        var act = () => sut.Handle(command, async _ =>
        {
            await Task.Yield();
            calls++;
            await cts.CancelAsync();
            throw new InvalidOperationException("boom");
        }, cts.Token);

        // Assert
        await Should.ThrowAsync<Exception>(act);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task TimeoutBehavior_WhenHandlerIgnoresCancellation_ThrowsTimeoutRejectedWithoutWaiting()
    {
        // Arrange
        var sut = new TimeoutCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Timeout = new TimeoutCommandOptions { Timeout = TimeSpan.FromMilliseconds(100) } };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Handle(command, async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return CommandResult.For(1);
        }, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<TimeoutRejectedException>(act);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task TimeoutBehavior_WhenHandlerCompletesInTime_ReturnsResult()
    {
        // Arrange
        var sut = new TimeoutCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Timeout = new TimeoutCommandOptions { Timeout = TimeSpan.FromSeconds(5) } };

        // Act
        var response = await sut.Handle(command, _ => Task.FromResult(CommandResult.For(7)), CancellationToken.None);

        // Assert
        response.Result.Value.ShouldBe(7);
    }

    [Fact]
    public async Task CircuitBreakerBehavior_WhenFailuresStayBelowThreshold_RetriesUntilSuccess()
    {
        // Arrange
        var sut = new CircuitBreakerCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand
        {
            CircuitBreaker = new CircuitBreakerCommandOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) }
        };
        var calls = 0;

        // Act
        var response = await sut.Handle(command, async _ =>
        {
            await Task.Yield();
            return ++calls < 3 ? throw new InvalidOperationException("boom") : CommandResult.For(5);
        }, CancellationToken.None);

        // Assert
        calls.ShouldBe(3);
        response.Result.Value.ShouldBe(5);
    }

    [Fact]
    public async Task CircuitBreakerBehavior_WhenConsecutiveFailuresReachThreshold_OpensCircuitUntilBreakDurationElapsed()
    {
        // Arrange
        var sut = new CircuitBreakerCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand
        {
            CircuitBreaker = new CircuitBreakerCommandOptions
            {
                Attempts = 3,
                Backoff = TimeSpan.FromMilliseconds(20),
                BreakDuration = TimeSpan.FromMilliseconds(600)
            }
        };
        var calls = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await sut.Handle(command, async _ =>
        {
            await Task.Yield();
            return ++calls <= 3 ? throw new InvalidOperationException("boom") : CommandResult.For(9);
        }, CancellationToken.None);

        // Assert (the handler is not invoked while the circuit is open)
        calls.ShouldBe(4);
        response.Result.Value.ShouldBe(9);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task CircuitBreakerBehavior_WhenCancelled_StopsRetrying()
    {
        // Arrange
        var sut = new CircuitBreakerCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand
        {
            CircuitBreaker = new CircuitBreakerCommandOptions
            {
                Attempts = 2,
                Backoff = TimeSpan.FromMilliseconds(20),
                BreakDuration = TimeSpan.FromSeconds(30)
            }
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        // Act
        var act = () => sut.Handle(command, _ => throw new InvalidOperationException("boom"), cts.Token);

        // Assert
        await Should.ThrowAsync<Exception>(act);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsOne_ThrowsConfiguredFaultWithoutCallingHandler()
    {
        // Arrange
        var sut = new ChaosExceptionCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var fault = new ArgumentException("chaos");
        var command = new TestCommand { Chaos = new ChaosExceptionCommandOptions { InjectionRate = 1, Fault = fault } };
        var calls = 0;

        // Act
        var act = () => sut.Handle(command, _ =>
        {
            calls++;
            return Task.FromResult(CommandResult.For(1));
        }, CancellationToken.None);

        // Assert
        (await Should.ThrowAsync<ArgumentException>(act)).ShouldBeSameAs(fault);
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsZero_CallsHandler()
    {
        // Arrange
        var sut = new ChaosExceptionCommandBehavior<TestCommand, CommandResponse<Result<int>>>(NullLoggerFactory.Instance);
        var command = new TestCommand { Chaos = new ChaosExceptionCommandOptions { InjectionRate = 0 } };

        // Act
        var response = await sut.Handle(command, _ => Task.FromResult(CommandResult.For(3)), CancellationToken.None);

        // Assert
        response.Result.Value.ShouldBe(3);
    }

    private sealed class TestCommand()
        : CommandRequestBase<Result<int>>(Guid.NewGuid()), IRetryCommand, ITimeoutCommand, ICircuitBreakerCommand, IChaosExceptionCommand
    {
        public RetryCommandOptions Retry { get; init; } = new();

        public TimeoutCommandOptions Timeout { get; init; } = new();

        public CircuitBreakerCommandOptions CircuitBreaker { get; init; } = new();

        public ChaosExceptionCommandOptions Chaos { get; init; } = new();

        RetryCommandOptions IRetryCommand.Options => this.Retry;

        TimeoutCommandOptions ITimeoutCommand.Options => this.Timeout;

        CircuitBreakerCommandOptions ICircuitBreakerCommand.Options => this.CircuitBreaker;

        ChaosExceptionCommandOptions IChaosExceptionCommand.Options => this.Chaos;
    }
}
