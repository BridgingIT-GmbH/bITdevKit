// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public sealed class OperationProfilingSegmentTests
{
    [Fact]
    public void RepeatedLoops_AggregateFullPaths_WithoutRetainingOccurrences()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        for (var batch = 0; batch < 5; batch++)
        {
            root.RunSegment(batch == 0 ? "Load" : "load", parent =>
            {
                for (var item = 0; item < 3; item++)
                {
                    parent.RunSegment(item == 0 ? "Read" : "read", _ => clock.Advance(TimeSpan.FromMilliseconds(10)));
                }

                clock.Advance(TimeSpan.FromMilliseconds(5));
            });
        }

        root.RunSegment("Calculate", p => p.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(20))));
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Segments.Count.ShouldBe(4);
        var load = record.Segments.Single(s => s.Path.Components.Count == 1 && s.Key == "Load");
        load.Statistics.Count.ShouldBe(5);
        load.Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(175));
        load.Statistics.TotalSelfDuration.ShouldBe(TimeSpan.FromMilliseconds(25));
        var reads = record.Segments.Single(s => s.ParentPath?.Components[0] == "Load");
        reads.Key.ShouldBe("Read");
        reads.Statistics.Count.ShouldBe(15);
        reads.Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(195));
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Fact]
    public void OverlappingDirectChildren_SubtractUnion_AndUseActualParentHandles()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var parent = root.BeginSegment("Load");
        var first = parent.BeginSegment("Read");
        clock.Advance(TimeSpan.FromMilliseconds(30));
        var second = parent.BeginSegment("Read");

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(50));
        first.Complete();
        first.Dispose();
        clock.Advance(TimeSpan.FromMilliseconds(20));
        second.Complete();
        second.Dispose();
        parent.Complete();
        parent.Dispose();
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        var load = record.Segments.Single(s => s.Key == "Load");
        load.Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
        load.Statistics.TotalSelfDuration.ShouldBe(TimeSpan.Zero);
        var reads = record.Segments.Single(s => s.Key == "Read");
        reads.Statistics.Count.ShouldBe(2);
        reads.Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        reads.Statistics.MinimumDuration.ShouldBe(TimeSpan.FromMilliseconds(70));
        reads.Statistics.MaximumDuration.ShouldBe(TimeSpan.FromMilliseconds(80));
    }

    [Fact]
    public async Task ParallelBranches_KeepPerInvocationParents_AndMergeSamePathSafely()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;

        // Act
        await profiler.RunOperationAsync("parallel", async (root, _) =>
        {
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => root.RunSegmentAsync("Load", async (parent, _) =>
            {
                await parent.RunSegmentAsync("Read", async (_, _) =>
                {
                    if (Interlocked.Increment(ref count) == 16)
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(10));
                        release.SetResult();
                    }

                    await release.Task;
                });
            })));
        });

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Segments.Count.ShouldBe(2);
        record.Segments.All(s => s.Statistics.Count == 16).ShouldBeTrue();
        record.Segments.All(s => s.Statistics.TotalDuration == TimeSpan.FromMilliseconds(160)).ShouldBeTrue();
        record.Segments.Single(s => s.Key == "Load").Statistics.TotalSelfDuration.ShouldBe(TimeSpan.Zero);
        profiler.Current.ShouldBeNull();
        profiler.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public void ClosingParent_ClosesChildrenIncomplete_AndLateCallsDoNotCountAgain()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var parent = root.BeginSegment("Load");
        var child = parent.BeginSegment("Read");
        clock.Advance(TimeSpan.FromMilliseconds(20));

        // Act
        parent.Complete();
        parent.Dispose();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        child.Complete();
        child.Dispose();
        child.Dispose();
        parent.Fail(new InvalidOperationException("late"));
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        var reads = record.Segments.Single(s => s.Key == "Read");
        reads.Statistics.Count.ShouldBe(1);
        reads.Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(20));
        reads.Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Incomplete);
        record.Quality.IncompleteSegmentCount.ShouldBe(1);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        profiler.Current.ShouldBeNull();
    }

    [Fact]
    public void DepthPathAndLiveLimits_SuppressRejectedDescendants_AndLabelAffectedParent()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create(o => { o.MaxSegmentDepth = 1; o.MaxSegmentPaths = 1; o.MaxLiveSegments = 1; });
        using var root = profiler.BeginOperation("root");

        // Act
        root.RunSegment("Load", parent =>
        {
            using var rejected = parent.BeginSegment("Read");
            rejected.IsRecording.ShouldBeFalse();
            profiler.RunSegment("Hidden", _ => clock.Advance(TimeSpan.FromMilliseconds(10)));
        });
        using (var rejected = root.BeginSegment("Other"))
        {
            rejected.IsRecording.ShouldBeFalse();
        }

        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Segments.ShouldHaveSingleItem().Key.ShouldBe("Load");
        record.Segments.Single().PartialCoverage.ShouldBeTrue();
        record.Quality.RejectedSegmentCount.ShouldBe(2);
        record.Quality.PartialCoverage.ShouldBeTrue();
    }

    [Fact]
    public void ManySequentialInvocations_KeepConstantPathAndLiveState()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create(o => { o.MaxSegmentPaths = 1; o.MaxLiveSegments = 1; });
        using var root = profiler.BeginOperation("root");
        long retainedBytes = 0;

        // Act
        for (var index = 0; index < 10000; index++)
        {
            root.RunSegment("Read", _ => clock.Advance(TimeSpan.FromTicks(1)));
            if (index == 0)
            {
                retainedBytes = profiler.ActiveBytes;
            }

            profiler.ActiveBytes.ShouldBe(retainedBytes);
        }

        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Segments.ShouldHaveSingleItem().Statistics.Count.ShouldBe(10000);
        record.Segments.Single().Statistics.TotalDuration.ShouldBe(TimeSpan.FromTicks(10000));
        record.Quality.RejectedSegmentCount.ShouldBe(0);
        profiler.ActiveBytes.ShouldBe(0);
    }

    [Fact]
    public void OutcomeDeclarations_FailureWins_AndSuccessfulRecoveryDoesNotFailRoot()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var first = root.BeginSegment("Read");

        // Act
        first.Fail(new InvalidOperationException("secret"));
        first.Complete();
        first.Cancel();
        first.Fail(new InvalidOperationException("again"));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        first.Dispose();
        root.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(20)));
        using (root.BeginSegment("Unmarked"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(5));
        }

        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        var reads = record.Segments.Single(s => s.Key == "Read");
        reads.Statistics.Count.ShouldBe(2);
        reads.Outcomes.Single(o => o.Outcome == ProfilingSegmentOutcome.Failed).Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(10));
        reads.Outcomes.Single(o => o.Outcome == ProfilingSegmentOutcome.Completed).Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(20));
        reads.Failures.ShouldHaveSingleItem().Count.ShouldBe(1);
        record.Quality.IncompleteSegmentCount.ShouldBe(1);
    }
}
