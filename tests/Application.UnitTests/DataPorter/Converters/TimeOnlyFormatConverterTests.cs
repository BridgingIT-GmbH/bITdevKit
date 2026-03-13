// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class TimeOnlyFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithConfiguredFormat_ReturnsFormattedString()
    {
        // Arrange
        var sut = new TimeOnlyFormatConverter
        {
            Format = "HH:mm:ss",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();
        var value = new TimeOnly(23, 45, 10);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("23:45:10");
    }

    [Fact]
    public void ConvertToExport_WithIso8601_ReturnsIsoFormattedString()
    {
        // Arrange
        var sut = new TimeOnlyFormatConverter { UseIso8601 = true };
        var context = CreateContext();
        var value = TimeOnly.ParseExact("23:45:10.1234000", "HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("23:45:10.1234000");
    }

    [Fact]
    public void ConvertFromImport_WithConfiguredFormat_ReturnsTimeOnly()
    {
        // Arrange
        var sut = new TimeOnlyFormatConverter
        {
            Format = "HH:mm:ss",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("23:45:10", context);

        // Assert
        result.ShouldBe(new TimeOnly(23, 45, 10));
    }

    [Fact]
    public void ConvertFromImport_WithIso8601_ReturnsTimeOnly()
    {
        // Arrange
        var sut = new TimeOnlyFormatConverter { UseIso8601 = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("23:45:10.1234000", context);

        // Assert
        result.ShouldBe(TimeOnly.ParseExact("23:45:10.1234000", "HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertFromImport_WithDateTimeValue_ReturnsTimeOnly()
    {
        // Arrange
        var sut = new TimeOnlyFormatConverter();
        var context = CreateContext();
        var value = new DateTime(2024, 12, 31, 23, 45, 10);

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBe(new TimeOnly(23, 45, 10));
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithTimeOnlyObject_ReturnsFormattedString()
    {
        // Arrange
        IValueConverter sut = new TimeOnlyFormatConverter
        {
            Format = "HH:mm",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(new TimeOnly(23, 45, 10), context);

        // Assert
        result.ShouldBe("23:45");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsTimeOnly()
    {
        // Arrange
        IValueConverter sut = new TimeOnlyFormatConverter
        {
            Format = "HH:mm",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("23:45", context);

        // Assert
        result.ShouldBe(new TimeOnly(23, 45));
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "StartTime",
            PropertyType = typeof(TimeOnly),
            EntityType = typeof(SimpleEntity)
        };
    }
}
