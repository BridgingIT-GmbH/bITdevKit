// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

public class DateOnlyExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void StartOfDay_GivenDateOnly_ReturnsSameDate()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfDay();

        // Assert
        result.ShouldBe(date); // DateOnly has no time component
    }

    [Fact]
    public void EndOfDay_GivenDateOnly_ReturnsSameDate()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.EndOfDay();

        // Assert
        result.ShouldBe(date); // DateOnly has no time component
    }

    [Fact]
    public void StartOfWeek_GivenDateOnly_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.StartOfWeek(); // Default is Monday

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 11)); // Should be Monday
        result.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Saturday)]
    public void StartOfWeek_WithCustomFirstDay_ReturnsCorrectStartOfWeek(DayOfWeek firstDayOfWeek)
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.StartOfWeek(firstDayOfWeek);

        // Assert
        result.DayOfWeek.ShouldBe(firstDayOfWeek);
        result.ShouldBeLessThanOrEqualTo(date);
    }

    [Fact]
    public void EndOfWeek_GivenDateOnly_ReturnsEndOfWeek()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15); // Friday

        // Act
        var result = date.EndOfWeek();

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 17)); // Should be Sunday
        result.DayOfWeek.ShouldBe(DayOfWeek.Sunday);
    }

    [Fact]
    public void StartOfMonth_GivenDateOnly_ReturnsStartOfMonth()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfMonth();

        // Assert
        result.ShouldBe(new DateOnly(2024, 3, 1));
    }

    [Theory]
    [InlineData(2024, 2, 15, 29)] // Leap year February
    [InlineData(2023, 2, 15, 28)] // Non-leap year February
    [InlineData(2024, 3, 15, 31)] // 31-day month
    [InlineData(2024, 4, 15, 30)] // 30-day month
    public void EndOfMonth_GivenDateOnly_ReturnsEndOfMonth(int year, int month, int day, int expectedLastDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var result = date.EndOfMonth();

        // Assert
        result.ShouldBe(new DateOnly(year, month, expectedLastDay));
    }

    [Fact]
    public void StartOfYear_GivenDateOnly_ReturnsStartOfYear()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.StartOfYear();

        // Assert
        result.ShouldBe(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void EndOfYear_GivenDateOnly_ReturnsEndOfYear()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.EndOfYear();

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    [Theory]
    [InlineData(2024, 3, 15, DateUnit.Day, 5, 2024, 3, 20)]
    [InlineData(2024, 3, 15, DateUnit.Week, 1, 2024, 3, 22)]
    [InlineData(2024, 3, 15, DateUnit.Month, 1, 2024, 4, 15)]
    [InlineData(2024, 3, 15, DateUnit.Year, 1, 2025, 3, 15)]
    [InlineData(2024, 3, 31, DateUnit.Month, 1, 2024, 4, 30)] // End of month case
    public void Add_WithDifferentUnits_AddsCorrectly(
        int year, int month, int day,
        DateUnit unit, int amount,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var expected = new DateOnly(expectedYear, expectedMonth, expectedDay);

        // Act
        var result = date.Add(unit, amount);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2024, 3, 15, 2024, 3, 1, 2024, 3, 31, true, true)]  // Within range inclusive
    [InlineData(2024, 3, 15, 2024, 3, 15, 2024, 3, 31, true, true)] // At start inclusive
    [InlineData(2024, 3, 31, 2024, 3, 1, 2024, 3, 31, true, true)]  // At end inclusive
    [InlineData(2024, 3, 15, 2024, 3, 1, 2024, 3, 31, false, true)] // Within range exclusive
    [InlineData(2024, 3, 1, 2024, 3, 1, 2024, 3, 31, false, false)] // At start exclusive
    public void IsInRange_WithVariousScenarios_ReturnsExpectedResult(
        int year, int month, int day,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay,
        bool inclusive, bool expected)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var start = new DateOnly(startYear, startMonth, startDay);
        var end = new DateOnly(endYear, endMonth, endDay);

        // Act
        var result = date.IsInRange(start, end, inclusive);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsInRelativeRange_InFutureRange_ReturnsTrue()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var future = today.AddDays(5);

        // Act & Assert
        future.IsInRelativeRange(DateUnit.Day, 10, DateTimeDirection.Future, true).ShouldBeTrue();
        future.IsInRelativeRange(DateUnit.Day, 3, DateTimeDirection.Future, true).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2024, 1, 1, 1)]  // First week of year
    [InlineData(2024, 12, 31, 53)] // Last week of year
    public void GetWeekOfYear_ReturnsCorrectWeek(int year, int month, int day, int expectedWeek)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var result = date.GetWeekOfYear();

        // Assert
        result.ShouldBe(expectedWeek);
    }

    [Theory]
    [InlineData(2024, true)]  // Leap year
    [InlineData(2023, false)] // Non-leap year
    public void IsLeapYear_ReturnsCorrectResult(int year, bool expected)
    {
        // Arrange
        var date = new DateOnly(year, 1, 1);

        // Act
        var result = date.IsLeapYear();

        // Assert
        result.ShouldBe(expected);
    }

    // does not work on devops CI
    // [Fact]
    // public void ToUnixTimeSeconds_ReturnsCorrectTimestamp()
    // {
    //     // Arrange
    //     var date = new DateOnly(2024, 1, 1);
    //     var expected = 1704063600L; // 2024-01-01 00:00:00 UTC
    //
    //     // Act
    //     var result = date.ToUnixTimeSeconds();
    //
    //     // Assert
    //     result.ShouldBe(expected);
    // }

    [Fact]
    public void ToDateTimeOffset_WithOffset_ReturnsCorrectValue()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);
        var offset = TimeSpan.FromHours(2);

        // Act
        var result = date.ToDateTimeOffset(offset);

        // Assert
        result.Date.ShouldBe(date.ToDateTime(TimeOnly.MinValue).Date);
        result.Offset.ShouldBe(offset);
    }

    [Fact]
    public void TimeSpanTo_ReturnsCorrectDuration()
    {
        // Arrange
        var start = new DateOnly(2024, 3, 15);
        var end = new DateOnly(2024, 3, 20);

        // Act
        var result = start.TimeSpanTo(end);

        // Assert
        result.ShouldBe(TimeSpan.FromDays(5));
    }

    [Theory]
    [InlineData(DateUnit.Day)]
    [InlineData(DateUnit.Week)]
    [InlineData(DateUnit.Month)]
    [InlineData(DateUnit.Year)]
    public void RoundToNearest_WithDateUnit_ReturnsCorrectDate(DateUnit unit)
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);

        // Act
        var result = date.RoundToNearest(unit);

        // Assert
        switch (unit)
        {
            case DateUnit.Day:
                result.ShouldBe(date); // DateOnly is already at day level
                break;
            case DateUnit.Week:
                result.ShouldBe(new DateOnly(2024, 3, 11)); // Monday
                break;
            case DateUnit.Month:
                result.ShouldBe(new DateOnly(2024, 3, 1));
                break;
            case DateUnit.Year:
                result.ShouldBe(new DateOnly(2024, 1, 1));
                break;
        }
    }

    [Fact]
    public void RoundToNearest_UnsupportedDateUnit_ThrowsArgumentException()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 15);
        const int invalidUnit = 999;

        // Act & Assert
        Should.Throw<ArgumentException>(() => date.RoundToNearest((DateUnit)invalidUnit))
            .Message.ShouldContain("Unsupported DateUnit");
    }
}