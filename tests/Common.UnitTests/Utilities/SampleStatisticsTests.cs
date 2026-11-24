// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

/// <summary>Checks actual-observation ranks, exact value types and public input validation.</summary>
public sealed class SampleStatisticsTests
{
    /// <summary>Nearest-rank selection preserves endpoints and rank boundaries without interpolation.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0.2, 1)]
    [InlineData(0.2001, 2)]
    [InlineData(0.5, 3)]
    [InlineData(0.95, 5)]
    [InlineData(1, 5)]
    public void NearestRank_ValidQuantile_SelectsActualRank(double quantile, int expected)
    {
        var observations = new[] { 1, 2, 3, 4, 5 };

        var sut = SampleStatistics.NearestRank(observations, quantile);

        sut.ShouldBe(expected);
        observations.ShouldBe([1, 2, 3, 4, 5]);
    }

    /// <summary>Tail percentiles select the same nearest rank for larger observation counts.</summary>
    [Theory]
    [InlineData(20, 0.95, 19)]
    [InlineData(100, 0.99, 99)]
    public void NearestRank_TailQuantile_SelectsObservedPercentile(int count, double quantile, int expected)
    {
        var observations = Enumerable.Range(1, count).ToArray();

        var sut = SampleStatistics.NearestRank(observations, quantile);

        sut.ShouldBe(expected);
    }

    /// <summary>Integral observations near Int64.MaxValue retain their exact value.</summary>
    [Fact]
    public void NearestRank_LargeIntegralObservations_PreservesPrecision()
    {
        var observations = new[] { long.MaxValue - 1, long.MaxValue };

        var sut = SampleStatistics.NearestRank(observations, 0.5);

        sut.ShouldBe(long.MaxValue - 1);
    }

    /// <summary>TimeSpan observations keep their type and tick precision.</summary>
    [Fact]
    public void NearestRank_DurationObservations_PreservesTicks()
    {
        var observations = new[] { TimeSpan.FromTicks(1), TimeSpan.FromTicks(2) };

        var sut = SampleStatistics.NearestRank(observations, 0.95);

        sut.ShouldBe(TimeSpan.FromTicks(2));
    }

    /// <summary>Repeated zero values remain valid observations and influence the selected rank.</summary>
    [Fact]
    public void NearestRank_RepeatedZeroObservations_PreservesZeros()
    {
        var observations = new[] { 0d, 0d, 0d, 10d };

        var sut = SampleStatistics.NearestRank(observations, 0.5);

        sut.ShouldBe(0);
    }

    /// <summary>Missing observations have no implicit zero-valued quantile.</summary>
    [Fact]
    public void NearestRank_EmptyOrNullObservations_ThrowsNamedArgumentException()
    {
        Should.Throw<ArgumentNullException>(() => SampleStatistics.NearestRank<int>(null, 0.5)).ParamName.ShouldBe("sortedValues");
        Should.Throw<ArgumentException>(() => SampleStatistics.NearestRank(Array.Empty<int>(), 0.5)).ParamName.ShouldBe("sortedValues");
    }

    /// <summary>Only finite fractions in the documented range are accepted.</summary>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NearestRank_InvalidQuantile_ThrowsNamedArgumentException(double quantile)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SampleStatistics.NearestRank(new[] { 1 }, quantile)).ParamName.ShouldBe("quantile");
    }
}