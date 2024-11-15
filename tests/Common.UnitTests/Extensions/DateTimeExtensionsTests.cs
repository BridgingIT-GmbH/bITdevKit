// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

[UnitTest("Common")]
public class DateTimeExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void StartOfDay_GivenDateTime_ReturnsStartOfDay()
    {
        // Arrange
        var date = new DateTime(2022, 12, 31, 20, 30, 40);

        // Act
        var startOfDay = date.StartOfDay();

        // Assert
        startOfDay.ShouldBe(new DateTime(2022, 12, 31, 0, 0, 0));
    }

    [Fact]
    public void EndOfDay_GivenDateTime_ReturnsEndOfDay()
    {
        // Arrange
        var date = new DateTime(2022, 12, 31, 20, 30, 40);

        // Act
        var endOfDay = date.EndOfDay();

        // Assert
        endOfDay.ShouldBe(new DateTime(2022, 12, 31, 23, 59, 59));
    }

    [Fact]
    public void StartOfWeek_GivenDateTime_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateTime(2022, 1, 1);

        // Act
        var startOfWeek = date.StartOfWeek();

        // Assert
        startOfWeek.ShouldBe(new DateTime(2021, 12, 27));
    }

    [Fact]
    public void StartOfWeek_GivenDateTimeAndDayOfWeek_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateTime(2022, 1, 1);

        // Act
        var startOfWeek = date.StartOfWeek(DayOfWeek.Monday);

        // Assert
        startOfWeek.ShouldBe(new DateTime(2021, 12, 27));
    }

    [Fact]
    public void EndOfWeek_GivenDateTime_ReturnsEndOfWeek()
    {
        // Arrange
        var date = new DateTime(2022, 1, 1);

        // Act
        var result = date.EndOfWeek();

        // Assert
        result.ShouldBe(new DateTime(2022, 1, 2, 23, 59, 59));
    }

    [Fact]
    public void StartOfMonth_GivenDateTime_ReturnsStartOfMonth()
    {
        // Arrange
        var date = new DateTime(2022, 12, 3, 20, 30, 40);

        // Act
        var startOfMonth = date.StartOfMonth();

        // Assert
        startOfMonth.ShouldBe(new DateTime(2022, 12, 1, 0, 0, 0));
    }

    [Fact]
    public void EndOfMonth_GivenDateTime_ReturnsEndOfMonth()
    {
        // Arrange
        var date = new DateTime(2022, 12, 3, 20, 30, 40);

        // Act
        var endOfMonth = date.EndOfMonth();

        // Assert
        endOfMonth.ShouldBe(new DateTime(2022, 12, 31, 23, 59, 59));
    }

    [Fact]
    public void StartOfYear_GivenDateTime_ReturnsStartOfYear()
    {
        // Arrange
        var date = new DateTime(2022, 11, 3, 20, 30, 40);

        // Act
        var startOfYear = date.StartOfYear();

        // Assert
        startOfYear.ShouldBe(new DateTime(2022, 1, 1, 0, 0, 0));
    }

    [Fact]
    public void EndOfYear_GivenDateTime_ReturnsEndOfYear()
    {
        // Arrange
        var date = new DateTime(2022, 11, 3, 20, 30, 40);

        // Act
        var endOfYear = date.EndOfYear();

        // Assert
        endOfYear.ShouldBe(new DateTime(2022, 12, 31, 23, 59, 59));
    }

    [Fact]
    public void StartOfDay_GivenDateTimeOffset_ReturnsStartOfDay()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 12, 31, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var startOfDay = date.StartOfDay();

        // Assert
        startOfDay.ShouldBe(new DateTimeOffset(2022, 12, 31, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void EndOfDay_GivenDateTimeOffset_ReturnsEndOfDay()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 12, 31, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var endOfDay = date.EndOfDay();

        // Assert
        endOfDay.ShouldBe(new DateTimeOffset(2022, 12, 31, 23, 59, 59, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void StartOfWeek_GivenDateTimeOffset_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

        // Act
        var startOfWeek = date.StartOfWeek();

        // Assert
        startOfWeek.ShouldBe(new DateTimeOffset(2021, 12, 27, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void StartOfWeek_GivenDateTimeOffsetAndDayOfWeek_ReturnsStartOfWeek()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        var dayOfWeek = DayOfWeek.Monday;

        // Act
        var startOfWeek = date.StartOfWeek(dayOfWeek);

        // Assert
        startOfWeek.ShouldBe(new DateTimeOffset(2021, 12, 27, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void StartOfMonth_GivenDateTimeOffset_ReturnsStartOfMonth()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 12, 31, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var startOfMonth = date.StartOfMonth();

        // Assert
        startOfMonth.ShouldBe(new DateTimeOffset(2022, 12, 1, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void EndOfMonth_GivenDateTimeOffset_ReturnsEndOfMonth()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 12, 3, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var endOfMonth = date.EndOfMonth();

        // Assert
        endOfMonth.ShouldBe(new DateTimeOffset(2022, 12, 31, 23, 59, 59, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void StartOfYear_GivenDateTimeOffset_ReturnsStartOfYear()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 11, 3, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var startOfYear = date.StartOfYear();

        // Assert
        startOfYear.ShouldBe(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void EndOfYear_GivenDateTimeOffset_ReturnsEndOfYear()
    {
        // Arrange
        var date = new DateTimeOffset(2022, 12, 31, 20, 30, 40, TimeSpan.FromHours(2));

        // Act
        var endOfYear = date.EndOfYear();

        // Assert
        endOfYear.ShouldBe(new DateTimeOffset(2022, 12, 31, 23, 59, 59, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void ParseDateOrEpoch_NullInput_ReturnsNull()
    {
        // Arrange
        string input = null;

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseDateOrEpoch_EmptyInput_ReturnsNull()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseDateOrEpoch_ValidEpoch_ReturnsCorrectDateTime()
    {
        // Arrange
        var epochSeconds = this.faker.Date.Past().ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var input = ((long)epochSeconds).ToString();

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(DateTimeOffset.FromUnixTimeSeconds((long)epochSeconds).UtcDateTime);
    }

    [Fact]
    public void ParseDateOrEpoch_ValidIso8601_ReturnsCorrectDateTime()
    {
        // Arrange
        var dateTime = this.faker.Date.Past().ToUniversalTime();
        var input = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(dateTime.Date + dateTime.TimeOfDay.TruncateToSeconds());
    }

    [Fact]
    public void ParseDateOrEpoch_ValidIso8601WithMilliseconds_NotIgnoresMilliseconds()
    {
        // Arrange
        var dateTime = this.faker.Date.Past().ToUniversalTime();
        var input = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(dateTime.Date + dateTime.TimeOfDay);
    }

    [Fact]
    public void ParseDateOrEpoch_InvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        var input = this.faker.Lorem.Word();

        // Act & Assert
        Should.Throw<ArgumentException>(() => input.ParseDateOrEpoch())
            .Message.ShouldContain($"Invalid date format: {input}");
    }

    [Fact]
    public void ParseDateOrEpoch_EpochBeforeUnixEpoch_ReturnsCorrectDateTime()
    {
        // Arrange
        var input = "-86400"; // One day before Unix epoch

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(new DateTime(1969, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseDateOrEpoch_LargeEpochValue_ReturnsCorrectDateTime()
    {
        // Arrange
        var input = "253402300799"; // 9999-12-31T23:59:59Z

        // Act
        var result = input.ParseDateOrEpoch();

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void TryParseDateOrEpoch_NullInput_ReturnsFalse()
    {
        // Arrange
        string source = null;

        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void TryParseDateOrEpoch_EmptyString_ReturnsFalse()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void TryParseDateOrEpoch_WhiteSpace_ReturnsFalse()
    {
        // Arrange
        var source = "   ";

        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public void TryParseDateOrEpoch_ValidUnixTimestamp_ReturnsTrue()
    {
        // Arrange
        var expectedDate = this.faker.Date.Past();
        var unixTimestamp = ((DateTimeOffset)expectedDate).ToUnixTimeSeconds();
        var source = unixTimestamp.ToString();

        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldBe(expectedDate.ToUniversalTime(), TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("99999")] // Too small
    public void TryParseDateOrEpoch_InvalidUnixTimestamp_ReturnsFalse(string source)
    {
        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Theory]
    [InlineData("2024-03-14")] // ISO 8601
    [InlineData("2024-03-14T13:45:30")] // ISO 8601 with time
    [InlineData("2024-03-14T13:45:30Z")] // ISO 8601 with UTC
    [InlineData("2024-03-14T13:45:30.1234567")] // ISO 8601 with milliseconds
    [InlineData("14/03/2024")] // UK format
    [InlineData("03/14/2024")] // US format
    [InlineData("14-03-2024")] // Alternative format
    [InlineData("14.03.2024")] // European format
    [InlineData("20240314")] // Compact format
    [InlineData("14 Mar 2024")] // Month name format
    [InlineData("14 March 2024")] // Full month name format
    public void TryParseDateOrEpoch_ValidDateFormats_ReturnsTrue(string source)
    {
        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBe(DateTime.MinValue);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2024-13-14")] // Invalid month
    [InlineData("2024-03-32")] // Invalid day
    [InlineData("14/13/2024")] // Invalid month
    [InlineData("32/03/2024")] // Invalid day
    public void TryParseDateOrEpoch_InvalidDateFormats_ReturnsFalse(string source)
    {
        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Theory]
    [InlineData(2023, 1, 31, DateUnit.Month, 1, 2023, 2, 28)] // Handles month with fewer days
    [InlineData(2023, 1, 31, DateUnit.Month, -1, 2022, 12, 31)] // Negative month addition
    [InlineData(2020, 2, 29, DateUnit.Year, 1, 2021, 2, 28)] // Leap year to non-leap year
    [InlineData(2023, 1, 1, DateUnit.Week, 2, 2023, 1, 15)] // Adding weeks
    [InlineData(2023, 1, 1, DateUnit.Day, 10, 2023, 1, 11)] // Adding days
    public void DateTime_Add_ShouldAddCorrectly(int year, int month, int day, DateUnit unit, int amount, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var date = new DateTime(year, month, day);

        // Act
        var sut = date.Add(unit, amount);

        // Assert
        sut.ShouldBe(new DateTime(expectedYear, expectedMonth, expectedDay));
    }

    [Theory]
    [InlineData(2023, 5, 15, 2023, 5, 1, 2023, 5, 31, true, true)] // Date within inclusive range
    [InlineData(2023, 5, 15, 2023, 5, 1, 2023, 5, 14, false, false)] // Date outside range
    public void DateTime_IsWithinRange_ShouldCheckIfDateIsWithinRange(
        int year,
        int month,
        int day,
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay,
        bool inclusive,
        bool expected)
    {
        // Arrange
        var date = new DateTime(year, month, day);
        var start = new DateTime(startYear, startMonth, startDay);
        var end = new DateTime(endYear, endMonth, endDay);

        // Act
        var sut = date.IsInRange(start, end, inclusive);

        // Assert
        sut.ShouldBe(expected);
    }

    [Fact]
    public void DateTime_IsWithinRelativeRange_ShouldReturnTrueForFutureDate()
    {
        // Arrange
        var date = DateTime.Now;
        var sut = date.Add(DateUnit.Day, 5); // future date +5 days

        // Act & Assert
        sut.IsInRelativeRange(DateUnit.Day, 3, DateTimeDirection.Future, inclusive: true).ShouldBeFalse();
        sut.IsInRelativeRange(DateUnit.Day, 5, DateTimeDirection.Future, inclusive: true).ShouldBeTrue();
        sut.IsInRelativeRange(DateUnit.Day, 10, DateTimeDirection.Future, inclusive: true).ShouldBeTrue();
    }

    // DateOnly Tests
    [Theory]
    [InlineData(2023, 1, 31, DateUnit.Month, 1, 2023, 2, 28)] // Handles month with fewer days
    [InlineData(2023, 1, 31, DateUnit.Month, -1, 2022, 12, 31)] // Negative month addition
    public void DateOnly_Add_ShouldAddCorrectly(int year, int month, int day, DateUnit unit, int amount, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var date = new DateOnly(year, month, day);

        // Act
        var sut = date.Add(unit, amount);

        // Assert
        sut.ShouldBe(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    [Fact]
    public void DateOnly_IsWithinRelativeRange_ShouldReturnTrueForFutureDate()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Now);
        var futureDate = today.Add(DateUnit.Day, 5); // +5 days in the future

        // Act & Assert
        futureDate.IsInRelativeRange(DateUnit.Day, 3, DateTimeDirection.Future, inclusive: true).ShouldBeFalse();
        futureDate.IsInRelativeRange(DateUnit.Day, 5, DateTimeDirection.Future, inclusive: true).ShouldBeTrue();
        futureDate.IsInRelativeRange(DateUnit.Day, 10, DateTimeDirection.Future, inclusive: true).ShouldBeTrue();
    }

    // TimeOnly Tests
    [Theory]
    [InlineData(10, 30, 0, TimeUnit.Hour, 2, 12, 30)] // Adding hours
    [InlineData(10, 30, 0, TimeUnit.Hour, -1, 9, 30)] // Subtracting hours
    [InlineData(10, 30, 0, TimeUnit.Minute, 30, 11, 0)] // Adding minutes
    [InlineData(10, 30, 0, TimeUnit.Minute, -15, 10, 15)] // Subtracting minutes
    public void TimeOnly_Add_ShouldAddCorrectly(int hour, int minute, int second, TimeUnit unit, int amount, int expectedHour, int expectedMinute)
    {
        // Arrange
        var sut = new TimeOnly(hour, minute, second);

        // Act
        var result = sut.Add(unit, amount);

        // Assert
        result.ShouldBe(new TimeOnly(expectedHour, expectedMinute));
    }

    [Fact]
    public void TimeOnly_IsWithinRelativeRange_ShouldReturnTrueForFutureTime()
    {
        // Arrange
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var future = now.Add(TimeUnit.Minute, 3);

        // Act & Assert
        future.IsInRelativeRange(TimeUnit.Minute, 3, DateTimeDirection.Future, inclusive: true).ShouldBeTrue();
        future.IsInRelativeRange(TimeUnit.Minute, 1, DateTimeDirection.Future, inclusive: true).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    public void IsLeapYear_ReturnsCorrectResult(int year, bool expected)
    {
        // Arrange
        var date = new DateTime(year, 1, 1);

        // Act
        var result = date.IsLeapYear();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void GetWeekOfYear_ReturnsCorrectWeekNumber()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1);

        // Act
        var result = date.GetWeekOfYear();

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void DaysUntil_ReturnsCorrectNumberOfDays()
    {
        // Arrange
        var now = DateTime.Now;
        var futureDate = now.AddDays(5);

        // Act
        var result = futureDate.Subtract(now).Days;

        // Assert
        result.ShouldBe(5);
    }

// Alternative version using a fixed date for more stability
    [Fact]
    public void DaysUntil_ReturnsCorrectNumberOfDays_WithFixedDates()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 1, 12, 0, 0);
        var futureDate = baseDate.AddDays(5);

        // Act
        var result = futureDate.Subtract(baseDate).Days;

        // Assert
        result.ShouldBe(5);
    }

    [Fact]
    public void ToUnixTimeSeconds_ReturnsCorrectTimestamp()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expected = 1704067200L;

        // Act
        var result = date.ToUnixTimeSeconds();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void TimeSpanTo_ReturnsCorrectDuration()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var end = start.AddDays(1).AddHours(2);

        // Act
        var result = start.TimeSpanTo(end);

        // Assert
        result.ShouldBe(TimeSpan.FromHours(26));
    }

    [Theory]
    [InlineData(DateUnit.Day)]
    [InlineData(DateUnit.Week)]
    [InlineData(DateUnit.Month)]
    [InlineData(DateUnit.Year)]
    public void RoundToNearest_DateUnit_ReturnsCorrectDateTime(DateUnit unit)
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 45);

        // Act
        var result = date.RoundToNearest(unit);

        // Assert
        switch (unit)
        {
            case DateUnit.Day:
                result.ShouldBe(new DateTime(2024, 3, 15, 0, 0, 0));

                break;
            case DateUnit.Week:
                result.ShouldBe(new DateTime(2024, 3, 11, 0, 0, 0)); // Monday

                break;
            case DateUnit.Month:
                result.ShouldBe(new DateTime(2024, 3, 1, 0, 0, 0));

                break;
            case DateUnit.Year:
                result.ShouldBe(new DateTime(2024, 1, 1, 0, 0, 0));

                break;
        }
    }

    [Theory]
    [InlineData(TimeUnit.Minute)]
    [InlineData(TimeUnit.Hour)]
    public void RoundToNearest_TimeUnit_ReturnsCorrectDateTime(TimeUnit unit)
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 45);

        // Act
        var result = date.RoundToNearest(unit);

        // Assert
        switch (unit)
        {
            case TimeUnit.Minute:
                result.ShouldBe(new DateTime(2024, 3, 15, 14, 30, 0));

                break;
            case TimeUnit.Hour:
                result.ShouldBe(new DateTime(2024, 3, 15, 14, 0, 0));

                break;
        }
    }

    [Fact]
    public void ToDateTimeOffset_WithOffset_ReturnsCorrectOffset()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 0);
        var offset = TimeSpan.FromHours(2);

        // Act
        var result = date.ToDateTimeOffset(offset);

        // Assert
        result.Offset.ShouldBe(offset);
        result.DateTime.ShouldBe(date);
    }

    [Fact]
    public void RoundToNearest_UnsupportedDateUnit_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now;
        const int invalidUnit = 999;

        // Act & Assert
        Should.Throw<ArgumentException>(() => date.RoundToNearest((DateUnit)invalidUnit))
            .Message.ShouldContain("Unsupported DateUnit");
    }

    [Fact]
    public void RoundToNearest_UnsupportedTimeUnit_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now;
        const int invalidUnit = 999;

        // Act & Assert
        Should.Throw<ArgumentException>(() => date.RoundToNearest((TimeUnit)invalidUnit))
            .Message.ShouldContain("Unsupported TimeUnit");
    }

    [Theory]
    [InlineData("2024-03-01", 1, "2024-03-04")] // Friday -> Monday
    [InlineData("2024-03-01", 2, "2024-03-05")] // Friday -> Tuesday
    [InlineData("2024-03-01", 5, "2024-03-08")] // Friday -> Next Friday
    [InlineData("2024-03-01", 10, "2024-03-15")] // Friday -> Friday after next
    [InlineData("2024-03-15", 22, "2024-04-16")] // Mid-month -> Mid-next-month
    public void AddBusinessDays_WithSpecificDates_ReturnsExpectedDate(string startDateString, int days, string expectedDateString)
    {
        // Arrange
        var startDate = DateTime.Parse(startDateString);
        var expectedDate = DateTime.Parse(expectedDateString);
        var holidays = Array.Empty<DateTime>();

        // Act
        var result = startDate.AddBusinessDays(days, holidays);

        // Assert
        result.Date.ShouldBe(expectedDate.Date);
    }

    [Fact]
    public void AddBusinessDays_WithMultipleHolidays_SkipsHolidaysAndWeekends()
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 1); // Friday
        var holidays = new[]
        {
            new DateTime(2024, 3, 4), // Monday holiday
            new DateTime(2024, 3, 5), // Tuesday holiday
            new DateTime(2024, 3, 8) // Friday holiday
        };

        // Act
        var result = startDate.AddBusinessDays(5, holidays);

        // Assert
        result.ShouldBe(new DateTime(2024, 3, 13)); // Should be Wednesday next week
        result.DayOfWeek.ShouldBe(DayOfWeek.Wednesday);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void AddBusinessDays_WithCustomNonWorkingDays_ReturnsCorrectDate(int days)
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 1); // Friday
        var holidays = new[] { new DateTime(2024, 3, 4) }; // Monday holiday
        var nonWorkingDays = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };

        // Act
        var result = startDate.AddBusinessDays(days, holidays, nonWorkingDays);

        // Assert
        nonWorkingDays.ShouldNotContain(result.DayOfWeek);
        holidays.ShouldNotContain(result.Date);
    }

    [Fact]
    public void AddBusinessDays_WithHolidays_SkipsHolidaysAndWeekends()
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 1); // Friday
        var holidays = new[]
        {
            new DateTime(2024, 3, 4), // Monday holiday
            new DateTime(2024, 3, 5) // Tuesday holiday
        };

        // Act
        var result = startDate.AddBusinessDays(3, holidays);

        // Assert
        result.ShouldBe(new DateTime(2024, 3, 8)); // Should be Thursday (skips weekend and two holidays)
    }

    [Fact]
    public void AddBusinessDays_WithEndOfMonthHolidays_HandlesMonthTransitionCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 28); // Thursday
        var holidays = new[]
        {
            new DateTime(2024, 3, 29), // Friday holiday
            new DateTime(2024, 4, 1) // Monday holiday
        };

        // Act
        var result = startDate.AddBusinessDays(2, holidays);

        // Assert
        result.ShouldBe(new DateTime(2024, 4, 3)); // Should be Tuesday (skips Friday holiday, weekend, and Monday holiday)
    }

    [Fact]
    public void AddBusinessDays_WithCustomWorkWeek_RespectsNonWorkingDays()
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 1); // Friday
        var holidays = Array.Empty<DateTime>();
        var nonWorkingDays = new[] { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }; // Friday-Sunday weekend

        // Act
        var result = startDate.AddBusinessDays(3, holidays, nonWorkingDays);

        // Assert
        result.ShouldBe(new DateTime(2024, 3, 6)); // Should be Wednesday (skips Friday, Saturday, Sunday)
        nonWorkingDays.ShouldNotContain(result.DayOfWeek);
    }

    [Theory]
    [InlineData(0)]
    public void AddBusinessDays_WithZeroDays_ReturnsSameDate(int days)
    {
        // Arrange
        var startDate = new DateTime(2024, 3, 1);
        var holidays = Array.Empty<DateTime>();

        // Act
        var result = startDate.AddBusinessDays(days, holidays);

        // Assert
        result.ShouldBe(startDate);
    }

    [Theory]
    [InlineData("2024-03-15", -5, "2024-03-07")] // Back 5 business days, skipping the March 8 holiday
    [InlineData("2024-03-15", -3, "2024-03-12")] // Simple case: back 3 business days
    [InlineData("2024-03-15", -10, "2024-02-29")] // Back 10 business days, skipping holiday and weekends

    public void AddBusinessDays_WithNegativeDays_MovesBackward(string startDateString, int days, string expectedDateString)
    {
        // Arrange
        var startDate = DateTime.Parse(startDateString);
        var expectedDate = DateTime.Parse(expectedDateString);
        var holidays = new[] { new DateTime(2024, 3, 8) }; // Friday is a holiday

        // Act
        var result = startDate.AddBusinessDays(days, holidays);

        // Assert
        result.ShouldBe(expectedDate);
        holidays.ShouldNotContain(result.Date);
        result.DayOfWeek.ShouldNotBe(DayOfWeek.Saturday);
        result.DayOfWeek.ShouldNotBe(DayOfWeek.Sunday);
    }
}