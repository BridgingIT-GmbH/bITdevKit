// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.JobScheduling;

using System.Diagnostics;
using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Timeout;
using Quartz;

public class JobSchedulingResilienceBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_WhenJobFailsTwice_RetriesUntilSuccess()
    {
        // Arrange
        var sut = new RetryJobSchedulingBehavior(NullLoggerFactory.Instance);
        var context = CreateContext(new TestJob { Retry = new RetryJobSchedulingOptions { Attempts = 3, Backoff = TimeSpan.FromMilliseconds(10) } });
        var calls = 0;

        // Act
        await sut.Execute(context, async () =>
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
    public async Task RetryBehavior_WhenJobAlwaysFails_RethrowsAfterAllAttempts()
    {
        // Arrange
        var sut = new RetryJobSchedulingBehavior(NullLoggerFactory.Instance);
        var context = CreateContext(new TestJob { Retry = new RetryJobSchedulingOptions { Attempts = 2, Backoff = TimeSpan.FromMilliseconds(10) } });
        var calls = 0;

        // Act
        var act = () => sut.Execute(context, async () =>
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
    public async Task TimeoutBehavior_WhenJobIgnoresCancellation_ThrowsTimeoutRejectedWithoutWaiting()
    {
        // Arrange
        var sut = new TimeoutJobSchedulingBehavior(NullLoggerFactory.Instance);
        var context = CreateContext(new TestJob { Timeout = new TimeoutJobSchedulingOptions { Timeout = TimeSpan.FromMilliseconds(100) } });
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => sut.Execute(context, () => Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        await Should.ThrowAsync<TimeoutRejectedException>(act);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ChaosBehavior_WhenInjectionRateIsOne_ThrowsConfiguredFaultWithoutCallingNext()
    {
        // Arrange
        var sut = new ChaosExceptionJobSchedulingBehavior(NullLoggerFactory.Instance);
        var fault = new ArgumentException("chaos");
        var context = CreateContext(new TestJob { Chaos = new ChaosExceptionJobSchedulingOptions { InjectionRate = 1, Fault = fault } });
        var calls = 0;

        // Act
        var act = () => sut.Execute(context, () =>
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
        var sut = new ChaosExceptionJobSchedulingBehavior(NullLoggerFactory.Instance);
        var context = CreateContext(new TestJob { Chaos = new ChaosExceptionJobSchedulingOptions { InjectionRate = 0 } });
        var calls = 0;

        // Act
        await sut.Execute(context, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        // Assert
        calls.ShouldBe(1);
    }

    private static IJobExecutionContext CreateContext(TestJob job)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.JobInstance.Returns(new JobWrapper(Substitute.For<IServiceProvider>(), job, []));
        context.JobDetail.Returns(JobBuilder.Create<TestJob>().WithIdentity("job-1").Build());

        return context;
    }

    private sealed class TestJob : IJob, IRetryJobScheduling, ITimeoutJobScheduling, IChaosExceptionJobScheduling
    {
        public RetryJobSchedulingOptions Retry { get; init; } = new();

        public TimeoutJobSchedulingOptions Timeout { get; init; } = new();

        public ChaosExceptionJobSchedulingOptions Chaos { get; init; } = new();

        RetryJobSchedulingOptions IRetryJobScheduling.Options => this.Retry;

        TimeoutJobSchedulingOptions ITimeoutJobScheduling.Options => this.Timeout;

        ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => this.Chaos;

        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
