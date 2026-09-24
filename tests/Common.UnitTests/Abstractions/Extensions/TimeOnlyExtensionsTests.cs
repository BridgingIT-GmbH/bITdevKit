// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class TimeOnlyExtensionsTests
{
    [Fact]
    public void Add_WithEveryTimeUnit_ReturnsExpectedTime()
    {
        var source = new TimeOnly(10, 20, 30, 400);

        source.Add(TimeUnit.Millisecond, 600).ShouldBe(new TimeOnly(10, 20, 31));
        source.Add(TimeUnit.Second, 30).ShouldBe(new TimeOnly(10, 21, 0, 400));
        source.Add(TimeUnit.Minute, 40).ShouldBe(new TimeOnly(11, 0, 30, 400));
        source.Add(TimeUnit.Hour, 2).ShouldBe(new TimeOnly(12, 20, 30, 400));
        source.Add(TimeUnit.Day, 1).ShouldBe(source);
    }

    [Fact]
    public void IsInRange_AtExclusiveBoundary_ReturnsFalse()
    {
        var source = new TimeOnly(10, 0);

        source.IsInRange(source, new TimeOnly(11, 0), inclusive: false).ShouldBeFalse();
    }

    [Fact]
    public void IsInRelativeRange_WithExplicitReference_ReturnsExpectedResult()
    {
        var reference = new TimeOnly(10, 0);

        new TimeOnly(10, 3).IsInRelativeRange(reference, TimeUnit.Minute, 5, DateTimeDirection.Future).ShouldBeTrue();
        new TimeOnly(10, 6).IsInRelativeRange(reference, TimeUnit.Minute, 5, DateTimeDirection.Future).ShouldBeFalse();
    }

    [Fact]
    public void FloorTo_WithUnitAndInterval_ReturnsContainingBoundary()
    {
        var source = new TimeOnly(10, 7, 31, 500);

        source.FloorTo(TimeUnit.Minute).ShouldBe(new TimeOnly(10, 7));
        source.FloorTo(TimeSpan.FromMinutes(15)).ShouldBe(new TimeOnly(10, 0));
    }

    [Fact]
    public void CeilingTo_WithUnitAndInterval_ReturnsNextBoundaryWhenUnaligned()
    {
        var source = new TimeOnly(10, 7, 31, 500);

        source.CeilingTo(TimeUnit.Minute).ShouldBe(new TimeOnly(10, 8));
        source.CeilingTo(TimeSpan.FromMinutes(15)).ShouldBe(new TimeOnly(10, 15));
    }

    [Fact]
    public void RoundToNearest_WithUnitAndInterval_ReturnsNearestBoundary()
    {
        var source = new TimeOnly(10, 7, 31, 500);

        source.RoundToNearest(TimeUnit.Minute).ShouldBe(new TimeOnly(10, 8));
        source.RoundToNearest(TimeSpan.FromMinutes(15)).ShouldBe(new TimeOnly(10, 15));
    }

    [Fact]
    public void ToIsoTimeString_ReturnsInvariantClockText()
    {
        new TimeOnly(13, 45, 30).ToIsoTimeString().ShouldBe("13:45:30");
    }
}