// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common.Resiliancy;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

public class ResiliencyTests
{
    // Helper method to simulate work with optional failure
    private async Task<int> SimulateWorkAsync(CancellationToken ct, bool fail = false)
    {
        await Task.Delay(100, ct);
        if (fail) throw new Exception("Simulated failure");
        return 42;
    }

    // Helper to collect progress updates
    private readonly List<object> progressUpdates = new();

    [Fact]
    public async Task Retryer_ExecutesWithRetries_ReportsProgressAndReturnsResult()
    {
        // Arrange
        var progress = new Progress<RetryProgress>(p => this.progressUpdates.Add(p));
        var retryer = new RetryerBuilder(3, TimeSpan.FromSeconds(0.1))
            .WithProgress(progress)
            .HandleErrors(new ConsoleLogger()) // Handle errors to avoid exception
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await retryer.ExecuteAsync(ct => this.SimulateWorkAsync(ct, true), cts.Token);

        // Assert
        this.progressUpdates.Count.ShouldBeGreaterThanOrEqualTo(1); // At least one progress update
        this.progressUpdates.ShouldAllBe(p => p is RetryProgress);
        var lastProgress = (RetryProgress)this.progressUpdates[^1];
        lastProgress.Status.ShouldContain("failed"); // Should fail all attempts with handleErrors
        lastProgress.CurrentAttempt.ShouldBeInRange(1, 3);
        lastProgress.MaxAttempts.ShouldBe(3);
    }

    [Fact]
    public async Task Debouncer_DebouncesExecution_ReportsProgress()
    {
        // Arrange
        var executionCount = 0;
        var progressUpdates = new List<DebouncerProgress>();
        var debouncer = new Debouncer(
            delay: TimeSpan.FromSeconds(0.5),
            action: async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.CompletedTask;
            },
            progress: new Progress<DebouncerProgress>(p => progressUpdates.Add(p))
        );
        var cts = new CancellationTokenSource();

        // Act
        var task1 = debouncer.DebounceAsync(cts.Token);
        var task2 = debouncer.DebounceAsync(cts.Token);
        await Task.Delay(100); // Simulate rapid calls
        var task3 = debouncer.DebounceAsync(cts.Token);
        await Task.WhenAll(task1, task2, task3);
        await Task.Delay(600); // Wait for debounce interval

