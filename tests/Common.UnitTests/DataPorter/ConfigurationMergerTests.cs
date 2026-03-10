// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class ConfigurationMergerTests
{
    private readonly ProfileRegistry profileRegistry;
    private readonly AttributeConfigurationReader attributeReader;

    public ConfigurationMergerTests()
    {
        this.profileRegistry = new ProfileRegistry();
        this.attributeReader = new AttributeConfigurationReader();
    }

    [Fact]
    public void BuildExportConfiguration_WithNullOptions_UsesDefaults()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildExportConfiguration<SimpleEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.SourceType.ShouldBe(typeof(SimpleEntity));
        result.IncludeHeaders.ShouldBeTrue();
    }

    [Fact]
    public void BuildExportConfiguration_WithUseAttributesFalse_DoesNotReadAttributes()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ExportOptions { UseAttributes = false };

        // Act
        var result = sut.BuildExportConfiguration<EntityWithColumnAttributes>(options);

        // Assert
        result.Columns.ShouldBeEmpty();
    }

    [Fact]
    public void BuildExportConfiguration_WithSheetNameOption_OverridesAttributeSheetName()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ExportOptions { SheetName = "CustomSheetName" };

        // Act
        var result = sut.BuildExportConfiguration<EntityWithSheetAttribute>(options);

        // Assert
        result.SheetName.ShouldBe("CustomSheetName");
    }

    [Fact]
    public void BuildExportConfiguration_WithCultureOption_SetsCulture()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var germanCulture = new System.Globalization.CultureInfo("de-DE");
        var options = new ExportOptions { Culture = germanCulture };

        // Act
        var result = sut.BuildExportConfiguration<SimpleEntity>(options);

        // Assert
        result.Culture.ShouldBe(germanCulture);
    }

    [Fact]
    public void BuildExportConfiguration_WithIncludeHeadersFalse_DisablesHeaders()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ExportOptions { IncludeHeaders = false };

        // Act
        var result = sut.BuildExportConfiguration<SimpleEntity>(options);

        // Assert
        result.IncludeHeaders.ShouldBeFalse();
    }

    [Fact]
    public void BuildExportConfiguration_WithProfile_MergesProfileSettings()
    {
        // Arrange
        var profile = new TestExportProfile();
        this.profileRegistry.RegisterExportProfile(profile);
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildExportConfiguration<TestExportEntity>();

        // Assert
        result.SheetName.ShouldBe("TestExportSheet");
    }

    [Fact]
    public void BuildImportConfiguration_WithNullOptions_UsesDefaults()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>();

        // Assert
        result.ShouldNotBeNull();
        result.TargetType.ShouldBe(typeof(SimpleEntity));
        result.ValidationBehavior.ShouldBe(ImportValidationBehavior.CollectErrors);
    }

    [Fact]
    public void BuildImportConfiguration_WithUseAttributesFalse_DoesNotReadAttributes()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { UseAttributes = false };

        // Act
        var result = sut.BuildImportConfiguration<EntityWithColumnAttributes>(options);

        // Assert
        result.Columns.ShouldBeEmpty();
    }

    [Fact]
    public void BuildImportConfiguration_WithSheetNameOption_OverridesAttributeSheetName()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { SheetName = "CustomImportSheet" };

        // Act
        var result = sut.BuildImportConfiguration<EntityWithSheetIndexAttribute>(options);

        // Assert
        result.SheetName.ShouldBe("CustomImportSheet");
    }

    [Fact]
    public void BuildImportConfiguration_WithSheetIndexOption_OverridesAttributeSheetIndex()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { SheetIndex = 5 };

        // Act
        var result = sut.BuildImportConfiguration<EntityWithSheetIndexAttribute>(options);

        // Assert
        result.SheetIndex.ShouldBe(5);
    }

    [Fact]
    public void BuildImportConfiguration_WithHeaderRowIndex_SetsHeaderRowIndex()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { HeaderRowIndex = 3 };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.HeaderRowIndex.ShouldBe(3);
    }

    [Fact]
    public void BuildImportConfiguration_WithSkipRows_SetsSkipRows()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { SkipRows = 2 };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.SkipRows.ShouldBe(2);
    }

    [Fact]
    public void BuildImportConfiguration_WithValidationBehavior_SetsValidationBehavior()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { ValidationBehavior = ImportValidationBehavior.StopImport };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.ValidationBehavior.ShouldBe(ImportValidationBehavior.StopImport);
    }

    [Fact]
    public void BuildImportConfiguration_WithProfile_MergesProfileSettings()
    {
        // Arrange
        var profile = new TestImportProfile();
        this.profileRegistry.RegisterImportProfile(profile);
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildImportConfiguration<TestImportEntity>();

        // Assert
        result.SheetName.ShouldBe("TestImportSheet");
    }

    [Fact]
    public void BuildExportConfiguration_ByType_ReturnsConfiguration()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildExportConfiguration(typeof(SimpleEntity));

        // Assert
        result.ShouldNotBeNull();
        result.SourceType.ShouldBe(typeof(SimpleEntity));
    }

    [Fact]
    public void BuildImportConfiguration_ByType_ReturnsConfiguration()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildImportConfiguration(typeof(SimpleEntity));

        // Assert
        result.ShouldNotBeNull();
        result.TargetType.ShouldBe(typeof(SimpleEntity));
    }
}
