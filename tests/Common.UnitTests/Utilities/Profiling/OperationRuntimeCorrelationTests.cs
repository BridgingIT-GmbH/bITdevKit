// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>Verifies honest Runtime coverage and process identity boundaries.</summary>
public sealed class OperationRuntimeCorrelationTests
{
    /// <summary>UTC discontinuities do not claim any Runtime ownership or issue an ambiguous provider query.</summary>
    [Fact]
    public async Task ClockDiscontinuity_IsUnavailable_WithoutProviderLookup()
    {
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var scope = profiler.BeginOperation("work");
        scope.Complete();
        scope.Dispose();
        var record = sink.Records.Single() with { Quality = new() { ClockDiscontinuity = true } };
        var store = Substitute.For<IRuntimeProfilingCorrelationStore>();
        var sut = new OperationRuntimeCorrelationService(store);
        var result = await sut.GetOverlayAsync(record);
        result.Value.ClockDiscontinuity.ShouldBeTrue();
        result.Value.Available.ShouldBeFalse();
        await store.DidNotReceiveWithAnyArgs().QueryCorrelationAsync(default, default);
    }

    /// <summary>Rate intervals, sparse sampling and clock jumps remain visible rather than being interpolated.</summary>
    [Fact]
    public async Task SparseAndSkewedSamples_ExposeGapsAndUnavailableRateIntervals()
    {
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var scope = profiler.BeginOperation("work");
        clock.Advance(TimeSpan.FromSeconds(10));
        scope.Complete();
        scope.Dispose();
        var record = sink.Records.Single();
        var session = new RuntimeProfilingSession { Identity = ProfilingIdentityFactory.CreateRuntimeSession(), SamplingInterval = TimeSpan.FromSeconds(1) };
        var first = new RuntimeProfilingSnapshot { Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(), SessionId = session.Identity.Id, SessionKey = session.Identity.Key,
            NodeId = record.NodeId, NodeKey = record.Node.Identity.Key, Sequence = 1, TimestampUtc = record.StartedUtc, CaptureStartedElapsed = TimeSpan.Zero };
        var next = first with { Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(), Sequence = 4, TimestampUtc = record.StartedUtc.AddSeconds(4), CaptureStartedElapsed = TimeSpan.FromSeconds(4), SkippedCaptureCount = 2 };
        var skewed = first with { Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(), Sequence = 5, TimestampUtc = record.StartedUtc.AddSeconds(3), CaptureStartedElapsed = TimeSpan.FromSeconds(5), SkippedCaptureCount = 2 };
        var store = Substitute.For<IRuntimeProfilingCorrelationStore>();
        store.QueryCorrelationAsync(Arg.Any<RuntimeProfilingCorrelationRequest>(), Arg.Any<CancellationToken>()).Returns(Result<RuntimeProfilingCorrelationSelection>.Success(new() { Sessions = [session], Snapshots = [first, next, skewed] }));
        var sut = new OperationRuntimeCorrelationService(store);
        var result = (await sut.GetOverlayAsync(record)).Value;
        result.Gaps.Count.ShouldBe(2);
        result.ClockDiscontinuity.ShouldBeTrue();
        result.Samples.Single(value => value.Snapshot.Sequence == 4).MetricIntervalFromUtc.ShouldBeNull();
        result.Samples.Single(value => value.Snapshot.Sequence == 5).MetricIntervalFromUtc.ShouldBeNull();
        result.Samples.Single(value => value.Snapshot.Sequence == 1).MetricIntervalFromUtc.ShouldBeNull();
    }

    /// <summary>Zero-duration operations have one exact inclusive instant while ordinary interval ends are exclusive.</summary>
    [Fact]
    public void HalfOpenIntervals_KeepInstantOperationsAndContextDistinct()
    {
        var utc = DateTimeOffset.UtcNow;
        OperationRuntimeCorrelationService.Relation(utc, utc, utc).ShouldBe("Inside");
        OperationRuntimeCorrelationService.Relation(utc.AddTicks(1), utc, utc).ShouldBe("After");
        OperationRuntimeCorrelationService.Relation(utc.AddSeconds(1), utc, utc.AddSeconds(1)).ShouldBe("After");
    }
}
