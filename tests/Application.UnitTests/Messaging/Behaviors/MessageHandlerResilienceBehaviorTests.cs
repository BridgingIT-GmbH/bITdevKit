// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

public class MessageHandlerResilienceBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_WhenHandlerFailsTwice_RetriesUntilSuccess()
    {
        // Arrange
        var sut = new RetryMessageHandlerBehavior(NullLoggerFactory.Instance);
        var handler = new TestHandler { Retry = new RetryMessageHandlerOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        await sut.Handle(new TestMessage(), CancellationToken.None, handler, async () =>
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
    public async Task RetryBehavior_WhenHandlerAlwaysFails_RethrowsAfterAllAttempts()
    {
        // Arrange
        var sut = new RetryMessageHandlerBehavior(NullLoggerFactory.Instance);
        var handler = new TestHandler { Retry = new RetryMessageHandlerOptions { Attempts = 2, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        var act = () => sut.Handle(new TestMessage(), CancellationToken.None, handler, async () =>
        {
            await Task.Yield();
            calls++;
            throw new InvalidOperationException("boom");
        });

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task RetryBehavior_WhenHandlerHasNoRetryOptions_CallsNextOnce()
    {
        // Arrange
        var sut = new RetryMessageHandlerBehavior(NullLoggerFactory.Instance);
        var calls = 0;

        // Act
        await sut.Handle(new TestMessage(), CancellationToken.None, new object(), () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task TimeoutBehavior_WhenHandlerIgnoresCancellation_ThrowsTimeoutRejectedWithoutWaiting()
    {
        // Arrange
        var sut = new TimeoutMessageHandlerBehavior(NullLoggerFactory.Instance);
        var handler = new TestHandler { Timeout = new TimeoutMessageHandlerOptions { Timeout = TimeSpan.FromMilliseconds(100) } };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Handle(new TestMessage(), CancellationToken.None, handler, () => Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        await Should.ThrowAsync<TimeoutRejectedException>(act);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task TimeoutBehavior_WhenHandlerCompletesInTime_CallsNext()
    {
        // Arrange
        var sut = new TimeoutMessageHandlerBehavior(NullLoggerFactory.Instance);
        var handler = new TestHandler { Timeout = new TimeoutMessageHandlerOptions { Timeout = TimeSpan.FromSeconds(5) } };
        var calls = 0;

        // Act
        await sut.Handle(new TestMessage(), CancellationToken.None, handler, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsOne_ThrowsConfiguredFaultWithoutCallingNext()
    {
        // Arrange
        var sut = new ChaosExceptionMessageHandlerBehavior(NullLoggerFactory.Instance);
        var fault = new ArgumentException("chaos");
        var handler = new TestHandler { Chaos = new ChaosExceptionMessageHandlerOptions { InjectionRate = 1, Fault = fault } };
        var calls = 0;

        // Act
        var act = () => sut.Handle(new TestMessage(), CancellationToken.None, handler, () =>
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
        var sut = new ChaosExceptionMessageHandlerBehavior(NullLoggerFactory.Instance);
        var handler = new TestHandler { Chaos = new ChaosExceptionMessageHandlerOptions { InjectionRate = 0 } };
        var calls = 0;

        // Act
        await sut.Handle(new TestMessage(), CancellationToken.None, handler, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        calls.ShouldBe(1);
    }

    private sealed class TestMessage : MessageBase;

    private sealed class TestHandler : IRetryMessageHandler, ITimeoutMessageHandler, IChaosExceptionMessageHandler
    {
        public RetryMessageHandlerOptions Retry { get; init; } = new();

        public TimeoutMessageHandlerOptions Timeout { get; init; } = new();

        public ChaosExceptionMessageHandlerOptions Chaos { get; init; } = new();

        RetryMessageHandlerOptions IRetryMessageHandler.Options => this.Retry;

        TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => this.Timeout;

        ChaosExceptionMessageHandlerOptions IChaosExceptionMessageHandler.Options => this.Chaos;
    }
}
