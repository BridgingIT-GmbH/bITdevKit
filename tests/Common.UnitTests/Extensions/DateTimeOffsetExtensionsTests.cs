// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

public class DateTimeOffsetExtensionsTests
{
    private readonly Faker faker = new();

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45")]  // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45")] // UTC-5
    [InlineData(0, "2024-03-15 14:30:45")]  // UTC
    public void StartOfDay_GivenDateTimeOffset_ReturnsStartOfDay(int offsetHours, string dateTimeString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var result = date.StartOfDay();

        // Assert
        result.ShouldBe(new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, 0, offset));
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45")]  // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45")] // UTC-5
    [InlineData(0, "2024-03-15 14:30:45")]  // UTC
    public void EndOfDay_GivenDateTimeOffset_ReturnsEndOfDay(int offsetHours, string dateTimeString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var result = date.EndOfDay();

        // Assert
        result.ShouldBe(new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, 23, 59, 59, offset));
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData("2024-03-15 14:30:45", 2, DayOfWeek.Monday, "2024-03-11 00:00:00")]    // Friday -> Monday
    [InlineData("2024-03-15 14:30:45", -5, DayOfWeek.Sunday, "2024-03-10 00:00:00")]   // Friday -> Sunday
    [InlineData("2024-03-15 14:30:45", 0, DayOfWeek.Saturday, "2024-03-09 00:00:00")]  // Friday -> Saturday
    public void StartOfWeek_WithCustomFirstDay_ReturnsCorrectStartOfWeek(
        string dateTimeString, 
        int offsetHours, 
        DayOfWeek firstDayOfWeek, 
        string expectedDateString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);
        var expectedDateTime = DateTime.Parse(expectedDateString);
        var expected = new DateTimeOffset(expectedDateTime, offset);

        // Act
        var result = date.StartOfWeek(firstDayOfWeek);

        // Assert
        result.ShouldBe(expected);
        result.Offset.ShouldBe(offset);
        result.DayOfWeek.ShouldBe(firstDayOfWeek);
    }

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45", "2024-03-17 23:59:59")]   // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45", "2024-03-17 23:59:59")]  // UTC-5
    [InlineData(0, "2024-03-15 14:30:45", "2024-03-17 23:59:59")]   // UTC
    public void EndOfWeek_GivenDateTimeOffset_ReturnsEndOfWeek(int offsetHours, string dateTimeString, string expectedDateString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);
        var expectedDateTime = DateTime.Parse(expectedDateString);
        var expected = new DateTimeOffset(expectedDateTime, offset);

        // Act
        var result = date.EndOfWeek();

        // Assert
        result.ShouldBe(expected);
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45")]  // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45")] // UTC-5
    [InlineData(0, "2024-03-15 14:30:45")]  // UTC
    public void StartOfMonth_GivenDateTimeOffset_ReturnsStartOfMonth(int offsetHours, string dateTimeString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var result = date.StartOfMonth();

        // Assert
        result.ShouldBe(new DateTimeOffset(dateTime.Year, dateTime.Month, 1, 0, 0, 0, 0, offset));
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData(2, 2024, 2, 15, 29)]  // Leap year February UTC+2
    [InlineData(-5, 2023, 2, 15, 28)] // Non-leap year February UTC-5
    [InlineData(0, 2024, 3, 15, 31)]  // 31-day month UTC
    [InlineData(2, 2024, 4, 15, 30)]  // 30-day month UTC+2
    public void EndOfMonth_GivenDateTimeOffset_ReturnsEndOfMonth(
        int offsetHours, 
        int year, 
        int month, 
        int day, 
        int expectedLastDay)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var date = new DateTimeOffset(year, month, day, 14, 30, 45, offset);

        // Act
        var result = date.EndOfMonth();

        // Assert
        result.ShouldBe(new DateTimeOffset(year, month, expectedLastDay, 23, 59, 59, offset));
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45")]  // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45")] // UTC-5
    [InlineData(0, "2024-03-15 14:30:45")]  // UTC
    public void StartOfYear_GivenDateTimeOffset_ReturnsStartOfYear(int offsetHours, string dateTimeString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var result = date.StartOfYear();

        // Assert
        result.ShouldBe(new DateTimeOffset(dateTime.Year, 1, 1, 0, 0, 0, 0, offset));
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData(2, "2024-03-15 14:30:45")]  // UTC+2
    [InlineData(-5, "2024-03-15 14:30:45")] // UTC-5
    [InlineData(0, "2024-03-15 14:30:45")]  // UTC
    public void EndOfYear_GivenDateTimeOffset_ReturnsEndOfYear(int offsetHours, string dateTimeString)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var result = date.EndOfYear();

        // Assert
        result.ShouldBe(new DateTimeOffset(dateTime.Year, 12, 31, 23, 59, 59, offset));
        result.Offset.ShouldBe(offset);
    }

    [Fact]
    public void StartOfWeek_DefaultFirstDay_ReturnsCorrectStartOfWeek()
    {
        // Arrange
        var offset = TimeSpan.FromHours(2);
        var date = new DateTimeOffset(2024, 3, 15, 14, 30, 45, offset); // Friday

        // Act
        var result = date.StartOfWeek(); // Default should be Monday

        // Assert
        result.ShouldBe(new DateTimeOffset(2024, 3, 11, 0, 0, 0, offset)); // Should be Monday
        result.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        result.Offset.ShouldBe(offset);
    }

    [Theory]
    [InlineData("2024-03-15 23:59:59", 2)]  // End of day UTC+2
    [InlineData("2024-03-15 00:00:00", -5)] // Start of day UTC-5
    [InlineData("2024-03-15 12:30:45", 0)]  // Middle of day UTC
    public void StartAndEndOfDay_PreservesOffset(string dateTimeString, int offsetHours)
    {
        // Arrange
        var offset = TimeSpan.FromHours(offsetHours);
        var dateTime = DateTime.Parse(dateTimeString);
        var date = new DateTimeOffset(dateTime, offset);

        // Act
        var startOfDay = date.StartOfDay();
        var endOfDay = date.EndOfDay();

        // Assert
        startOfDay.Offset.ShouldBe(offset);
        endOfDay.Offset.ShouldBe(offset);
        startOfDay.DateTime.TimeOfDay.ShouldBe(TimeSpan.Zero);
        endOfDay.DateTime.TimeOfDay.ShouldBe(new TimeSpan(23, 59, 59));
    }
}