// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.DataPorter;

using System.ComponentModel.DataAnnotations;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class EnumDisplayNameConverterTests
{
    [Fact]
    public void ConvertToExport_WithEnumWithoutDisplayAttribute_ReturnsEnumName()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(TestStatusWithoutDisplay.Active, context);

        // Assert
        result.ShouldBe("Active");
    }

    [Fact]
    public void ConvertToExport_WithEnumWithDisplayAttribute_ReturnsDisplayName()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(TestStatusWithDisplay.Active, context);

        // Assert
        result.ShouldBe("Currently Active");
    }

    [Fact]
    public void ConvertFromImport_WithEnumName_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Active", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Active);
    }

    [Fact]
    public void ConvertFromImport_WithDisplayName_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Currently Active", context);

        // Assert
        result.ShouldBe(TestStatusWithDisplay.Active);
    }

    [Fact]
    public void ConvertFromImport_WithEnumNameCaseInsensitive_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("active", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Active);
    }

    [Fact]
    public void ConvertFromImport_WithDisplayNameCaseInsensitive_ReturnsEnumValue()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("currently active", context);

        // Assert
        result.ShouldBe(TestStatusWithDisplay.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConvertFromImport_WithNullOrWhitespace_ReturnsDefault(string value)
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void ConvertFromImport_WithUnrecognizedValue_ReturnsDefault()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("NotAValidStatus", context);

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void ConvertFromImport_PrefersEnumNameOverDisplayName()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Active", context);

        // Assert
        result.ShouldBe(TestStatusWithDisplay.Active);
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithNonEnumObject_ReturnsDefaultName()
    {
        // Arrange
        IValueConverter sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport("not an enum", context);

        // Assert
        result.ShouldBe("Pending"); // Default enum value name
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithValidEnum_ReturnsCorrectString()
    {
        // Arrange
        IValueConverter sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(TestStatusWithDisplay.Inactive, context);

        // Assert
        result.ShouldBe("Not Active");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsEnumValue()
    {
        // Arrange
        IValueConverter sut = new EnumDisplayNameConverter<TestStatusWithoutDisplay>();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Active", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Active);
    }

    [Fact]
    public void ConvertToExport_WithAllEnumValues_ReturnsCorrectDisplayNames()
    {
        // Arrange
        var sut = new EnumDisplayNameConverter<TestStatusWithDisplay>();
        var context = CreateContext();

        // Act & Assert
        sut.ConvertToExport(TestStatusWithDisplay.Pending, context).ShouldBe("Pending");
        sut.ConvertToExport(TestStatusWithDisplay.Active, context).ShouldBe("Currently Active");
        sut.ConvertToExport(TestStatusWithDisplay.Inactive, context).ShouldBe("Not Active");
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "Status",
            PropertyType = typeof(TestStatusWithDisplay),
            EntityType = typeof(SimpleEntity)
        };
    }
}

/// <summary>
/// Test enum without display attributes.
/// </summary>
public enum TestStatusWithoutDisplay
{
    Pending,
    Active,
    Inactive
}

/// <summary>
/// Test enum with display attributes.
/// </summary>
public enum TestStatusWithDisplay
{
    Pending,

    [Display(Name = "Currently Active")]
    Active,

    [Display(Name = "Not Active")]
    Inactive
}
