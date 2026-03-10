// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DataPorterColumnAttributeTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var sut = new DataPorterColumnAttribute();

        // Assert
        sut.Name.ShouldBeNull();
        sut.Order.ShouldBe(-1);
        sut.Width.ShouldBe(-1);
        sut.Required.ShouldBeFalse();
        sut.Export.ShouldBeTrue();
        sut.Import.ShouldBeTrue();
        sut.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        sut.VerticalAlignment.ShouldBe(VerticalAlignment.Middle);
    }

    [Fact]
    public void ConstructorWithName_SetsName()
    {
        // Arrange & Act
        var sut = new DataPorterColumnAttribute("Display Name");

        // Assert
        sut.Name.ShouldBe("Display Name");
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        // Arrange & Act
        var sut = new DataPorterColumnAttribute
        {
            Name = "Column Name",
            Order = 5,
            Format = "N2",
            Width = 150,
            NullValue = "N/A",
            Required = true,
            RequiredMessage = "This field is required",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Export = false,
            Import = false
        };

        // Assert
        sut.Name.ShouldBe("Column Name");
        sut.Order.ShouldBe(5);
        sut.Format.ShouldBe("N2");
        sut.Width.ShouldBe(150);
        sut.NullValue.ShouldBe("N/A");
        sut.Required.ShouldBeTrue();
        sut.RequiredMessage.ShouldBe("This field is required");
        sut.HorizontalAlignment.ShouldBe(HorizontalAlignment.Center);
        sut.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        sut.Export.ShouldBeFalse();
        sut.Import.ShouldBeFalse();
    }
}

[UnitTest("Common")]
public class DataPorterSheetAttributeTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        // Arrange & Act
        var sut = new DataPorterSheetAttribute("Sheet Name");

        // Assert
        sut.Name.ShouldBe("Sheet Name");
        sut.Index.ShouldBe(-1);
    }

    [Fact]
    public void Index_CanBeSet()
    {
        // Arrange & Act
        var sut = new DataPorterSheetAttribute("Sheet") { Index = 2 };

        // Assert
        sut.Index.ShouldBe(2);
    }
}

[UnitTest("Common")]
public class DataPorterIgnoreAttributeTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var sut = new DataPorterIgnoreAttribute();

        // Assert
        sut.ExportOnly.ShouldBeFalse();
        sut.ImportOnly.ShouldBeFalse();
    }

    [Fact]
    public void ExportOnly_CanBeSet()
    {
        // Arrange & Act
        var sut = new DataPorterIgnoreAttribute { ExportOnly = true };

        // Assert
        sut.ExportOnly.ShouldBeTrue();
        sut.ImportOnly.ShouldBeFalse();
    }

    [Fact]
    public void ImportOnly_CanBeSet()
    {
        // Arrange & Act
        var sut = new DataPorterIgnoreAttribute { ImportOnly = true };

        // Assert
        sut.ImportOnly.ShouldBeTrue();
        sut.ExportOnly.ShouldBeFalse();
    }
}

[UnitTest("Common")]
public class DataPorterConverterAttributeTests
{
    [Fact]
    public void Constructor_WithNullType_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DataPorterConverterAttribute(null));
    }

    [Fact]
    public void Constructor_WithNonConverterType_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new DataPorterConverterAttribute(typeof(string)));
    }

    [Fact]
    public void Constructor_WithValidConverterType_SetsConverterType()
    {
        // Arrange & Act
        var sut = new DataPorterConverterAttribute(typeof(BooleanYesNoConverter));

        // Assert
        sut.ConverterType.ShouldBe(typeof(BooleanYesNoConverter));
    }
}

[UnitTest("Common")]
public class DataPorterValidationAttributeTests
{
    [Fact]
    public void Constructor_SetsTypeAndErrorMessage()
    {
        // Arrange & Act
        var sut = new DataPorterValidationAttribute(ValidationType.Required, "Field is required");

        // Assert
        sut.Type.ShouldBe(ValidationType.Required);
        sut.ErrorMessage.ShouldBe("Field is required");
    }

    [Fact]
    public void Constructor_WithNullErrorMessage_SetsNullErrorMessage()
    {
        // Arrange & Act
        var sut = new DataPorterValidationAttribute(ValidationType.Email);

        // Assert
        sut.Type.ShouldBe(ValidationType.Email);
        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Parameter_CanBeSet()
    {
        // Arrange & Act
        var sut = new DataPorterValidationAttribute(ValidationType.MinLength) { Parameter = 5 };

        // Assert
        sut.Parameter.ShouldBe(5);
    }

    [Theory]
    [InlineData(ValidationType.Required)]
    [InlineData(ValidationType.MinLength)]
    [InlineData(ValidationType.MaxLength)]
    [InlineData(ValidationType.Range)]
    [InlineData(ValidationType.Regex)]
    [InlineData(ValidationType.Email)]
    [InlineData(ValidationType.Url)]
    public void AllValidationTypes_CanBeUsed(ValidationType type)
    {
        // Arrange & Act
        var sut = new DataPorterValidationAttribute(type);

        // Assert
        sut.Type.ShouldBe(type);
    }
}
