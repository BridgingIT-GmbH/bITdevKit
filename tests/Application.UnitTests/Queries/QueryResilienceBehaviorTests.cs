// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#pragma warning disable CS0618 // obsolete query request types are covered by these behaviors
namespace BridgingIT.DevKit.Application.UnitTests.Queries;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;

public class QueryResilienceBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_WhenHandlerFailsTwice_RetriesAndReturnsResult()
    {
        // Arrange
        var sut = new RetryQueryBehavior<TestQuery, QueryResponse<Result<int>>>(NullLoggerFactory.Instance);
        var query = new TestQuery { Retry = new RetryQueryOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) } };
        var calls = 0;

        // Act
        var response = await sut.Handle(query, async _ =>
        {
            await Task.Yield();
            return ++calls < 3 ? throw new InvalidOperationException("boom") : QueryResult.For(42);
        }, CancellationToken.None);

        // Assert
        calls.ShouldBe(3);
        response.Result.Value.ShouldBe(42);
    }

    [Fact]
    public async Task TimeoutBehavior_WhenHandlerIgnoresCancellation_ThrowsTimeoutRejectedWithoutWaiting()
    {
        // Arrange
        var sut = new TimeoutQueryBehavior<TestQuery, QueryResponse<Result<int>>>(NullLoggerFactory.Instance);
        var query = new TestQuery { Timeout = new TimeoutQueryOptions { Timeout = TimeSpan.FromMilliseconds(100) } };
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Handle(query, async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return QueryResult.For(1);
        }, CancellationToken.None);

        // Assert
        await Should.ThrowAsync<TimeoutRejectedException>(act);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CircuitBreakerBehavior_WhenConsecutiveFailuresReachThreshold_OpensCircuitUntilBreakDurationElapsed()
    {
        // Arrange
        var sut = new CircuitBreakerQueryBehavior<TestQuery, QueryResponse<Result<int>>>(NullLoggerFactory.Instance);
        var query = new TestQuery
        {
            CircuitBreaker = new CircuitBreakerQueryOptions
            {
                Attempts = 3,
                Backoff = TimeSpan.FromMilliseconds(20),
                BreakDuration = TimeSpan.FromMilliseconds(600)
            }
        };
        var calls = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await sut.Handle(query, async _ =>
        {
            await Task.Yield();
            return ++calls <= 3 ? throw new InvalidOperationException("boom") : QueryResult.For(9);
        }, CancellationToken.None);

        // Assert
        calls.ShouldBe(4);
        response.Result.Value.ShouldBe(9);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsOne_ThrowsConfiguredFaultWithoutCallingHandler()
    {
        // Arrange
        var sut = new ChaosExceptionQueryBehavior<TestQuery, QueryResponse<Result<int>>>(NullLoggerFactory.Instance);
        var fault = new ArgumentException("chaos");
        var query = new TestQuery { Chaos = new ChaosExceptionQueryOptions { InjectionRate = 1, Fault = fault } };
        var calls = 0;

        // Act
        var act = () => sut.Handle(query, _ =>
        {
            calls++;
            return Task.FromResult(QueryResult.For(1));
        }, CancellationToken.None);

        // Assert
        (await Should.ThrowAsync<ArgumentException>(act)).ShouldBeSameAs(fault);
        calls.ShouldBe(0);
    }

    private sealed class TestQuery()
        : QueryRequestBase<Result<int>>(Guid.NewGuid()), IRetryQuery, ITimeoutQuery, ICircuitBreakerQuery, IChaosExceptionQuery
    {
        public RetryQueryOptions Retry { get; init; } = new();

        public TimeoutQueryOptions Timeout { get; init; } = new();

        public CircuitBreakerQueryOptions CircuitBreaker { get; init; } = new();

        public ChaosExceptionQueryOptions Chaos { get; init; } = new();

        RetryQueryOptions IRetryQuery.Options => this.Retry;

        TimeoutQueryOptions ITimeoutQuery.Options => this.Timeout;

        CircuitBreakerQueryOptions ICircuitBreakerQuery.Options => this.CircuitBreaker;

        ChaosExceptionQueryOptions IChaosExceptionQuery.Options => this.Chaos;
    }
}
