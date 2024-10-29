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
    [InlineData("99999")]  // Too small
    public void TryParseDateOrEpoch_InvalidUnixTimestamp_ReturnsFalse(string source)
    {
        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }

    [Theory]
    [InlineData("2024-03-14")]                         // ISO 8601
    [InlineData("2024-03-14T13:45:30")]               // ISO 8601 with time
    [InlineData("2024-03-14T13:45:30Z")]              // ISO 8601 with UTC
    [InlineData("2024-03-14T13:45:30.1234567")]       // ISO 8601 with milliseconds
    [InlineData("14/03/2024")]                        // UK format
    [InlineData("03/14/2024")]                        // US format
    [InlineData("14-03-2024")]                        // Alternative format
    [InlineData("14.03.2024")]                        // European format
    [InlineData("20240314")]                          // Compact format
    [InlineData("14 Mar 2024")]                       // Month name format
    [InlineData("14 March 2024")]                     // Full month name format
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
    [InlineData("2024-13-14")]        // Invalid month
    [InlineData("2024-03-32")]        // Invalid day
    [InlineData("14/13/2024")]        // Invalid month
    [InlineData("32/03/2024")]        // Invalid day
    public void TryParseDateOrEpoch_InvalidDateFormats_ReturnsFalse(string source)
    {
        // Act
        var success = source.TryParseDateOrEpoch(out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(DateTime.MinValue);
    }
}