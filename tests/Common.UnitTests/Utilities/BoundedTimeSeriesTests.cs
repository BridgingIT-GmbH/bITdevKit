// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Checks bounded retention, chronological windows and caller-owned aggregation.</summary>
public sealed class BoundedTimeSeriesTests
{
    [Fact]
    public void Query_WrappedRing_ReturnsRetainedInclusiveWindowInTimestampOrder()
    {
        var start = DateTimeOffset.UnixEpoch;
        var sut = new BoundedTimeSeries<(DateTimeOffset At, int Value)>(3, point => point.At);
        sut.Add((start.AddSeconds(1), 1));
        sut.Add((start.AddSeconds(4), 4));
        sut.Add((start.AddSeconds(2), 2));
        sut.Add((start.AddSeconds(3), 3));

        var result = sut.Query(start.AddSeconds(2), start.AddSeconds(4), 10);

        result.Select(point => point.Value).ShouldBe([2, 3, 4]);
        sut.Count.ShouldBe(3);
        sut.Capacity.ShouldBe(3);
        sut.Query(start.AddSeconds(3), start.AddSeconds(3), 10).Single().Value.ShouldBe(3);
    }

    [Fact]
    public void Query_DefaultDownsampling_KeepsLastActualObservationOfEachBucket()
    {
        var start = DateTimeOffset.UnixEpoch;
        var sut = new BoundedTimeSeries<(DateTimeOffset At, int Value)>(5, point => point.At);
        foreach (var value in Enumerable.Range(1, 5)) { sut.Add((start.AddSeconds(value), value)); }

        var result = sut.Query(start, start.AddSeconds(5), 2);

        result.Select(point => point.Value).ShouldBe([3, 5]);
        result.Select(point => point.At).ShouldBe([start.AddSeconds(3), start.AddSeconds(5)]);
    }

    [Fact]
    public void Query_CustomAggregation_ReceivesChronologicalNonemptyBuckets()
    {
        var start = DateTimeOffset.UnixEpoch;
        var sut = new BoundedTimeSeries<(DateTimeOffset At, double Rate, double Seconds)>(4, point => point.At);
        sut.Add((start.AddSeconds(5), 2, 5));
        sut.Add((start.AddSeconds(20), 4, 15));
        sut.Add((start.AddSeconds(25), 10, 5));
        sut.Add((start.AddSeconds(40), 0, 15));

        var result = sut.Query(start, start.AddSeconds(40), 2, bucket =>
            (bucket[^1].At, bucket.Sum(point => point.Rate * point.Seconds) / bucket.Sum(point => point.Seconds), bucket.Sum(point => point.Seconds)));

        result.Select(point => point.Rate).ShouldBe([3.5, 2.5]);
        result.Select(point => point.Seconds).ShouldBe([20d, 20d]);
        result.Select(point => point.At).ShouldBe([start.AddSeconds(20), start.AddSeconds(40)]);
    }

    [Fact]
    public void Query_Snapshot_DoesNotChangeWhenRingIsOverwritten()
    {
        var sut = new BoundedTimeSeries<DateTimeOffset>(1, point => point);
        sut.Add(DateTimeOffset.UnixEpoch);
        var result = sut.Query(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, 1);

        sut.Add(DateTimeOffset.UnixEpoch.AddSeconds(1));

        result.Single().ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ZeroCapacity_DiscardsObservationsAndReturnsEmptyWindows()
    {
        var sut = new BoundedTimeSeries<DateTimeOffset>(0, point => point);

        sut.Add(DateTimeOffset.UnixEpoch);

        sut.Count.ShouldBe(0);
        sut.Query(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, 1).ShouldBeEmpty();
    }

    [Fact]
    public void InvalidConfigurationOrQuery_ThrowsWithoutChangingRetention()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedTimeSeries<DateTimeOffset>(-1, point => point));
        Should.Throw<ArgumentNullException>(() => new BoundedTimeSeries<DateTimeOffset>(1, null));
        var sut = new BoundedTimeSeries<DateTimeOffset>(1, point => point);
        sut.Add(DateTimeOffset.UnixEpoch);

        Should.Throw<ArgumentOutOfRangeException>(() => sut.Query(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, 0));
        Should.Throw<ArgumentException>(() => sut.Query(DateTimeOffset.MaxValue, DateTimeOffset.MinValue, 1));

        sut.Count.ShouldBe(1);
    }
}