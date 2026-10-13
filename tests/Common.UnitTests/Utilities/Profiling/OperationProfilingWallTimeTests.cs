// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public sealed class OperationProfilingWallTimeTests
{
    [Fact]
    public void SequentialRepeatedKeys_CreditOneBucketAndOutsideTime()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(10));
        root.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(20)));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        root.RunSegment("read", _ => clock.Advance(TimeSpan.FromMilliseconds(30)));
        clock.Advance(TimeSpan.FromMilliseconds(30));
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Segment).Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Segment).Key.ShouldBe("Read");
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.OutsideSegments).Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.WallTime.Sum(b => b.Duration.Ticks).ShouldBe(record.Duration.Ticks);
    }

    [Fact]
    public void SameKeyOverlap_IsParallelOnce_RatherThanAdditivePercentages()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var first = root.BeginSegment("Read");
        clock.Advance(TimeSpan.FromMilliseconds(30));
        var second = root.BeginSegment("read");

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(50));
        first.Complete();
        first.Dispose();
        clock.Advance(TimeSpan.FromMilliseconds(20));
        second.Complete();
        second.Dispose();
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(100));
        record.Segments.ShouldHaveSingleItem().Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Segment).Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Parallel).Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
        record.WallTime.Sum(b => b.Duration.Ticks).ShouldBe(record.Duration.Ticks);
    }

    [Fact]
    public void NestedParallelChildren_CreditParentAtRootLevel_AndUnionAtParentLevel()
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
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Segment).Key.ShouldBe("Load");
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.Segment).Duration.ShouldBe(TimeSpan.FromMilliseconds(100));
        record.WallTime.ShouldNotContain(b => b.Kind == ProfilingWallTimeKind.Parallel);
        record.Segments.Single(s => s.Key == "Load").Statistics.TotalSelfDuration.ShouldBe(TimeSpan.Zero);
        record.WallTime.Sum(b => b.Duration.Ticks).ShouldBe(record.Duration.Ticks);
    }

    [Fact]
    public void RejectedSegments_LeaveCoverageExplicitlyPartial_AndTotalsStillSumToRoot()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create(o => o.MaxSegmentPaths = 1);
        using var root = profiler.BeginOperation("root");

        // Act
        root.RunSegment("Read", _ => clock.Advance(TimeSpan.FromMilliseconds(20)));
        root.RunSegment("Other", _ => clock.Advance(TimeSpan.FromMilliseconds(80)));
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Quality.PartialCoverage.ShouldBeTrue();
        record.Quality.RejectedSegmentCount.ShouldBe(1);
        record.WallTime.Single(b => b.Kind == ProfilingWallTimeKind.OutsideSegments).Duration.ShouldBe(TimeSpan.FromMilliseconds(80));
        record.WallTime.Sum(b => b.Duration.Ticks).ShouldBe(record.Duration.Ticks);
    }

    [Fact]
    public void ZeroDuration_ProducesZeroCoverageWithoutInventedTime()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();

        // Act
        profiler.RunOperation("root", root => root.RunSegment("Read", _ => { }));

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Duration.ShouldBe(TimeSpan.Zero);
        record.WallTime.All(b => b.Duration == TimeSpan.Zero).ShouldBeTrue();
        record.Segments.ShouldHaveSingleItem().Statistics.Count.ShouldBe(1);
    }
}
