// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DateTimeFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithConfiguredFormat_ReturnsFormattedString()
    {
        // Arrange
        var sut = new DateTimeFormatConverter
        {
            Format = "yyyy/MM/dd HH:mm:ss",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();
        var value = new DateTime(2024, 12, 31, 23, 45, 10);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("2024/12/31 23:45:10");
    }

    [Fact]
    public void ConvertToExport_WithIso8601AndUtcConversion_ReturnsUtcRoundTripString()
    {
        // Arrange
        var sut = new DateTimeFormatConverter { UseIso8601 = true, ConvertToUtcOnExport = true };
        var context = CreateContext();
        var value = new DateTime(2024, 12, 31, 23, 45, 10, DateTimeKind.Utc);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("2024-12-31T23:45:10.0000000Z");
    }

    [Fact]
    public void ConvertFromImport_WithConfiguredFormat_ReturnsDateTime()
    {
        // Arrange
        var sut = new DateTimeFormatConverter
        {
            Format = "dd.MM.yyyy HH:mm:ss",
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("31.12.2024 23:45:10", context);

        // Assert
        result.ShouldBe(new DateTime(2024, 12, 31, 23, 45, 10));
    }

    [Fact]
    public void ConvertFromImport_WithIso8601AndUtcConversion_ReturnsUtcDateTime()
    {
        // Arrange
        var sut = new DateTimeFormatConverter { UseIso8601 = true, ConvertToUtcOnImport = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31T23:45:10.0000000+02:00", context);

        // Assert
        result.ShouldBe(new DateTime(2024, 12, 31, 21, 45, 10, DateTimeKind.Utc));
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ConvertFromImport_WithDateTimeOffsetValue_ReturnsDateTime()
    {
        // Arrange
        var sut = new DateTimeFormatConverter();
        var context = CreateContext();
        var value = new DateTimeOffset(2024, 12, 31, 23, 45, 10, TimeSpan.FromHours(2));

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBe(value.DateTime);
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithDateTimeObject_ReturnsFormattedString()
    {
        // Arrange
        IValueConverter sut = new DateTimeFormatConverter
        {
            Format = "yyyy-MM-dd",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(new DateTime(2024, 12, 31), context);

        // Assert
        result.ShouldBe("2024-12-31");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsDateTime()
    {
        // Arrange
        IValueConverter sut = new DateTimeFormatConverter
        {
            Format = "yyyy-MM-dd",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31", context);

        // Assert
        result.ShouldBe(new DateTime(2024, 12, 31));
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "CreatedOn",
            PropertyType = typeof(DateTime),
            EntityType = typeof(SimpleEntity)
        };
    }
}
