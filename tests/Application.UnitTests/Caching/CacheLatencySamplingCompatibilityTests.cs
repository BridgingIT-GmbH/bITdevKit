// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Checks retained latency samples, exact window boundaries and nearest-rank statistics.</summary>
public sealed class CacheLatencySamplingCompatibilityTests
{
    /// <summary>Wrapping retains the last appended samples while the observation count includes evicted samples.</summary>
    [Fact]
    public void Snapshot_WrappedSamples_PreservesObservedCountAndActualQuantiles()
    {
        var clock = new Clock();
        var sut = Create(clock);
        foreach (var seconds in new[] { 9, 8, 0, 2, 1 })
        {
            sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(seconds));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var result = sut.Snapshot().Latencies[CacheLatencyKind.Operation];

        result.SamplesObserved.ShouldBe(5);
        result.RetainedSamples.ShouldBe(3);
        result.P50Seconds.ShouldBe(1);
        result.P95Seconds.ShouldBe(2);
        result.P99Seconds.ShouldBe(2);
        result.Accuracy.ShouldBe(CacheObservationAccuracy.Sampled);
        result.OldestSampleAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(2));
        result.NewestSampleAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(4));
    }

    /// <summary>Time windows include both endpoints and preserve observed counts after every retained sample expires.</summary>
    [Fact]
    public void Snapshot_ExactWindowBoundaries_PreservesZerosAndMissingSamples()
    {
        var clock = new Clock();
        var sut = Create(clock);
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.Zero);
        sut.Snapshot().Latencies[CacheLatencyKind.Operation].RetainedSamples.ShouldBe(3);

        clock.Advance(TimeSpan.FromSeconds(1));
        var selected = sut.Snapshot().Latencies[CacheLatencyKind.Operation];

        selected.RetainedSamples.ShouldBe(2);
        selected.P50Seconds.ShouldBe(0);
        selected.P95Seconds.ShouldBe(2);
        clock.Advance(TimeSpan.FromSeconds(10));
        var expired = sut.Snapshot().Latencies[CacheLatencyKind.Operation];
        expired.Availability.ShouldBe(CacheObservationAvailability.NoSamples);
        expired.Accuracy.ShouldBe(CacheObservationAccuracy.Unavailable);
        expired.SamplesObserved.ShouldBe(3);
        expired.RetainedSamples.ShouldBe(0);
        expired.P50Seconds.ShouldBeNull();
        expired.P95Seconds.ShouldBeNull();
        expired.OldestSampleAt.ShouldBeNull();
        expired.NewestSampleAt.ShouldBeNull();
    }

    /// <summary>A clock correction excludes future samples without discarding their retained observations.</summary>
    [Fact]
    public void Snapshot_ClockMovesBackward_ExcludesFutureSamplesUntilTheirTimestamp()
    {
        var clock = new Clock();
        var sut = Create(clock);
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(100));
        clock.Advance(TimeSpan.FromSeconds(-5));
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(2));

        var corrected = sut.Snapshot().Latencies[CacheLatencyKind.Operation];

        corrected.SamplesObserved.ShouldBe(3);
        corrected.RetainedSamples.ShouldBe(2);
        corrected.P50Seconds.ShouldBe(1);
        corrected.P95Seconds.ShouldBe(2);
        clock.Advance(TimeSpan.FromSeconds(5));
        var caughtUp = sut.Snapshot().Latencies[CacheLatencyKind.Operation];
        caughtUp.RetainedSamples.ShouldBe(3);
        caughtUp.P50Seconds.ShouldBe(2);
        caughtUp.P95Seconds.ShouldBe(100);
    }

    private static CacheObservationSlot Create(TimeProvider clock) => new("items", "memory", "NodeLocal",
        new() { LatencySampleCapacity = 3, LatencySampleWindow = TimeSpan.FromSeconds(10) }, clock, new CacheMetrics());

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan duration) => this.now += duration;
    }
}