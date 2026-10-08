// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

public sealed class OperationProfilingCleanupTests
{
    [Fact]
    public void Expiry_ClipsRootChildrenAndWallTimeAtDeadline_AndReleasesAdmissionOnce()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create(o =>
        {
            o.MaxActiveOperations = 1;
            o.MaxRecordingDuration = TimeSpan.FromMilliseconds(50);
            o.CleanupInterval = TimeSpan.FromMilliseconds(10);
        });
        using var root = profiler.BeginOperation("root");
        root.Fail(new InvalidOperationException("observed before expiry"));
        var parent = root.BeginSegment("Load");
        var first = parent.BeginSegment("Read");
        var second = parent.BeginSegment("Read");
        clock.Advance(TimeSpan.FromMilliseconds(200));

        // Act
        profiler.ExpireAbandoned();
        profiler.ExpireAbandoned();
        root.IsRecording.ShouldBeFalse();
        awaitUnrecordedWork();
        first.Complete();
        first.Dispose();
        second.Dispose();
        parent.Dispose();
        root.Dispose();
        using (var independent = profiler.BeginOperation("independent"))
        {
            independent.IsRecording.ShouldBeTrue();
            independent.Complete();
        }

        // Assert
        var record = sink.Records.First();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Incomplete);
        record.Failure.ShouldNotBeNull();
        record.Quality.IncompleteReason.ShouldBe(ProfilingIncompleteReason.CaptureDeadlineExceeded);
        record.Quality.ActualCompletionUnobserved.ShouldBeTrue();
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.CompletedUtc.ShouldBe(record.StartedUtc.AddMilliseconds(50));
        record.Segments.Single(s => s.Key == "Read").Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
        record.Quality.IncompleteSegmentCount.ShouldBe(3);
        record.WallTime.Sum(b => b.Duration.Ticks).ShouldBe(record.Duration.Ticks);
        sink.Records.Count.ShouldBe(2);
        profiler.Counters.Expired.ShouldBe(1);
        profiler.ActiveCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);

        void awaitUnrecordedWork()
        {
            profiler.JoinOrStartAsync("late", OperationProfilingKind.Service, (_, _) => Task.FromResult(42)).GetAwaiter().GetResult().ShouldBe(42);
            sink.Records.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task CleanupWorker_ExpiresOverlappingActionsWhileBusinessDelegatesContinue()
    {
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true, MaxRecordingDuration = TimeSpan.FromMilliseconds(50), CleanupInterval = TimeSpan.FromMilliseconds(10) } };
        var sink = new OperationProfilerTests.CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(clock), sink, clock);
        using var worker = new OperationProfilingCleanupService(profiler, options, clock);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await worker.StartAsync(default);
        await worker.Ready.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var actions = Enumerable.Range(0, 3).Select(i => profiler.RunOperationAsync($"action:{i}", OperationProfilingKind.BlazorInteraction, async (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return await release.Task;
        })).ToArray();
        clock.Advance(TimeSpan.FromMilliseconds(60));
        await WaitUntil(() => sink.Records.Count == 3);
        actions.All(t => !t.IsCompleted).ShouldBeTrue();
        profiler.ActiveCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);
        release.SetResult(42);
        var results = await Task.WhenAll(actions);
        await worker.StopAsync(default);

        // Assert
        results.ShouldBe([42, 42, 42]);
        calls.ShouldBe(3);
        sink.Records.Count.ShouldBe(3);
        sink.Records.All(r => r.Outcome == OperationProfilingOutcome.Incomplete && r.Duration == TimeSpan.FromMilliseconds(50)).ShouldBeTrue();
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public async Task HostStop_ClosesRemainingCapturesBeforeWriterDrain_WithoutDuplicateLateDisposal()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        var options = new ProfilingOptions { Operations = new() { Enabled = true } };
        using var worker = new OperationProfilingCleanupService(profiler, options, clock);
        using var root = profiler.BeginOperation("root");
        using var segment = root.BeginSegment("Read");
        clock.Advance(TimeSpan.FromMilliseconds(20));
        await worker.StartAsync(default);
        await worker.Ready.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        await worker.StopAsync(default);
        root.Complete();
        segment.Complete();
        segment.Dispose();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Quality.IncompleteReason.ShouldBe(ProfilingIncompleteReason.HostStopping);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Incomplete);
        record.Segments.ShouldHaveSingleItem().Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Incomplete);
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletionSinkFailureOrRejection_ReleasesCapture_AndPreservesBusinessValue(bool throws)
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), new FailedSink(throws));
        var calls = 0;

        // Act
        var result = profiler.RunOperation("root", _ => { calls++; return 42; });

        // Assert
        result.ShouldBe(42);
        calls.ShouldBe(1);
        profiler.Counters.CompletionRejected.ShouldBe(1);
        profiler.ActiveCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public void ClockFailureDuringFinalization_DropsObservation_AndStillReleasesAllCapacity()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var clock = new ControlledClock();
        var sink = new OperationProfilerTests.CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink, clock);

        // Act
        var result = profiler.RunOperation("root", root =>
        {
            root.BeginSegment("Read");
            clock.ThrowUtc = true;
            return 42;
        });

        // Assert
        result.ShouldBe(42);
        sink.Records.ShouldBeEmpty();
        profiler.Counters.CaptureFaults.ShouldBeGreaterThan(0);
        profiler.ActiveCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public void UtcClockJump_DoesNotChangeMonotonicDuration_AndLabelsDiscontinuity()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var clock = new ControlledClock();
        var sink = new OperationProfilerTests.CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink, clock);

        // Act
        profiler.RunOperation("root", _ => { clock.Timestamp = TimeSpan.FromMilliseconds(50).Ticks; clock.Utc = clock.Utc.AddMinutes(-5); });

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.Quality.ClockDiscontinuity.ShouldBeTrue();
        record.CompletedUtc.ShouldBeLessThan(record.StartedUtc);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
    }

    /// <summary>Unexpected observation faults discard inconsistent state and immediately free recording capacity.</summary>
    [Fact]
    public void ClockFailureDuringObservation_ReleasesState_AndPreservesBusinessException()
    {
        // Arrange
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var clock = new ControlledClock();
        var sink = new OperationProfilerTests.CaptureSink();
        var sut = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink, clock);
        var expected = new InvalidOperationException("business");
        var calls = 0;

        // Act
        var actual = Should.Throw<InvalidOperationException>(() => sut.RunOperation<int>("root", root =>
        {
            calls++;
            using var segment = root.BeginSegment("Read");
            clock.ThrowTimestamp = true;
            segment.SetDimension("size", 5);
            sut.ActiveCount.ShouldBe(0);
            sut.ActiveBytes.ShouldBe(0);
            root.IsRecording.ShouldBeFalse();
            throw expected;
        }));

        // Assert
        actual.ShouldBeSameAs(expected);
        calls.ShouldBe(1);
        sink.Records.ShouldBeEmpty();
        sut.Counters.CaptureFaults.ShouldBeGreaterThan(0);
        sut.Counters.DiscardedCaptures.ShouldBe(1);
        sut.Current.ShouldBeNull();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class FailedSink(bool throws) : IOperationProfilingCompletionSink
    {
        public bool TryEnqueue(OperationProfilingRecord record) => throws ? throw new InvalidOperationException("storage observer") : false;
    }

    private sealed class ControlledClock : TimeProvider
    {
        public bool ThrowUtc { get; set; }
        public bool ThrowTimestamp { get; set; }
        public long Timestamp { get; set; }
        public DateTimeOffset Utc { get; set; } = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => this.ThrowTimestamp ? throw new InvalidOperationException("timestamp") : this.Timestamp;
        public override DateTimeOffset GetUtcNow() => this.ThrowUtc ? throw new InvalidOperationException("clock") : this.Utc;
    }
}
