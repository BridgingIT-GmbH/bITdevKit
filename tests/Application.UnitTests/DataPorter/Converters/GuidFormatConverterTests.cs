// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class GuidFormatConverterTests
{
    [Fact]
    public void ConvertToExport_WithDefaultFormat_ReturnsDashedString()
    {
        // Arrange
        var sut = new GuidFormatConverter();
        var context = CreateContext();
        var value = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    }

    [Fact]
    public void ConvertToExport_WithUpperCase_ReturnsUpperCaseString()
    {
        // Arrange
        var sut = new GuidFormatConverter { UseUpperCase = true };
        var context = CreateContext();
        var value = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        // Act
        var result = sut.ConvertToExport(value, context);

        // Assert
        result.ShouldBe("3F2504E0-4F89-11D3-9A0C-0305E82C3301");
    }

    [Fact]
    public void ConvertFromImport_WithExactFormat_ReturnsGuid()
    {
        // Arrange
        var sut = new GuidFormatConverter { Format = "N" };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("3f2504e04f8911d39a0c0305e82c3301", context);

        // Assert
        result.ShouldBe(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"));
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsGuid()
    {
        // Arrange
        IValueConverter sut = new GuidFormatConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("3f2504e0-4f89-11d3-9a0c-0305e82c3301", context);

        // Assert
        result.ShouldBe(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"));
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "Identifier",
            PropertyType = typeof(Guid),
            EntityType = typeof(SimpleEntity)
        };
    }
}
