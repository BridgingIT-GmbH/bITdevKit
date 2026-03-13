// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class StringTrimConverterTests
{
    [Fact]
    public void ConvertFromImport_WithTrimEnabled_ReturnsTrimmedString()
    {
        // Arrange
        var sut = new StringTrimConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("  hello  ", context);

        // Assert
        result.ShouldBe("hello");
    }

    [Fact]
    public void ConvertFromImport_WithLineEndingNormalization_ReturnsNormalizedString()
    {
        // Arrange
        var sut = new StringTrimConverter { NormalizeLineEndings = true, TrimStart = false, TrimEnd = false };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("a\r\nb\rc", context);

        // Assert
        result.ShouldBe("a\nb\nc");
    }

    [Fact]
    public void ConvertFromImport_WithEmptyToNull_ReturnsNull()
    {
        // Arrange
        var sut = new StringTrimConverter { ConvertEmptyToNull = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("   ", context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertToExport_WithTrimEndOnly_ReturnsExpectedString()
    {
        // Arrange
        var sut = new StringTrimConverter { TrimStart = false, TrimEnd = true };
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport("  hello  ", context);

        // Assert
        result.ShouldBe("  hello");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsTrimmedString()
    {
        // Arrange
        IValueConverter sut = new StringTrimConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("  hello  ", context);

        // Assert
        result.ShouldBe("hello");
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "Name",
            PropertyType = typeof(string),
            EntityType = typeof(SimpleEntity)
        };
    }
}
