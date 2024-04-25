// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using Xunit;
using Shouldly;

[UnitTest("Common")]
public class DateTimeExtensionsTests
{
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
        startOfWeek.ShouldBe(new DateTime(2021, 12, 26));
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
        startOfWeek.ShouldBe(new DateTimeOffset(2021, 12, 26, 0, 0, 0, TimeSpan.FromHours(2)));
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
}