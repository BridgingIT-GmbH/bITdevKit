// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System;
using Bogus;
using Shouldly;
using Xunit;

[UnitTest("Common")]
public class TimeSpanExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void ToCancellationTokenSource_ZeroTimeSpan_ReturnsCancelledToken()
    {
        // Arrange
        var timeSpan = TimeSpan.Zero;

        // Act
        var result = timeSpan.ToCancellationTokenSource();

        // Assert
        result.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void ToCancellationTokenSource_PositiveTimeSpan_ReturnsUncancelledToken()
    {
        // Arrange
        var timeSpan = TimeSpan.FromSeconds(this.faker.Random.Int(1, 10));

        // Act
        var result = timeSpan.ToCancellationTokenSource();

        // Assert
        result.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void ToCancellationTokenSource_NegativeTimeSpan_ReturnsUncancelledToken()
    {
        // Arrange
        var timeSpan = TimeSpan.FromSeconds(-this.faker.Random.Int(1, 10));

        // Act
        var result = timeSpan.ToCancellationTokenSource();

        // Assert
        result.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void ToCancellationTokenSource_NullableTimeSpanWithValue_ReturnsExpectedToken()
    {
        // Arrange
        TimeSpan? timeSpan = TimeSpan.FromSeconds(this.faker.Random.Int(1, 10));

        // Act
        var result = timeSpan.ToCancellationTokenSource();

        // Assert
        result.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void ToCancellationTokenSource_NullTimeSpan_ReturnsUncancelledToken()
    {
        // Arrange
        TimeSpan? timeSpan = null;

        // Act
        var result = timeSpan.ToCancellationTokenSource();

        // Assert
        result.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void ToCancellationTokenSource_NullTimeSpanWithDefault_UsesDefaultTimeout()
    {
        // Arrange
        TimeSpan? timeSpan = null;
        var defaultTimeout = TimeSpan.Zero;

        // Act
        var result = timeSpan.ToCancellationTokenSource(defaultTimeout);

        // Assert
        result.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Min_FirstSmallerThanSecond_ReturnsFirst()
    {
        // Arrange
        var first = TimeSpan.FromSeconds(1);
        var second = TimeSpan.FromSeconds(2);

        // Act
        var result = first.Min(second);

        // Assert
        result.ShouldBe(first);
    }

    [Fact]
    public void Min_SecondSmallerThanFirst_ReturnsSecond()
    {
        // Arrange
        var first = TimeSpan.FromSeconds(2);
        var second = TimeSpan.FromSeconds(1);

        // Act
        var result = first.Min(second);

        // Assert
        result.ShouldBe(second);
    }

    [Fact]
    public void Max_FirstLargerThanSecond_ReturnsFirst()
    {
        // Arrange
        var first = TimeSpan.FromSeconds(2);
        var second = TimeSpan.FromSeconds(1);

        // Act
        var result = first.Max(second);

        // Assert
        result.ShouldBe(first);
    }

    [Fact]
    public void Max_SecondLargerThanFirst_ReturnsSecond()
    {
        // Arrange
        var first = TimeSpan.FromSeconds(1);
        var second = TimeSpan.FromSeconds(2);

        // Act
        var result = first.Max(second);

        // Assert
        result.ShouldBe(second);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(-1)]
    public void Ticks_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Ticks();

        // Assert
        result.Ticks.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(-1)]
    public void Milliseconds_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Milliseconds();

        // Assert
        result.TotalMilliseconds.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(-1)]
    public void Seconds_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Seconds();

        // Assert
        result.TotalSeconds.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(-1)]
    public void Minutes_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Minutes();

        // Assert
        result.TotalMinutes.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(-1)]
    public void Hours_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Hours();

        // Assert
        result.TotalHours.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Days_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Days();

        // Assert
        result.TotalDays.ShouldBe(value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Weeks_ValidValue_ReturnsCorrectTimeSpan(long value)
    {
        // Act
        var result = value.Weeks();

        // Assert
        result.TotalDays.ShouldBe(value * 7);
    }

    [Fact]
    public void TruncateToSeconds_TimeSpanWithMilliseconds_RemovesMilliseconds()
    {
        // Arrange
        var timeSpan = TimeSpan.FromMilliseconds(this.faker.Random.Double(1000, 2000));

        // Act
        var result = timeSpan.TruncateToSeconds();

        // Assert
        result.Milliseconds.ShouldBe(0);
        result.Seconds.ShouldBe(timeSpan.Seconds);
    }

    [Fact]
    public void TruncateToSeconds_ComplexTimeSpan_PreservesLargerUnits()
    {
        // Arrange
        var original = new TimeSpan(1, 2, 3, 4, 500);

        // Act
        var result = original.TruncateToSeconds();

        // Assert
        result.Days.ShouldBe(1);
        result.Hours.ShouldBe(2);
        result.Minutes.ShouldBe(3);
        result.Seconds.ShouldBe(4);
        result.Milliseconds.ShouldBe(0);
    }

    [Fact]
    public void ParseTime_NullInput_ReturnsZeroTimeSpan()
    {
        // Arrange
        string source = null;

        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ParseTime_EmptyString_ReturnsZeroTimeSpan()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ParseTime_WhiteSpace_ReturnsZeroTimeSpan()
    {
        // Arrange
        var source = "   ";

        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("14:30:00", 14, 30, 0)]    // 24-hour with seconds
    [InlineData("14:30", 14, 30, 0)]       // 24-hour without seconds
    [InlineData("02:30:00 PM", 14, 30, 0)] // 12-hour with seconds
    [InlineData("02:30 PM", 14, 30, 0)]    // 12-hour without seconds
    [InlineData("143000", 14, 30, 0)]      // 24-hour compact with seconds
    [InlineData("1430", 14, 30, 0)]        // 24-hour compact without seconds
    [InlineData("02:30:00", 2, 30, 0)]     // 12-hour with seconds (AM)
    [InlineData("02:30", 2, 30, 0)]        // 12-hour without seconds (AM)
    public void ParseTime_ValidTimeFormats_ReturnsCorrectTimeSpan(string source, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        // Arrange
        var expected = new TimeSpan(expectedHours, expectedMinutes, expectedSeconds);

        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("02:30:45.500")]           // Direct TimeSpan format
    [InlineData("1.02:30:45")]             // TimeSpan with days
    public void ParseTime_ValidTimeSpanFormat_ReturnsCorrectTimeSpan(string source)
    {
        // Arrange
        var expected = TimeSpan.Parse(source);

        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("not-a-time")]
    [InlineData("14:60:00")]       // Invalid minutes
    [InlineData("14:30:61")]       // Invalid seconds
    [InlineData("14:30:00 XM")]    // Invalid meridiem
    public void ParseTime_InvalidTimeFormat_ReturnsZeroTimeSpan(string source)
    {
        // Act
        var result = source.ParseTime();

        // Assert
        result.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TryParseTime_NullInput_ReturnsFalse()
    {
        // Arrange
        string source = null;

        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TryParseTime_EmptyString_ReturnsFalse()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TryParseTime_WhiteSpace_ReturnsFalse()
    {
        // Arrange
        var source = "   ";

        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("14:30:00", 14, 30, 0)]    // 24-hour with seconds
    [InlineData("14:30", 14, 30, 0)]       // 24-hour without seconds
    [InlineData("02:30:00 PM", 14, 30, 0)] // 12-hour with seconds
    [InlineData("02:30 PM", 14, 30, 0)]    // 12-hour without seconds
    [InlineData("143000", 14, 30, 0)]      // 24-hour compact with seconds
    [InlineData("1430", 14, 30, 0)]        // 24-hour compact without seconds
    [InlineData("02:30:00", 2, 30, 0)]     // 12-hour with seconds (AM)
    [InlineData("02:30", 2, 30, 0)]        // 12-hour without seconds (AM)
    public void TryParseTime_ValidTimeFormats_ReturnsTrueAndCorrectTimeSpan(string source, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        // Arrange
        var expected = new TimeSpan(expectedHours, expectedMinutes, expectedSeconds);

        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("02:30:45.500")]           // Direct TimeSpan format
    [InlineData("1.02:30:45")]             // TimeSpan with days
    public void TryParseTime_ValidTimeSpanFormat_ReturnsTrueAndCorrectTimeSpan(string source)
    {
        // Arrange
        var expected = TimeSpan.Parse(source);

        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("not-a-time")]
    [InlineData("14:60:00")]       // Invalid minutes
    [InlineData("14:30:61")]       // Invalid seconds
    [InlineData("14:30:00 XM")]    // Invalid meridiem
    public void TryParseTime_InvalidTimeFormat_ReturnsFalseAndZeroTimeSpan(string source)
    {
        // Act
        var success = source.TryParseTime(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(TimeSpan.Zero);
    }
}