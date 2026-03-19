// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DecimalFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithConfiguredFormatAndCulture_ReturnsFormattedString()
    {
        // Arrange
        var sut = new DecimalFormatConverter
        {
            Format = "N2",
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(1234.56m, context);

        // Assert
        result.ShouldBe("1.234,56");
    }

    [Fact]
    public void ConvertToExport_WithInvariantCulture_ReturnsInvariantString()
    {
        // Arrange
        var sut = new DecimalFormatConverter
        {
            Format = "0.00",
            UseInvariantCulture = true
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(1234.56m, context);

        // Assert
        result.ShouldBe("1234.56");
    }

    [Fact]
    public void ConvertFromImport_WithConfiguredCulture_ReturnsDecimal()
    {
        // Arrange
        var sut = new DecimalFormatConverter
        {
            Culture = new CultureInfo("de-DE")
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("1.234,56", context);

        // Assert
        result.ShouldBe(1234.56m);
    }

    [Fact]
    public void ConvertFromImport_WithInvariantCulture_ReturnsDecimal()
    {
        // Arrange
        var sut = new DecimalFormatConverter { UseInvariantCulture = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("1234.56", context);

        // Assert
        result.ShouldBe(1234.56m);
    }

    [Fact]
    public void ConvertFromImport_WithNumericValue_ReturnsDecimal()
    {
        // Arrange
        var sut = new DecimalFormatConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(1234.56d, context);

        // Assert
        result.ShouldBe(1234.56m);
    }

    [Fact]
    public void ConvertFromImport_WithCurrencyStyle_ReturnsDecimal()
    {
        // Arrange
        var sut = new DecimalFormatConverter
        {
            Culture = new CultureInfo("en-US"),
            NumberStyles = NumberStyles.Currency
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("$1,234.56", context);

        // Assert
        result.ShouldBe(1234.56m);
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithDecimalObject_ReturnsFormattedString()
    {
        // Arrange
        IValueConverter sut = new DecimalFormatConverter
        {
            Format = "F2",
            UseInvariantCulture = true
        };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(12.3m, context);

        // Assert
        result.ShouldBe("12.30");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsDecimal()
    {
        // Arrange
        IValueConverter sut = new DecimalFormatConverter { UseInvariantCulture = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("12.30", context);

        // Assert
        result.ShouldBe(12.30m);
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "Amount",
            PropertyType = typeof(decimal),
            EntityType = typeof(SimpleEntity)
        };
    }
}