        // Assert
        Assert.Equal(1, executionCount); // Executes once
        Assert.True(progressUpdates.Any(p => p.Status.Contains("Executing")), "Should report execution");
        Assert.True(progressUpdates.Count >= 2, "Should have multiple progress updates");
    }

    //[Fact]
    //public async Task Debouncer_DebouncesAndThrottles_ReportsProgress()
    //{
    //    // Arrange
    //    var executionCount = 0;
    //    var progress = new Progress<DebouncerProgress>(p => this.progressUpdates.Add(p));
    //    var debouncer = new DebouncerBuilder(TimeSpan.FromSeconds(0.5), async ct =>
    //    {
    //        executionCount++;
    //        await this.SimulateWorkAsync(ct);
    //    })
    //        .WithProgress(progress)
    //        .UseThrottling()
    //        .Build();
    //    var cts = new CancellationTokenSource();

    //    // Act
    //    await debouncer.DebounceAsync(cts.Token); // First execution
    //    await debouncer.DebounceAsync(cts.Token); // Should throttle
    //    await Task.Delay(700); // Allow throttle to reset (increased for reliability)
    //    await debouncer.DebounceAsync(cts.Token); // Next execution

    //    // Assert
    //    executionCount.ShouldBe(2); // Should execute twice (first and third calls)
    //    this.progressUpdates.Count.ShouldBeGreaterThanOrEqualTo(3); // At least three updates: execution, throttle, execution
    //    this.progressUpdates.ShouldAllBe(p => p is DebouncerProgress);
    //    var throttleUpdate = this.progressUpdates.Find(p => ((DebouncerProgress)p).IsThrottling && ((DebouncerProgress)p).RemainingDelay > TimeSpan.Zero);
    //    throttleUpdate.ShouldNotBeNull();
    //    var executionUpdates = this.progressUpdates.FindAll(p => ((DebouncerProgress)p).Status.Contains("Executing"));
    //    executionUpdates.Count.ShouldBe(2); // Two executions (first and third)
    //    var throttleIndex = this.progressUpdates.IndexOf(throttleUpdate);
    //    var executionIndexes = executionUpdates.Select(p => this.progressUpdates.IndexOf(p)).OrderBy(i => i).ToList();
    //    executionIndexes[0].ShouldBeLessThan(throttleIndex); // First execution before throttle
    //    executionIndexes[1].ShouldBeGreaterThan(throttleIndex); // Second execution after throttle
    //}

    [Fact]
    public async Task Throttler_ThrottlesExecution_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<ThrottlerProgress>(p => this.progressUpdates.Add(p));
        var throttler = new ThrottlerBuilder(TimeSpan.FromSeconds(0.5), ct => this.SimulateWorkAsync(ct))
            .WithProgress(progress)
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await throttler.ThrottleAsync(cts.Token); // First execution
        await throttler.ThrottleAsync(cts.Token); // Should throttle
        await Task.Delay(600); // Allow throttle to reset
        await throttler.ThrottleAsync(cts.Token); // Next execution

        // Assert
        this.progressUpdates.Count.ShouldBeGreaterThanOrEqualTo(2); // At least two updates (throttle and execution)
        this.progressUpdates.ShouldAllBe(p => p is ThrottlerProgress);
        var throttleUpdate = this.progressUpdates.Find(p => ((ThrottlerProgress)p).RemainingInterval > TimeSpan.Zero);
        throttleUpdate.ShouldNotBeNull();
        var executionUpdate = this.progressUpdates.Find(p => ((ThrottlerProgress)p).Status.Contains("Executing"));
        executionUpdate.ShouldNotBeNull();
    }

    [Fact]
    public async Task CircuitBreaker_HandlesFailuresAndRecovery_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<CircuitBreakerProgress>(p => progressUpdates.Add(p));
        var circuitBreaker = new CircuitBreakerBuilder(2, TimeSpan.FromSeconds(1))
            .WithProgress(progress)
            .HandleErrors(new ConsoleLogger()) // Handle errors to avoid exception propagation
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await circuitBreaker.ExecuteAsync(ct => SimulateWorkAsync(ct, true), cts.Token); // First failure
        await circuitBreaker.ExecuteAsync(ct => SimulateWorkAsync(ct, true), cts.Token); // Second failure (opens circuit)
        await Task.Delay(1100); // Wait for reset
        await circuitBreaker.ExecuteAsync(ct => SimulateWorkAsync(ct, false), cts.Token); // Should succeed

        // Assert
        progressUpdates.Count.ShouldBeGreaterThanOrEqualTo(3); // At least three updates (failures and state changes)
        progressUpdates.ShouldAllBe(p => p is CircuitBreakerProgress);
        var openUpdate = progressUpdates.Find(p => ((CircuitBreakerProgress)p).State == CircuitBreakerState.Open);
        openUpdate.ShouldNotBeNull();
        var halfOpenUpdate = progressUpdates.Find(p => ((CircuitBreakerProgress)p).State == CircuitBreakerState.HalfOpen);
        halfOpenUpdate.ShouldNotBeNull();
        var closedUpdate = progressUpdates.Find(p => ((CircuitBreakerProgress)p).State == CircuitBreakerState.Closed);
        closedUpdate.ShouldNotBeNull();
    }

    [Fact]
    public async Task RateLimiter_EnforcesRateLimit_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<RateLimiterProgress>(p => this.progressUpdates.Add(p));
        var rateLimiter = new RateLimiterBuilder(2, TimeSpan.FromSeconds(1))
            .WithProgress(progress)
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await rateLimiter.ExecuteAsync(ct => this.SimulateWorkAsync(ct), cts.Token); // First operation
        await rateLimiter.ExecuteAsync(ct => this.SimulateWorkAsync(ct), cts.Token); // Second operation
        await Assert.ThrowsAsync<RateLimitExceededException>(() => rateLimiter.ExecuteAsync(ct => this.SimulateWorkAsync(ct), cts.Token)); // Should exceed limit

        // Assert
        this.progressUpdates.Count.ShouldBe(3); // One for each operation attempt
        this.progressUpdates.ShouldAllBe(p => p is RateLimiterProgress);
        var allowedUpdate = this.progressUpdates[0] as RateLimiterProgress;
        allowedUpdate.ShouldNotBeNull();
        allowedUpdate.CurrentOperations.ShouldBe(1);
        var exceededUpdate = this.progressUpdates[2] as RateLimiterProgress;
        exceededUpdate.ShouldNotBeNull();
        exceededUpdate.Status.ShouldContain("exceeded");
    }

    [Fact]
    public async Task SimpleNotifier_PublishesEvents_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<SimpleNotifierProgress>(p => this.progressUpdates.Add(p));
        var notifier = new SimpleNotifierBuilder()
            .WithProgress(progress)
            .Build();
        notifier.Subscribe<string>(async (msg, ct) => await this.SimulateWorkAsync(ct));
        notifier.Subscribe<string>(async (msg, ct) => await this.SimulateWorkAsync(ct));
        var cts = new CancellationTokenSource();

        // Act
        await notifier.PublishAsync("Test", cancellationToken: cts.Token);

        // Assert
        this.progressUpdates.Count.ShouldBe(2); // One update per handler
        this.progressUpdates.ShouldAllBe(p => p is SimpleNotifierProgress);
        var progressUpdate = (SimpleNotifierProgress)this.progressUpdates[0];
        progressUpdate.HandlersProcessed.ShouldBe(1);
        progressUpdate.TotalHandlers.ShouldBe(2);
    }

    [Fact]
    public async Task BackgroundWorker_ExecutesWork_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<BackgroundWorkerProgress>(p => this.progressUpdates.Add(p));
        var worker = new BackgroundWorkerBuilder(async (ct, p) =>
        {
            for (var i = 0; i <= 100; i += 20)
            {
                await Task.Delay(50, ct);
                p.Report(i);
            }
        })
            .WithProgress(progress)
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);

        // Assert
        this.progressUpdates.Count.ShouldBe(6); // 0, 20, 40, 60, 80, 100
        this.progressUpdates.ShouldAllBe(p => p is BackgroundWorkerProgress);
        var lastProgress = (BackgroundWorkerProgress)this.progressUpdates[^1];
        lastProgress.ProgressPercentage.ShouldBe(100);
    }

    [Fact]
    public async Task SimpleRequester_SendsRequest_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<SimpleRequesterProgress>(p => this.progressUpdates.Add(p));
        var requester = new SimpleRequesterBuilder()
            .WithProgress(progress)
            //.AddPipelineBehavior(new LoggingRequestPipelineBehavior(new ConsoleLogger())) // Optional logging behavior
            .Build();
        requester.RegisterHandler<TestRequest, string>(new TestRequestHandler());
        var cts = new CancellationTokenSource();

        // Act
        var response = await requester.SendAsync<TestRequest, string>(new TestRequest(), cancellationToken: cts.Token);

        // Assert
        this.progressUpdates.Count.ShouldBe(1);
        this.progressUpdates.ShouldAllBe(p => p is SimpleRequesterProgress);
        var progressUpdate = (SimpleRequesterProgress)this.progressUpdates[0];
        progressUpdate.RequestType.ShouldBe(nameof(TestRequest));
        response.ShouldBe("Processed: TestRequest");
    }

    [Fact]
    public async Task TimeoutHandler_EnforcesTimeout_ReportsProgress()
    {
        // Arrange
        var progress = new Progress<TimeoutHandlerProgress>(p => this.progressUpdates.Add(p));
        var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(0.1)) // Reduced timeout to 100ms
            .WithProgress(progress)
            .Build();
        var cts = new CancellationTokenSource();

        // Act
        await Assert.ThrowsAsync<TimeoutException>(() => timeoutHandler.ExecuteAsync(ct => this.SimulateWorkAsync(ct, false), cts.Token)); // Increased delay to 200ms

        // Assert
        this.progressUpdates.Count.ShouldBeGreaterThanOrEqualTo(1); // At least one update during timeout
        this.progressUpdates.ShouldAllBe(p => p is TimeoutHandlerProgress);
        var lastProgress = (TimeoutHandlerProgress)this.progressUpdates[^1];
        lastProgress.Status.ShouldContain("timed out");
    }
}

// Test implementation for Requester with proper IRequest implementation
public class TestRequest : ISimpleRequest<string> { }

public class TestRequestHandler : ISimpleRequestHandler<TestRequest, string>
{
    public Task<string> HandleAsync(TestRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Processed: {nameof(TestRequest)}");
    }
}

public class ConsoleLogger : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        if (exception != null) Console.WriteLine(exception.ToString());
    }
}