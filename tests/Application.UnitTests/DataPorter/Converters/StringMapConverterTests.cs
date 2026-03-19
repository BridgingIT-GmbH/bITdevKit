// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class StringMapConverterTests
{
    [Fact]
    public void ConvertFromImport_WithMappedValue_ReturnsMappedResult()
    {
        // Arrange
        var sut = new StringMapConverter<bool>
        {
            ImportMappings = new Dictionary<string, bool>
            {
                ["ja"] = true,
                ["nein"] = false
            }
        };
        var context = CreateContext(typeof(bool));

        // Act
        var result = sut.ConvertFromImport("JA", context);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ConvertToExport_WithReverseImportMapping_ReturnsMappedKey()
    {
        // Arrange
        var sut = new StringMapConverter<bool>
        {
            ImportMappings = new Dictionary<string, bool>
            {
                ["ja"] = true,
                ["nein"] = false
            }
        };
        var context = CreateContext(typeof(bool));

        // Act
        var result = sut.ConvertToExport(true, context);

        // Assert
        result.ShouldBe("ja");
    }

    [Fact]
    public void ConvertToExport_WithExplicitExportMapping_ReturnsConfiguredValue()
    {
        // Arrange
        var sut = new StringMapConverter<TestStatusWithoutDisplay>
        {
            ExportMappings = new Dictionary<TestStatusWithoutDisplay, string>
            {
                [TestStatusWithoutDisplay.Active] = "A"
            }
        };
        var context = CreateContext(typeof(TestStatusWithoutDisplay));

        // Act
        var result = sut.ConvertToExport(TestStatusWithoutDisplay.Active, context);

        // Assert
        result.ShouldBe("A");
    }

    [Fact]
    public void ConvertFromImport_WithoutMapping_UsesFallbackConversion()
    {
        // Arrange
        var sut = new StringMapConverter<int>();
        var context = CreateContext(typeof(int));

        // Act
        var result = sut.ConvertFromImport("42", context);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void ConvertFromImport_WithEnumFallback_ReturnsEnumValue()
    {
        // Arrange
        var sut = new StringMapConverter<TestStatusWithoutDisplay>();
        var context = CreateContext(typeof(TestStatusWithoutDisplay));

        // Act
        var result = sut.ConvertFromImport("Inactive", context);

        // Assert
        result.ShouldBe(TestStatusWithoutDisplay.Inactive);
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsMappedValue()
    {
        // Arrange
        IValueConverter sut = new StringMapConverter<bool>
        {
            ImportMappings = new Dictionary<string, bool>
            {
                ["yes"] = true,
                ["no"] = false
            }
        };
        var context = CreateContext(typeof(bool));

        // Act
        var result = sut.ConvertFromImport("yes", context);

        // Assert
        result.ShouldBe(true);
    }

    private static ValueConversionContext CreateContext(Type propertyType)
    {
        return new ValueConversionContext
        {
            PropertyName = "MappedValue",
            PropertyType = propertyType,
            EntityType = typeof(SimpleEntity)
        };
    }
}
