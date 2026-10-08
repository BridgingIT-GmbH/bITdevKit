// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies recoverable instrumentation failures remain isolated from the host and business execution.</summary>
/// <example>Injects timer and clock faults into the actual registered workers.</example>
public sealed class OperationProfilingFailureIsolationTests
{
    /// <summary>Timer creation failures cannot fault hosted workers or stop an otherwise healthy application.</summary>
    [Fact]
    public async Task RegisteredWorkers_TimerCreationFails_HostAndBusinessWorkSurvive()
    {
        // Arrange
        using var sut = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(new BrokenTimerClock());
            services.AddProfiling(options => options.Enabled()).WithOperationProfiling();
        }).Build();

        // Act
        await sut.StartAsync();
        var workers = sut.Services.GetServices<IHostedService>().OfType<BackgroundService>().ToArray();
        foreach (var worker in workers) { await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5)); }

        var calls = 0;
        var value = await sut.Services.GetRequiredService<IOperationProfiler>().RunOperationAsync("business", (_, _) =>
        {
            calls++;
            return Task.FromResult(42);
        });
        await sut.StopAsync();

        // Assert
        workers.Length.ShouldBe(3);
        workers.ShouldAllBe(worker => worker.ExecuteTask.IsCompletedSuccessfully);
        value.ShouldBe(42);
        calls.ShouldBe(1);
        sut.Services.GetRequiredService<IOperationProfilingHealthSource>().GetSnapshot().PersistenceFaults.ShouldBeGreaterThan(0);
    }

    /// <summary>A clock failure in queue health is reported as unavailable instead of throwing or implying an empty queue.</summary>
    [Fact]
    public void QueueClockFails_HealthRetainsCountsAndLabelsAgeUnavailable()
    {
        // Arrange
        var clock = new BrokenObservationClock();
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var queue = new OperationProfilingCompletionQueue(options, clock);
        queue.Activate(new() { WriterId = Guid.NewGuid(), Token = Guid.NewGuid(), StoreEpoch = Guid.NewGuid() });
        queue.TryEnqueue(new() { Id = Guid.NewGuid(), EstimatedPayloadBytes = 512 }).ShouldBeTrue();
        queue.Select(queue.Watermark()).ShouldHaveSingleItem().Uncertain = true;
        clock.Fail = true;
        var sut = new OperationProfilingHealthState(queue, new InMemoryProfilingStorageProvider(options), new ProfilingNodeIdentityProvider(), options);

        // Act
        var observed = sut.GetSnapshot();

        // Assert
        observed.QueueRecords.ShouldBe(1);
        observed.QueuePayloadBytes.ShouldBe(512);
        observed.QueueOldestAgeUnavailable.ShouldBeTrue();
        observed.PersistenceFaults.ShouldBe(1);
        queue.Snapshot().PendingUnknownCount.ShouldBe(1);
        queue.Retire(expired: false);
        queue.Snapshot().Count.ShouldBe(0);
    }

    /// <summary>Independent feature worker boundaries contain injected restoration faults and dispose their handle once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkerBoundary_FaultyStartOrRestoration_PreservesBusinessException(bool failStart)
    {
        var profiling = Substitute.For<IOperationProfiler>();
        var restoration = Substitute.For<IDisposable>();
        if (failStart) { profiling.BeginExecutionBoundary().Returns(_ => throw new InvalidOperationException("start")); }
        else { profiling.BeginExecutionBoundary().Returns(restoration); }

        restoration.When(scope => scope.Dispose()).Do(_ => throw new InvalidOperationException("restore"));
        var expected = new InvalidOperationException("business");
        var calls = 0;
        var actual = Should.Throw<InvalidOperationException>(() =>
        {
            using var boundary = profiling.BeginSafeExecutionBoundary();
            boundary?.Dispose();
            calls++;
            throw expected;
        });

        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        restoration.Received(failStart ? 0 : 1).Dispose();
    }

    /// <summary>Injected ticks coalesce, a canceled wait restores admission, and disposal releases a pending waiter.</summary>
    [Fact]
    public async Task WorkerTimer_CoalescesTicksAndPreservesSingleConsumerCancellation()
    {
        var clock = new FakeTimeProvider();
        using var sut = new ProfilingPeriodicTimer(TimeSpan.FromSeconds(1), clock);
        clock.Advance(TimeSpan.FromSeconds(10));
        (await sut.WaitForNextTickAsync()).ShouldBeTrue();
        using var canceled = new CancellationTokenSource();
        var pending = sut.WaitForNextTickAsync(canceled.Token).AsTask();
        Should.Throw<InvalidOperationException>(() => sut.WaitForNextTickAsync());
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        var next = sut.WaitForNextTickAsync().AsTask();
        clock.Advance(TimeSpan.FromSeconds(1));
        (await next).ShouldBeTrue();
        var stopping = sut.WaitForNextTickAsync().AsTask();
        sut.Dispose();
        (await stopping).ShouldBeFalse();
        (await sut.WaitForNextTickAsync()).ShouldBeFalse();
    }

    /// <summary>A failed injected factory leaves no unsafe finalizer to run during subsequent garbage collection.</summary>
    [Fact]
    public void WorkerTimer_FactoryThrows_GarbageCollectionRemainsSafe()
    {
        Should.Throw<InvalidOperationException>(() => new ProfilingPeriodicTimer(TimeSpan.FromSeconds(1), new BrokenTimerClock()));
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private sealed class BrokenTimerClock : TimeProvider
    {
        /// <inheritdoc />
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period) =>
            throw new InvalidOperationException("timer unavailable");
    }

    private sealed class BrokenObservationClock : TimeProvider
    {
        /// <summary>Enables a recoverable clock fault after successful enqueue.</summary>
        public bool Fail { get; set; }
        /// <inheritdoc />
        public override long GetTimestamp() => this.Fail ? throw new InvalidOperationException("clock unavailable") : base.GetTimestamp();
    }
}
