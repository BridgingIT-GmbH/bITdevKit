// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.DateTime;

using System.Globalization;
using DateTime = System.DateTime;

[UnitTest("Common")]
public class DateTimeRangeExtensionsTests
{
    [Fact]
    public void DateTimeRange_ContainsIncludesStartAndExcludesEnd()
    {
        var range = new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

        range.Contains(new DateTime(2026, 1, 1)).ShouldBeTrue();
        range.Contains(new DateTime(2026, 1, 2)).ShouldBeFalse();
        range.Duration.ShouldBe(TimeSpan.FromDays(1));
    }

    [Fact]
    public void DateTimeRange_OpenBoundaries_ContainAndSortDeterministically()
    {
        var openStart = new DateTimeRange(null, new DateTime(2026, 1, 1));
        var closed = new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));
        var openEnd = new DateTimeRange(new DateTime(2026, 1, 2), null);
        var ranges = new[] { openEnd, closed, openStart };

        Array.Sort(ranges);

        ranges.ShouldBe([openStart, closed, openEnd]);
        openStart.Contains(new DateTime(2025, 12, 31)).ShouldBeTrue();
        openEnd.Contains(new DateTime(2030, 1, 1)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => openEnd.Duration);
        Should.Throw<ArgumentException>(() => new DateTimeRange(null, null));
    }

    [Fact]
    public void DateTimeRange_CompareOperators_OrderByOpenStartThenStartThenOpenEnd()
    {
        var openStart = new DateTimeRange(null, new DateTime(2026, 1, 1));
        var closed = new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));
        var openEnd = new DateTimeRange(new DateTime(2026, 1, 1), null);

        (openStart < closed).ShouldBeTrue();
        (openEnd > closed).ShouldBeTrue();
        (closed <= new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2))).ShouldBeTrue();
        (openEnd >= closed).ShouldBeTrue();
    }

    [Fact]
    public void DateTimeRange_Overlaps_WithAdjacentHalfOpenRanges_ReturnsFalse()
    {
        var first = new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));
        var second = new DateTimeRange(new DateTime(2026, 1, 2), new DateTime(2026, 1, 3));

        first.Overlaps(second).ShouldBeFalse();
    }

    [Fact]
    public void DateTimeRange_Algebra_ReturnsIntersectionUnionGapAndNormalize()
    {
        var first = new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 10));
        var second = new DateTimeRange(new DateTime(2026, 1, 5), new DateTime(2026, 1, 12));
        var disjoint = new DateTimeRange(new DateTime(2026, 1, 20), new DateTime(2026, 1, 25));

        first.Intersection(second).ShouldBe(new DateTimeRange(new DateTime(2026, 1, 5), new DateTime(2026, 1, 10)));
        first.Union(second).ShouldBe(new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 12)));
        first.Gap(disjoint).ShouldBe(new DateTimeRange(new DateTime(2026, 1, 10), new DateTime(2026, 1, 20)));
        first.Contains(new DateTimeRange(new DateTime(2026, 1, 2), new DateTime(2026, 1, 3))).ShouldBeTrue();

        new[] { disjoint, second, first }.Normalize()
            .ShouldBe([
                new DateTimeRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 12)),
                disjoint
            ]);
    }

    [Fact]
    public void DateTimeRange_Algebra_WithOpenBoundaries_UsesInfiniteBounds()
    {
        var openStart = new DateTimeRange(null, new DateTime(2026, 1, 10));
        var openEnd = new DateTimeRange(new DateTime(2026, 1, 5), null);

        openStart.Intersection(openEnd).ShouldBe(new DateTimeRange(new DateTime(2026, 1, 5), new DateTime(2026, 1, 10)));
        openStart.Union(openEnd).ShouldBeNull();
    }

    [Fact]
    public void DateTimeRange_SplitByDay_ClipsToSourceBoundaries()
    {
        var range = new DateTimeRange(
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc));

        range.SplitByDay().ToArray().ShouldBe([
            new DateTimeRange(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            new DateTimeRange(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            new DateTimeRange(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc))
        ]);
    }

    [Fact]
    public void DateTimeRange_SplitByMonth_ClipsToSourceBoundaries()
    {
        var range = new DateTimeRange(
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc));

        range.SplitByMonth().ToArray().ShouldBe([
            new DateTimeRange(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DateTimeRange(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DateTimeRange(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc))
        ]);
    }

    [Fact]
    public void DateTimeRange_SplitByDay_OpenRange_ThrowsInvalidOperationException()
    {
        var range = new DateTimeRange(new DateTime(2026, 1, 1), null);

        Should.Throw<InvalidOperationException>(() => range.SplitByDay().ToArray());
    }

    [Fact]
    public void DateTimeRange_ToDateTimeOffsetRange_UsesTimeZoneRules()
    {
        var timeZone = FindBerlinTimeZone();
        var range = new DateTimeRange(new DateTime(2026, 1, 1, 12, 0, 0), new DateTime(2026, 1, 1, 13, 0, 0));

        var result = range.ToDateTimeOffsetRange(timeZone);

        result.StartInclusive.Value.Offset.ShouldBe(TimeSpan.FromHours(1));
        result.EndExclusive.Value.Offset.ShouldBe(TimeSpan.FromHours(1));
        result.Duration.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void DateTimeRange_ToDateTimeOffsetRange_InvalidBoundary_UsesPolicy()
    {
        var timeZone = FindBerlinTimeZone();
        var range = new DateTimeRange(new DateTime(2026, 3, 29, 2, 30, 0), new DateTime(2026, 3, 29, 4, 0, 0));

        var result = range.ToDateTimeOffsetRange(timeZone, InvalidTimePolicy.MoveForward);

        result.StartInclusive.Value.DateTime.ShouldBe(new DateTime(2026, 3, 29, 3, 0, 0));
    }

    [Fact]
    public void DateTimeRange_ToIsoRangeStringAndTryParse_RoundTripsClosedRange()
    {
        var range = new DateTimeRange(
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));

        var text = range.ToIsoRangeString();
        var success = text.TryParseDateTimeRange(out var result);

        success.ShouldBeTrue();
        result.ShouldBe(range);
    }

    [Fact]
    public void DateTimeRange_TryParse_WithOpenEnd_ReturnsOpenRange()
    {
        var success = "2026-01-01T12:00:00.0000000Z/".TryParseDateTimeRange(out var result);

        success.ShouldBeTrue();
        result.ShouldBe(new DateTimeRange(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), null));
        result.ToIsoRangeString().ShouldBe("2026-01-01T12:00:00.0000000Z/");
    }

    [Fact]
    public void DateTimeOffsetRange_ClosedRange_ReturnsFiniteDuration()
    {
        var range = new DateTimeOffsetRange(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero));

        range.Duration.ShouldBe(TimeSpan.FromHours(6));
        (range < new DateTimeOffsetRange(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), null)).ShouldBeTrue();
    }

    [Fact]
    public void DateTimeOffsetRange_OpenBoundaries_ContainOverlapAndSortDeterministically()
    {
        var openStart = new DateTimeOffsetRange(null, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var closed = new DateTimeOffsetRange(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var openEnd = new DateTimeOffsetRange(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), null);
        var ranges = new[] { openEnd, closed, openStart };

        Array.Sort(ranges);

        ranges.ShouldBe([openStart, closed, openEnd]);
        openStart.Contains(new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.Zero)).ShouldBeTrue();
        openEnd.Overlaps(new DateTimeOffsetRange(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), null)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => openStart.Duration);
        Should.Throw<ArgumentException>(() => new DateTimeOffsetRange(null, null));
    }

    [Fact]
    public void DateTimeOffsetRange_Algebra_MergesAdjacentRanges()
    {
        var first = new DateTimeOffsetRange(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var second = new DateTimeOffsetRange(
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero));

        first.Touches(second).ShouldBeTrue();
        first.Union(second).ShouldBe(new DateTimeOffsetRange(first.StartInclusive, second.EndExclusive));
        new[] { second, first }.Normalize().ShouldBe([new DateTimeOffsetRange(first.StartInclusive, second.EndExclusive)]);
    }

    [Fact]
    public void DateTimeOffsetRange_ToTimeZone_PreservesInstants()
    {
        var timeZone = FindBerlinTimeZone();
        var range = new DateTimeOffsetRange(
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero));

        var result = range.ToTimeZone(timeZone);

        result.StartInclusive.Value.DateTime.ShouldBe(new DateTime(2026, 1, 1, 13, 0, 0));
        result.StartInclusive.Value.UtcDateTime.ShouldBe(range.StartInclusive.Value.UtcDateTime);
    }

    [Fact]
    public void DateTimeOffsetRange_ToIsoRangeStringAndTryParse_RoundTripsOpenStart()
    {
        var range = new DateTimeOffsetRange(null, new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.FromHours(1)));

        var text = range.ToIsoRangeString();
        var success = text.TryParseDateTimeOffsetRange(out var result);

        success.ShouldBeTrue();
        result.ShouldBe(range);
        text.ShouldBe("/2026-01-01T13:00:00.0000000+01:00");
    }

    [Fact]
    public void DateOnlyRange_InvalidRange_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new DateOnlyRange(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void DateOnlyRange_Days_ReturnsHalfOpenDayCount()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        range.Days.ShouldBe(9);
        range.Overlaps(new DateOnlyRange(new DateOnly(2026, 1, 9), new DateOnly(2026, 1, 11))).ShouldBeTrue();
    }

    [Fact]
    public void DateOnlyRange_OpenBoundaries_ContainAndSortDeterministically()
    {
        var openStart = new DateOnlyRange(null, new DateOnly(2026, 1, 1));
        var closed = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));
        var openEnd = new DateOnlyRange(new DateOnly(2026, 1, 2), null);
        var ranges = new[] { openEnd, closed, openStart };

        Array.Sort(ranges);

        ranges.ShouldBe([openStart, closed, openEnd]);
        openStart.Contains(new DateOnly(2025, 12, 31)).ShouldBeTrue();
        openEnd.Contains(new DateOnly(2030, 1, 1)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => openEnd.Days);
        Should.Throw<ArgumentException>(() => new DateOnlyRange(null, null));
    }

    [Fact]
    public void DateOnlyRange_CompareOperators_OrderByOpenStartThenStartThenOpenEnd()
    {
        var openStart = new DateOnlyRange(null, new DateOnly(2026, 1, 1));
        var closed = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));
        var openEnd = new DateOnlyRange(new DateOnly(2026, 1, 1), null);

        (openStart < closed).ShouldBeTrue();
        (openEnd > closed).ShouldBeTrue();
        (closed <= new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2))).ShouldBeTrue();
        (openEnd >= closed).ShouldBeTrue();
    }

    [Fact]
    public void DateOnlyRange_Overlaps_WithAdjacentHalfOpenRanges_ReturnsFalse()
    {
        var first = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));
        var second = new DateOnlyRange(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3));

        first.Overlaps(second).ShouldBeFalse();
    }

    [Fact]
    public void DateOnlyRange_Algebra_ReturnsIntersectionGapAndNormalize()
    {
        var first = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var second = new DateOnlyRange(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 12));
        var disjoint = new DateOnlyRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 1, 25));

        first.Intersection(second).ShouldBe(new DateOnlyRange(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10)));
        first.Gap(disjoint).ShouldBe(new DateOnlyRange(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20)));
        new[] { disjoint, second, first }.Normalize()
            .ShouldBe([
                new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 12)),
                disjoint
            ]);
    }

    [Fact]
    public void DateOnlyRange_EachDay_EnumeratesHalfOpenDates()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 4));

        range.EachDay().ToArray().ShouldBe([
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 3)
        ]);
    }

    [Fact]
    public void DateOnlyRange_SplitByMonth_ClipsToSourceBoundaries()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 2));

        range.SplitByMonth().ToArray().ShouldBe([
            new DateOnlyRange(new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 1)),
            new DateOnlyRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)),
            new DateOnlyRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2))
        ]);
    }

    [Fact]
    public void DateOnlyRange_BusinessDays_UsesCalendar()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7));
        var calendar = new BusinessCalendar(holidays: [new DateOnly(2026, 1, 1)]);

        range.BusinessDays(calendar).ToArray().ShouldBe([
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 6)
        ]);
        range.BusinessDayCount(calendar).ShouldBe(3);
    }

    [Fact]
    public void DateOnlyRange_BusinessDayCount_GlobalCalendarRegistration_ResolvesByCulture()
    {
        var culture = CultureInfo.GetCultureInfo("fr-BE");
        var calendar = new BusinessCalendar(nonWorkingDays: [DayOfWeek.Tuesday]);
        BusinessCalendars.RegisterCountry("BE", calendar);
        var range = new DateOnlyRange(new DateOnly(2026, 6, 29), new DateOnly(2026, 7, 2));

        range.BusinessDays(culture).ToArray().ShouldBe([
            new DateOnly(2026, 6, 29),
            new DateOnly(2026, 7, 1)
        ]);
        range.BusinessDayCount(culture).ShouldBe(2);
    }

    [Fact]
    public void DateOnlyRange_AtStartAndEndOfDay_WithFixedOffset_ReturnsOffsetRange()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3));

        var result = range.AtStartAndEndOfDay(TimeSpan.FromHours(2));

        result.StartInclusive.Value.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)));
        result.EndExclusive.Value.ShouldBe(new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void DateOnlyRange_AtStartAndEndOfDay_WithTimeZone_ReturnsZoneOffsets()
    {
        var timeZone = FindBerlinTimeZone();
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));

        var result = range.AtStartAndEndOfDay(timeZone);

        result.StartInclusive.Value.Offset.ShouldBe(TimeSpan.FromHours(1));
        result.EndExclusive.Value.Offset.ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void DateOnlyRange_ToIsoRangeStringAndTryParse_RoundTripsClosedRange()
    {
        var range = new DateOnlyRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));

        var text = range.ToIsoRangeString();
        var success = text.TryParseDateOnlyRange(out var result);

        success.ShouldBeTrue();
        result.ShouldBe(range);
        text.ShouldBe("2026-01-01/2026-02-01");
    }

    [Fact]
    public void DateOnlyRange_TryParse_FullyOpenRange_ReturnsFalse()
    {
        "/".TryParseDateOnlyRange(out _).ShouldBeFalse();
    }

    [Fact]
    public void DateOnlyRange_EachDay_OpenRange_ThrowsInvalidOperationException()
    {
        var range = new DateOnlyRange(null, new DateOnly(2026, 1, 1));

        Should.Throw<InvalidOperationException>(() => range.EachDay().ToArray());
    }

    [Fact]
    public void TimeOnlyRange_ClosedRange_ReturnsFiniteDuration()
    {
        var range = new TimeOnlyRange(new TimeOnly(12, 0), new TimeOnly(14, 30));

        range.Duration.ShouldBe(TimeSpan.FromMinutes(150));
        (range < new TimeOnlyRange(new TimeOnly(15, 0), null)).ShouldBeTrue();
    }

    [Fact]
    public void TimeOnlyRange_OpenBoundaries_ContainOverlapAndSortDeterministically()
    {
        var openStart = new TimeOnlyRange(null, new TimeOnly(12, 0));
        var closed = new TimeOnlyRange(new TimeOnly(12, 0), new TimeOnly(13, 0));
        var openEnd = new TimeOnlyRange(new TimeOnly(13, 0), null);
        var ranges = new[] { openEnd, closed, openStart };

        Array.Sort(ranges);

        ranges.ShouldBe([openStart, closed, openEnd]);
        openStart.Contains(new TimeOnly(11, 59)).ShouldBeTrue();
        openEnd.Overlaps(new TimeOnlyRange(new TimeOnly(14, 0), null)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => openEnd.Duration);
        Should.Throw<ArgumentException>(() => new TimeOnlyRange(null, null));
    }

    [Fact]
    public void TimeOnlyRange_Algebra_ReturnsIntersectionUnionAndGap()
    {
        var first = new TimeOnlyRange(new TimeOnly(9, 0), new TimeOnly(12, 0));
        var second = new TimeOnlyRange(new TimeOnly(11, 0), new TimeOnly(13, 0));
        var disjoint = new TimeOnlyRange(new TimeOnly(14, 0), new TimeOnly(15, 0));

        first.Intersection(second).ShouldBe(new TimeOnlyRange(new TimeOnly(11, 0), new TimeOnly(12, 0)));
        first.Union(second).ShouldBe(new TimeOnlyRange(new TimeOnly(9, 0), new TimeOnly(13, 0)));
        first.Gap(disjoint).ShouldBe(new TimeOnlyRange(new TimeOnly(12, 0), new TimeOnly(14, 0)));
    }

    [Fact]
    public void TimeOnlyRange_ToIsoRangeStringAndTryParse_RoundTripsClosedRange()
    {
        var range = new TimeOnlyRange(new TimeOnly(9, 0), new TimeOnly(17, 30));

        var text = range.ToIsoRangeString();
        var success = text.TryParseTimeOnlyRange(out var result);

        success.ShouldBeTrue();
        result.ShouldBe(range);
        text.ShouldBe("09:00:00/17:30:00");
    }

    [Fact]
    public void TimeOnlyRange_TryParse_InvalidRange_ReturnsFalse()
    {
        "17:00:00/09:00:00".TryParseTimeOnlyRange(out _).ShouldBeFalse();
    }

    private static TimeZoneInfo FindBerlinTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
