// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class ColumnConfigurationTests
{
    [Fact]
    public void GetValue_WithNullSource_ReturnsNull()
    {
        // Arrange
        var sut = new ColumnConfiguration { PropertyName = "Name" };

        // Act
        var result = sut.GetValue(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetValue_WithPropertyInfo_ReturnsPropertyValue()
    {
        // Arrange
        var entity = new SimpleEntity { Id = 1, Name = "Test" };
        var propertyInfo = typeof(SimpleEntity).GetProperty("Name");
        var sut = new ColumnConfiguration { PropertyName = "Name", PropertyInfo = propertyInfo };

        // Act
        var result = sut.GetValue(entity);

        // Assert
        result.ShouldBe("Test");
    }

    [Fact]
    public void GetValue_WithValueGetter_UsesValueGetter()
    {
        // Arrange
        var entity = new SimpleEntity { Id = 1, Name = "Test" };
        var sut = new ColumnConfiguration
        {
            PropertyName = "Name",
            PropertyInfo = typeof(SimpleEntity).GetProperty("Name")
        };

        // Use reflection to set internal ValueGetter
        var valueGetterProperty = typeof(ColumnConfiguration)
            .GetProperty("ValueGetter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        valueGetterProperty?.SetValue(sut, new Func<object, object>(o => ((SimpleEntity)o).Name.ToUpper()));

        // Act
        var result = sut.GetValue(entity);

        // Assert
        result.ShouldBe("TEST");
    }

    [Fact]
    public void GetValue_WithNullPropertyInfo_ReturnsNull()
    {
        // Arrange
        var entity = new SimpleEntity { Id = 1, Name = "Test" };
        var sut = new ColumnConfiguration { PropertyName = "Name" };

        // Act
        var result = sut.GetValue(entity);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ColumnConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ColumnConfiguration { PropertyName = "Test" };

        // Assert
        sut.Order.ShouldBe(-1);
        sut.Width.ShouldBe(-1);
        sut.Ignore.ShouldBeFalse();
        sut.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        sut.VerticalAlignment.ShouldBe(VerticalAlignment.Middle);
        sut.ConditionalStyles.ShouldNotBeNull();
        sut.ConditionalStyles.ShouldBeEmpty();
    }
}

[UnitTest("Common")]
public class ImportColumnConfigurationTests
{
    [Fact]
    public void HeaderName_GetSet_WorksAsAlias()
    {
        // Arrange
        var sut = new ImportColumnConfiguration { PropertyName = "Test" };

        // Act
        sut.HeaderName = "Custom Header";

        // Assert
        sut.SourceName.ShouldBe("Custom Header");
        sut.HeaderName.ShouldBe("Custom Header");
    }

    [Fact]
    public void SourceName_WhenSet_ReflectsInHeaderName()
    {
        // Arrange
        var sut = new ImportColumnConfiguration { PropertyName = "Test" };

        // Act
        sut.SourceName = "Source Column";

        // Assert
        sut.HeaderName.ShouldBe("Source Column");
    }

    [Fact]
    public void ImportColumnConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ImportColumnConfiguration { PropertyName = "Test" };

        // Assert
        sut.SourceIndex.ShouldBe(-1);
        sut.Order.ShouldBe(-1);
        sut.Width.ShouldBe(-1);
        sut.Ignore.ShouldBeFalse();
        sut.IsRequired.ShouldBeFalse();
        sut.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        sut.VerticalAlignment.ShouldBe(VerticalAlignment.Middle);
    }
}

[UnitTest("Common")]
public class ExportConfigurationTests
{
    [Fact]
    public void ExportConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ExportConfiguration();

        // Assert
        sut.Columns.ShouldNotBeNull();
        sut.Columns.ShouldBeEmpty();
        sut.HeaderRows.ShouldNotBeNull();
        sut.HeaderRows.ShouldBeEmpty();
        sut.FooterRows.ShouldNotBeNull();
        sut.FooterRows.ShouldBeEmpty();
        sut.IncludeHeaders.ShouldBeTrue();
        sut.Culture.ShouldBe(System.Globalization.CultureInfo.InvariantCulture);
    }
}

[UnitTest("Common")]
public class ImportConfigurationTests
{
    [Fact]
    public void ImportConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var sut = new ImportConfiguration();

        // Assert
        sut.Columns.ShouldNotBeNull();
        sut.Columns.ShouldBeEmpty();
        sut.SheetIndex.ShouldBe(-1);
        sut.HeaderRowIndex.ShouldBe(0);
        sut.SkipRows.ShouldBe(0);
        sut.ValidationBehavior.ShouldBe(ImportValidationBehavior.CollectErrors);
        sut.MaxErrors.ShouldBeNull();
        sut.Culture.ShouldBe(System.Globalization.CultureInfo.InvariantCulture);
    }
}
