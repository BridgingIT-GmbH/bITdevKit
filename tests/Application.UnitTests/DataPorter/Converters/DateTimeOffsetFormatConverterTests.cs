// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DateTimeOffsetFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithConfiguredFormat_ReturnsFormattedString()
    {
        // Arrange
        var sut = new DateTimeOffsetFormatConverter
        {
            Format = "yyyy/MM/dd HH:mm:ss zzz",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 12, 31, 23, 45, 10, TimeSpan.FromHours(2));

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("2024/12/31 23:45:10 +02:00");
    }

    [Fact]
    public void ConvertToExport_WithIso8601AndUtcConversion_ReturnsUtcRoundTripString()
    {
        // Arrange
        var sut = new DateTimeOffsetFormatConverter { UseIso8601 = true, ConvertToUtcOnExport = true };
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 12, 31, 23, 45, 10, TimeSpan.FromHours(2));

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("2024-12-31T21:45:10.0000000+00:00");
    }

    [Fact]
    public void ConvertFromImport_WithConfiguredFormat_ReturnsDateTimeOffset()
    {
        // Arrange
        var sut = new DateTimeOffsetFormatConverter
        {
            Format = "dd.MM.yyyy HH:mm:ss zzz",
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("31.12.2024 23:45:10 +02:00", context);

        // Assert
        result.ShouldBe(new DateTimeOffset(2024, 12, 31, 23, 45, 10, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void ConvertFromImport_WithIso8601AndUtcConversion_ReturnsUtcDateTimeOffset()
    {
        // Arrange
        var sut = new DateTimeOffsetFormatConverter { UseIso8601 = true, ConvertToUtcOnImport = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31T23:45:10.0000000+02:00", context);

        // Assert
        result.ShouldBe(new DateTimeOffset(2024, 12, 31, 21, 45, 10, TimeSpan.Zero));
    }

    [Fact]
    public void ConvertFromImport_WithDateTimeValue_ReturnsDateTimeOffset()
    {
        // Arrange
        var sut = new DateTimeOffsetFormatConverter();
        var context = CreateContext();
        var value = new DateTime(2024, 12, 31, 23, 45, 10, DateTimeKind.Utc);

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBe(new DateTimeOffset(value, TimeSpan.Zero));
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithDateTimeOffsetObject_ReturnsFormattedString()
    {
        // Arrange
        IValueConverter sut = new DateTimeOffsetFormatConverter
        {
            Format = "yyyy-MM-dd zzz",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero), context);

        // Assert
        result.ShouldBe("2024-12-31 +00:00");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsDateTimeOffset()
    {
        // Arrange
        IValueConverter sut = new DateTimeOffsetFormatConverter
        {
            Format = "yyyy-MM-dd zzz",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31 +00:00", context);

        // Assert
        result.ShouldBe(new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "CreatedOn",
            PropertyType = typeof(DateTimeOffset),
            EntityType = typeof(SimpleEntity)
        };
    }
}
