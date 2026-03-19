// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;
using System.IO.Compression;

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
    public void BuildExportConfiguration_WithProgress_SetsProgress()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var progress = new TestProgress<ExportProgressReport>();
        var options = new ExportOptions { Progress = progress };

        // Act
        var result = sut.BuildExportConfiguration<SimpleEntity>(options);

        // Assert
        result.Progress.ShouldBeSameAs(progress);
    }

    [Fact]
    public void BuildExportConfiguration_WithCompression_SetsCompression()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ExportOptions
        {
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.Zip, CompressionLevel = CompressionLevel.Fastest, ZipEntryName = "payload.csv" }
        };

        // Act
        var result = sut.BuildExportConfiguration<SimpleEntity>(options);

        // Assert
        result.Compression.Kind.ShouldBe(PayloadCompressionKind.Zip);
        result.Compression.CompressionLevel.ShouldBe(CompressionLevel.Fastest);
        result.Compression.ZipEntryName.ShouldBe("payload.csv");
    }

    [Fact]
    public void BuildExportConfiguration_WithDifferentSheetNames_ReturnsIndependentConfigurations()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var first = sut.BuildExportConfiguration<SimpleEntity>(new ExportOptions { SheetName = "Sheet1" });
        var second = sut.BuildExportConfiguration<SimpleEntity>(new ExportOptions { SheetName = "Sheet2" });

        // Assert
        first.ShouldNotBeSameAs(second);
        first.SheetName.ShouldBe("Sheet1");
        second.SheetName.ShouldBe("Sheet2");
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
    public void BuildImportConfiguration_WithMaxErrors_SetsMaxErrors()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions { MaxErrors = 3 };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.MaxErrors.ShouldBe(3);
    }

    [Fact]
    public void BuildImportConfiguration_WithProgress_SetsProgress()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var progress = new TestProgress<ImportProgressReport>();
        var options = new ImportOptions { Progress = progress };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.Progress.ShouldBeSameAs(progress);
    }

    [Fact]
    public void BuildImportConfiguration_WithCompression_SetsCompression()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var options = new ImportOptions
        {
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.GZip, CompressionLevel = CompressionLevel.SmallestSize }
        };

        // Act
        var result = sut.BuildImportConfiguration<SimpleEntity>(options);

        // Assert
        result.Compression.Kind.ShouldBe(PayloadCompressionKind.GZip);
        result.Compression.CompressionLevel.ShouldBe(CompressionLevel.SmallestSize);
    }

    [Fact]
    public void BuildImportConfiguration_WithDifferentSheetNames_ReturnsIndependentConfigurations()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var first = sut.BuildImportConfiguration<SimpleEntity>(new ImportOptions { SheetName = "Sheet1" });
        var second = sut.BuildImportConfiguration<SimpleEntity>(new ImportOptions { SheetName = "Sheet2" });

        // Assert
        first.ShouldNotBeSameAs(second);
        first.SheetName.ShouldBe("Sheet1");
        second.SheetName.ShouldBe("Sheet2");
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

    [Fact]
    public void BuildTemplateConfiguration_WithImportMetadata_PrefersImportConfiguration()
    {
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        var result = sut.BuildTemplateConfiguration<EntityWithRequiredColumn>();

        result.TargetType.ShouldBe(typeof(EntityWithRequiredColumn));
        result.Fields.Count.ShouldBe(2);
        result.Fields.ShouldContain(field => field.PropertyName == nameof(EntityWithRequiredColumn.RequiredField) && field.IsRequired);
    }

    [Fact]
    public void BuildTemplateConfiguration_WithNoImportMetadata_FallsBackToExportConfiguration()
    {
        this.profileRegistry.RegisterExportProfile(new TestExportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        var result = sut.BuildTemplateConfiguration(typeof(TestExportEntity), new TemplateOptions { UseAttributes = false });

        result.Fields.Count.ShouldBe(3);
        result.Fields[0].HeaderName.ShouldBe("Identifier");
        result.Fields[2].Format.ShouldBe("C2");
    }

    [Fact]
    public void BuildTemplateConfiguration_WithOptions_MapsTemplateSettings()
    {
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
        var culture = new System.Globalization.CultureInfo("nl-NL");

        var result = sut.BuildTemplateConfiguration<SimpleEntity>(new TemplateOptions
        {
            Culture = culture,
            SheetName = "TemplateSheet",
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.GZip },
            IncludeHints = false,
            AnnotationStyle = TemplateAnnotationStyle.StructureOnly,
            SampleItemCount = 2,
            UseMetadataWrapper = false,
            ProviderOptions = new Dictionary<string, object> { ["strict"] = true }
        });

        result.Culture.ShouldBe(culture);
        result.SheetName.ShouldBe("TemplateSheet");
        result.Compression.Kind.ShouldBe(PayloadCompressionKind.GZip);
        result.IncludeHints.ShouldBeFalse();
        result.AnnotationStyle.ShouldBe(TemplateAnnotationStyle.StructureOnly);
        result.SampleItemCount.ShouldBe(2);
        result.UseMetadataWrapper.ShouldBeFalse();
        result.ProviderOptions["strict"].ShouldBe(true);
    }
}
