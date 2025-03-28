// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.JobScheduling;

using BridgingIT.DevKit.Application.JobScheduling;
using Xunit;

public class CronExpressionBuilderTests
{
    [Fact]
    public void Build_DefaultExpression_ReturnsEveryMinute()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Build();

        // Assert
        Assert.Equal("0 * * * * ?", result); // Default: run at start of every minute
    }

    [Fact]
    public void SecondsInt_SetsSpecificValue_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Seconds(5).Build();

        // Assert
        Assert.Equal("5 * * * * ?", result); // At 5 seconds past the minute
    }

    [Fact]
    public void SecondsRange_SetsRange_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.SecondsRange(0, 10).Build();

        // Assert
        Assert.Equal("0-10 * * * * ?", result); // From 0 to 10 seconds past the minute
    }

    [Fact]
    public void SecondsInt_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Seconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Seconds(60));
    }

    [Fact]
    public void SecondsRange_InvalidRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SecondsRange(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SecondsRange(0, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SecondsRange(10, 5)); // end < start
    }

    [Fact]
    public void MinutesInt_SetsSpecificValue_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Minutes(15).Build();

        // Assert
        Assert.Equal("0 15 * * * ?", result); // At 15 minutes past the hour
    }

    [Fact]
    public void MinutesRange_SetsRange_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.MinutesRange(0, 30).Build();

        // Assert
        Assert.Equal("0 0-30 * * * ?", result); // From 0 to 30 minutes past the hour
    }

    [Fact]
    public void MinutesInt_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Minutes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Minutes(60));
    }

    [Fact]
    public void HoursInt_SetsSpecificValue_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Hours(9).Build();

        // Assert
        Assert.Equal("0 * 9 * * ?", result); // At 9 AM
    }

    [Fact]
    public void HoursRange_SetsRange_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.HoursRange(8, 17).Build();

        // Assert
        Assert.Equal("0 * 8-17 * * ?", result); // From 8 AM to 5 PM
    }

    [Fact]
    public void HoursInt_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Hours(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Hours(24));
    }

    [Fact]
    public void DayOfMonthInt_SetsSpecificValue_ResetsDayOfWeek()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.DayOfWeek(CronDayOfWeek.Monday).DayOfMonth(15).Build();

        // Assert
        Assert.Equal("0 * * 15 * ?", result); // Day of month takes precedence
    }

    [Fact]
    public void DayOfMonthRange_SetsRange_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.DayOfMonthRange(1, 10).Build();

        // Assert
        Assert.Equal("0 * * 1-10 * ?", result); // First 10 days of the month
    }

    [Fact]
    public void DayOfMonthString_SetsSpecialValue_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.DayOfMonth("L").Build();

        // Assert
        Assert.Equal("0 * * L * ?", result); // Last day of the month
    }

    [Fact]
    public void DayOfMonthInt_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.DayOfMonth(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.DayOfMonth(32));
    }

    [Fact]
    public void DayOfWeekEnum_SetsSpecificValue_ResetsDayOfMonth()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.DayOfMonth(15).DayOfWeek(CronDayOfWeek.Wednesday).Build();

        // Assert
        Assert.Equal("0 * * ? * WED", result); // Day of week takes precedence
    }

    [Fact]
    public void DayOfWeekString_SetsSpecificValue_ResetsDayOfMonth()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.DayOfMonth(15).DayOfWeek("WED").Build();

        // Assert
        Assert.Equal("0 * * ? * WED", result); // Day of week takes precedence
    }

    [Fact]
    public void MonthEnum_SetsSpecificValue_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Month(CronMonth.January).Build();

        // Assert
        Assert.Equal("0 * * * JAN ?", result); // Every day in January
    }

    [Fact]
    public void MonthString_SetsSpecificRange_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.Month("JAN-MAR").Build();

        // Assert
        Assert.Equal("0 * * * JAN-MAR ?", result); // Every day in January to March
    }

    [Fact]
    public void SpecificDateTime_CombinesFieldsCorrectly()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder
            .DayOfMonth(15)
            .DayOfWeek(CronDayOfWeek.Wednesday)
            .Hours(12)
            .Minutes(59)
            .Build();

        // Assert
        Assert.Equal("0 59 12 ? * WED", result); // Every Wednesday at 12:59 PM
    }

    [Fact]
    public void AtTime_ValidHourMinuteSecond_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.AtTime(9, 30, 15).Build();

        // Assert
        Assert.Equal("15 30 9 * * ?", result); // Daily at 9:30:15 AM
    }

    [Fact]
    public void AtDateTime_SetsSpecificDateTime_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();
        var dateTime = new DateTimeOffset(2025, 3, 27, 14, 30, 0, TimeSpan.Zero);

        // Act
        var result = builder.AtDateTime(dateTime).Build();

        // Assert
        Assert.Equal("0 30 14 27 3 ?", result); // March 27, 2025, at 2:30:00 PM
    }

    [Fact]
    public void EveryMinutes_ValidInterval_ReturnsCorrectExpression()
    {
        // Arrange
        var builder = new CronExpressionBuilder();

        // Act
        var result = builder.EveryMinutes(5).Build();

        // Assert
        Assert.Equal("0 0/5 * * * ?", result); // Every 5 minutes
    }
}