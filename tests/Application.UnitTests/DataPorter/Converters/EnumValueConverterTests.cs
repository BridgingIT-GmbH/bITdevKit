// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class EnumValueConverterTests
{
    [Fact]
    public void ConvertToExport_WithEnumName_ReturnsName()
    {
        // Arrange
        var sut = new EnumValueConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(TestStatusWithoutDisplay.Active, context);

        // Assert
        result.ShouldBe("Active");
    }

    [Fact]
    public void ConvertToExport_WithNumericOption_ReturnsUnderlyingValue()
    {
        // Arrange
        var sut = new EnumValueConverter<TestStatusWithoutDisplay> { ExportAsNumeric = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(TestStatusWithoutDisplay.Inactive, context);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void ConvertFromImport_WithName_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumValueConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("active", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Active);
    }

    [Fact]
    public void ConvertFromImport_WithNumericValue_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumValueConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(2, context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Inactive);
    }

    [Fact]
    public void ConvertFromImport_WithNumericStringNotAllowed_ReturnsDefault()
    {
        // Arrange
        var sut = new EnumValueConverter<TestStatusWithoutDisplay> { AllowNumericImport = false };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("2", context);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsEnumValue()
    {
        // Arrange
        IValueConverter sut = new EnumValueConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Inactive", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Inactive);
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "Status",
            PropertyType = typeof(TestStatusWithoutDisplay),
            EntityType = typeof(SimpleEntity)
        };
    }
}
