// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DateOnlyFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithConfiguredFormat_ReturnsFormattedString()
    {
        // Arrange
        var sut = new DateOnlyFormatConverter
        {
            Format = "dd.MM.yyyy",
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();
        var value = new DateOnly(2024, 12, 31);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("31.12.2024");
    }

    [Fact]
    public void ConvertToExport_WithIso8601_ReturnsIsoFormattedString()
    {
        // Arrange
        var sut = new DateOnlyFormatConverter { UseIso8601 = true };
        var context = CreateContext();
        var value = new DateOnly(2024, 12, 31);

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("2024-12-31");
    }

    [Fact]
    public void ConvertFromImport_WithConfiguredFormat_ReturnsDateOnly()
    {
        // Arrange
        var sut = new DateOnlyFormatConverter
        {
            Format = "dd.MM.yyyy",
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("31.12.2024", context);

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    [Fact]
    public void ConvertFromImport_WithIso8601_ReturnsDateOnly()
    {
        // Arrange
        var sut = new DateOnlyFormatConverter { UseIso8601 = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31", context);

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    [Fact]
    public void ConvertFromImport_WithDateTimeValue_ReturnsDateOnly()
    {
        // Arrange
        var sut = new DateOnlyFormatConverter();
        var context = CreateContext();
        var value = new DateTime(2024, 12, 31, 23, 45, 10);

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithDateOnlyObject_ReturnsFormattedString()
    {
        // Arrange
        IValueConverter sut = new DateOnlyFormatConverter
        {
            Format = "yyyy-MM-dd",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(new DateOnly(2024, 12, 31), context);

        // Assert
        result.ShouldBe("2024-12-31");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsDateOnly()
    {
        // Arrange
        IValueConverter sut = new DateOnlyFormatConverter
        {
            Format = "yyyy-MM-dd",
            Culture = CultureInfo.InvariantCulture
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2024-12-31", context);

        // Assert
        result.ShouldBe(new DateOnly(2024, 12, 31));
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "BirthDate",
            PropertyType = typeof(DateOnly),
            EntityType = typeof(SimpleEntity)
        };
    }
}
