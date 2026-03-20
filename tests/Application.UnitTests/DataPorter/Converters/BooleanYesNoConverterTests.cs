// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class BooleanYesNoConverterTests
{
    [Fact]
    public void ConvertToExport_WithTrueValue_ReturnsYes()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(true, context);

        // Assert
        result.ShouldBe("Yes");
    }

    [Fact]
    public void ConvertToExport_WithFalseValue_ReturnsNo()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport(false, context);

        // Assert
        result.ShouldBe("No");
    }

    [Fact]
    public void ConvertToExport_WithCustomValues_ReturnsCustomStrings()
    {
        // Arrange
        var sut = new BooleanYesNoConverter { YesValue = "Active", NoValue = "Inactive" };
        var context = CreateContext();

        // Act
        var trueResult = sut.ConvertToExport(true, context);
        var falseResult = sut.ConvertToExport(false, context);

        // Assert
        trueResult.ShouldBe("Active");
        falseResult.ShouldBe("Inactive");
    }

    [Fact]
    public void ConvertFromImport_WithYesString_ReturnsTrue()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Yes", context);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ConvertFromImport_WithNoString_ReturnsFalse()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("No", context);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("YES")]
    [InlineData("Yes")]
    [InlineData("ja")]
    [InlineData("JA")]
    public void ConvertFromImport_WithYesCaseInsensitive_ReturnsTrue(string value)
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void ConvertFromImport_WithTrueString_ReturnsTrue(string value)
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("y")]
    [InlineData("Y")]
    [InlineData("j")]
    [InlineData("J")]
    public void ConvertFromImport_WithAlternativeTrueValues_ReturnsTrue(string value)
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ConvertFromImport_WithBooleanValue_ReturnsSameValue()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var trueResult = sut.ConvertFromImport(true, context);
        var falseResult = sut.ConvertFromImport(false, context);

        // Assert
        trueResult.ShouldBeTrue();
        falseResult.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConvertFromImport_WithNullOrWhitespace_ReturnsFalse(string value)
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport(value, context);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ConvertFromImport_WithUnrecognizedString_ReturnsFalse()
    {
        // Arrange
        var sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("maybe", context);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithNonBooleanObject_ReturnsFalseString()
    {
        // Arrange
        IValueConverter sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertToExport("not a boolean", context);

        // Assert
        result.ShouldBe("No");
    }

    [Fact]
    public void IValueConverter_ConvertToExport_WithBooleanObject_ReturnsCorrectString()
    {
        // Arrange
        IValueConverter sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var trueResult = sut.ConvertToExport((object)true, context);
        var falseResult = sut.ConvertToExport((object)false, context);

        // Assert
        trueResult.ShouldBe("Yes");
        falseResult.ShouldBe("No");
    }

    [Fact]
    public void IValueConverter_ConvertFromImport_ReturnsBoolean()
    {
        // Arrange
        IValueConverter sut = new BooleanYesNoConverter();
        var context = CreateContext();

        // Act
        var result = sut.ConvertFromImport("Yes", context);

        // Assert
        result.ShouldBe(true);
    }

    private static ValueConversionContext CreateContext()
    {
        return new ValueConversionContext
        {
            PropertyName = "TestProperty",
            PropertyType = typeof(bool),
            EntityType = typeof(SimpleEntity)
        };
    }
}
