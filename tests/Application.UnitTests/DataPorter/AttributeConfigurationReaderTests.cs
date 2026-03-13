// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class AttributeConfigurationReaderTests
{
    [Fact]
    public void ReadExportConfiguration_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.ReadExportConfiguration(null));
    }

    [Fact]
    public void ReadExportConfiguration_WithSimpleType_ReturnsConfiguration()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<SimpleEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.SourceType.ShouldBe(typeof(SimpleEntity));
        result.Columns.Count.ShouldBe(2);
    }

    [Fact]
    public void ReadExportConfiguration_WithSheetAttribute_ReadsSheetName()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<EntityWithSheetAttribute>();

        // Assert
        result.SheetName.ShouldBe("TestSheet");
    }

    [Fact]
    public void ReadExportConfiguration_WithoutSheetAttribute_UsesTypeName()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<SimpleEntity>();

        // Assert
        result.SheetName.ShouldBe(nameof(SimpleEntity));
    }

    [Fact]
    public void ReadExportConfiguration_WithColumnAttribute_ReadsColumnSettings()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<EntityWithColumnAttributes>();

        // Assert
        var nameColumn = result.Columns.FirstOrDefault(c => c.PropertyName == "Name");
        nameColumn.ShouldNotBeNull();
        nameColumn.HeaderName.ShouldBe("Display Name");
        nameColumn.Order.ShouldBe(1);
        nameColumn.Format.ShouldBe("0.00");
        nameColumn.Width.ShouldBe(100.0);
    }

    [Fact]
    public void ReadExportConfiguration_WithIgnoreAttribute_ExcludesProperty()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<EntityWithIgnoreAttribute>();

        // Assert
        result.Columns.ShouldNotContain(c => c.PropertyName == "IgnoredProperty");
    }

    [Fact]
    public void ReadExportConfiguration_WithIgnoreExportOnlyAttribute_IncludesForImportExcludesForExport()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var exportConfig = sut.ReadExportConfiguration<EntityWithIgnoreExportOnly>();
        var importConfig = sut.ReadImportConfiguration<EntityWithIgnoreExportOnly>();

        // Assert
        exportConfig.Columns.ShouldNotContain(c => c.PropertyName == "ExportIgnored");
        importConfig.Columns.ShouldContain(c => c.PropertyName == "ExportIgnored");
    }

    [Fact]
    public void ReadExportConfiguration_WithExportFalse_ExcludesProperty()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<EntityWithExportDisabled>();

        // Assert
        result.Columns.ShouldNotContain(c => c.PropertyName == "NoExport");
    }

    [Fact]
    public void ReadExportConfiguration_OrdersColumnsByOrder()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadExportConfiguration<EntityWithOrderedColumns>();

        // Assert
        result.Columns[0].PropertyName.ShouldBe("Third");
        result.Columns[1].PropertyName.ShouldBe("First");
        result.Columns[2].PropertyName.ShouldBe("Second");
    }

    [Fact]
    public void ReadImportConfiguration_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.ReadImportConfiguration(null));
    }

    [Fact]
    public void ReadImportConfiguration_WithSimpleType_ReturnsConfiguration()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<SimpleEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.TargetType.ShouldBe(typeof(SimpleEntity));
        result.Columns.Count.ShouldBe(2);
    }

    [Fact]
    public void ReadImportConfiguration_WithSheetAttribute_ReadsSheetSettings()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithSheetIndexAttribute>();

        // Assert
        result.SheetName.ShouldBe("ImportSheet");
        result.SheetIndex.ShouldBe(2);
    }

    [Fact]
    public void ReadImportConfiguration_WithRequiredColumn_SetsIsRequired()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithRequiredColumn>();

        // Assert
        var requiredColumn = result.Columns.FirstOrDefault(c => c.PropertyName == "RequiredField");
        requiredColumn.ShouldNotBeNull();
        requiredColumn.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void ReadImportConfiguration_WithValidationAttributes_CreatesValidators()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithValidation>();

        // Assert
        var emailColumn = result.Columns.FirstOrDefault(c => c.PropertyName == "Email");
        emailColumn.ShouldNotBeNull();
        emailColumn.Validators.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ReadImportConfiguration_WithImportDisabled_ExcludesProperty()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithImportDisabled>();

        // Assert
        result.Columns.ShouldNotContain(c => c.PropertyName == "NoImport");
    }

    [Fact]
    public void ReadImportConfiguration_ReadOnlyProperties_AreExcluded()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithReadOnlyProperty>();

        // Assert
        result.Columns.ShouldNotContain(c => c.PropertyName == "ReadOnlyProperty");
    }

    [Fact]
    public void ReadExportConfiguration_CachesResults()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result1 = sut.ReadExportConfiguration<SimpleEntity>();
        var result2 = sut.ReadExportConfiguration<SimpleEntity>();

        // Assert
        ReferenceEquals(result1, result2).ShouldBeTrue();
    }

    [Fact]
    public void ReadImportConfiguration_CachesResults()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result1 = sut.ReadImportConfiguration<SimpleEntity>();
        var result2 = sut.ReadImportConfiguration<SimpleEntity>();

        // Assert
        ReferenceEquals(result1, result2).ShouldBeTrue();
    }

    [Fact]
    public void ReadImportConfiguration_WithConverterAttribute_ResolvesConverter()
    {
        // Arrange
        var sut = new AttributeConfigurationReader();

        // Act
        var result = sut.ReadImportConfiguration<EntityWithConverter>();

        // Assert
        var column = result.Columns.FirstOrDefault(c => c.PropertyName == "IsActive");
        column.ShouldNotBeNull();
        column.Converter.ShouldNotBeNull();
        column.Converter.ShouldBeOfType<BooleanYesNoConverter>();
    }
}
