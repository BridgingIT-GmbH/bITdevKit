// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.IO.Compression;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class ConfigurationMergerTests
{
    private readonly ProfileRegistry profileRegistry = new();
    private readonly AttributeConfigurationReader attributeReader = new();

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

    [Fact]
    public void BuildExportConfiguration_WithMergingProfile_MergesColumnsAndFiltersIgnoredColumns()
    {
        // Arrange
        this.profileRegistry.RegisterExportProfile(new MergingExportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildExportConfiguration<EntityWithColumnAttributes>();

        // Assert
        result.Columns.Count.ShouldBe(1);

        var column = result.Columns[0];
        column.PropertyName.ShouldBe(nameof(EntityWithColumnAttributes.Name));
        column.HeaderName.ShouldBe("Profile Name");
        column.Order.ShouldBe(2);
        column.Format.ShouldBe("0.00");
        column.Width.ShouldBe(150);
        column.NullValue.ShouldBe("(missing)");
        column.ConditionalStyles.Count.ShouldBe(1);
        result.HeaderRows.Count.ShouldBe(1);
        result.FooterRows.Count.ShouldBe(1);
    }

    [Fact]
    public void BuildExportConfiguration_WithMergingProfile_ReturnsIndependentConfigurationsAcrossCalls()
    {
        // Arrange
        this.profileRegistry.RegisterExportProfile(new MergingExportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var first = sut.BuildExportConfiguration<EntityWithColumnAttributes>();
        first.Columns[0].HeaderName = "Mutated";
        var second = sut.BuildExportConfiguration<EntityWithColumnAttributes>();

        // Assert
        second.Columns[0].HeaderName.ShouldBe("Profile Name");
    }

    [Fact]
    public void BuildImportConfiguration_WithMergingProfile_MergesColumnsAndPreservesRequiredFlags()
    {
        // Arrange
        this.profileRegistry.RegisterImportProfile(new MergingImportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildImportConfiguration<EntityWithRequiredColumn>();

        // Assert
        var requiredColumn = result.Columns.Single(column => column.PropertyName == nameof(EntityWithRequiredColumn.RequiredField));
        requiredColumn.SourceName.ShouldBe("Profile Required");
        requiredColumn.SourceIndex.ShouldBe(4);
        requiredColumn.Order.ShouldBe(1);
        requiredColumn.IsRequired.ShouldBeTrue();
        requiredColumn.RequiredMessage.ShouldBe("Profile required message");
        requiredColumn.Validators.Count.ShouldBe(1);
        result.SheetIndex.ShouldBe(3);
        result.HeaderRowIndex.ShouldBe(0);
        result.SkipRows.ShouldBe(0);
        result.ValidationBehavior.ShouldBe(ImportValidationBehavior.CollectErrors);
    }

    [Fact]
    public void BuildImportConfiguration_WithFactoryProfile_UsesFactoryFromProfile()
    {
        // Arrange
        this.profileRegistry.RegisterImportProfile(new MergingImportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildImportConfiguration<EntityWithRequiredColumn>();
        var created = (EntityWithRequiredColumn)result.Factory();

        // Assert
        created.Id.ShouldBe(99);
    }

    [Fact]
    public void BuildTemplateConfiguration_WithProfileOnlyMetadata_UsesImportAndExportFallbacks()
    {
        // Arrange
        this.profileRegistry.RegisterExportProfile(new TemplateMergeExportProfile());
        this.profileRegistry.RegisterImportProfile(new TemplateMergeImportProfile());
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildTemplateConfiguration<TemplateMergeEntity>(new TemplateOptions { UseAttributes = false });

        // Assert
        result.Fields.Count.ShouldBe(2);
        result.Fields[0].PropertyName.ShouldBe(nameof(TemplateMergeEntity.Id));
        result.Fields[0].HeaderName.ShouldBe("Identifier");

        result.Fields[1].PropertyName.ShouldBe(nameof(TemplateMergeEntity.Name));
        result.Fields[1].HeaderName.ShouldBe("Profile Display Name");
        result.Fields[1].Order.ShouldBe(3);
        result.Fields[1].Format.ShouldBe("N0");
        result.Fields[1].ValidationHints.Count.ShouldBe(1);
        result.Fields[1].ValidationHints[0].ShouldBe("Too long");
    }

    [Fact]
    public void BuildTemplateConfiguration_WithoutImportOrExportSheetName_UsesTypeName()
    {
        // Arrange
        var sut = new ConfigurationMerger(this.profileRegistry, this.attributeReader);

        // Act
        var result = sut.BuildTemplateConfiguration<SimpleEntity>(new TemplateOptions { UseAttributes = false });

        // Assert
        result.SheetName.ShouldBe(nameof(SimpleEntity));
    }

    private sealed class MergingExportProfile : IExportProfile<EntityWithColumnAttributes>
    {
        public Type SourceType => typeof(EntityWithColumnAttributes);

        public IReadOnlyList<ColumnConfiguration> Columns { get; } =
        [
            new()
            {
                PropertyName = nameof(EntityWithColumnAttributes.Name),
                HeaderName = "Profile Name",
                Order = 2,
                Width = 150,
                NullValue = "(missing)",
                ConditionalStyles =
                [
                    new ConditionalStyle
                    {
                        Condition = _ => true,
                        IsBold = true
                    }
                ]
            },
            new()
            {
                PropertyName = "Ignored",
                HeaderName = "Ignored",
                Ignore = true,
                Order = 0
            }
        ];

        public string SheetName => string.Empty;

        public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [new HeaderRowConfiguration { Content = "Header" }];

        public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [new FooterRowConfiguration { Content = "Footer" }];
    }

    private sealed class MergingImportProfile : IImportProfile<EntityWithRequiredColumn>
    {
        public Type TargetType => typeof(EntityWithRequiredColumn);

        public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
        [
            new()
            {
                PropertyName = nameof(EntityWithRequiredColumn.RequiredField),
                SourceName = "Profile Required",
                SourceIndex = 4,
                Order = 1,
                IsRequired = false,
                RequiredMessage = "Profile required message",
                Validators = [new ColumnValidator { Validate = _ => false, ErrorMessage = "Invalid required field" }]
            },
            new()
            {
                PropertyName = nameof(EntityWithRequiredColumn.Id),
                Ignore = true,
                Order = 0
            }
        ];

        public string SheetName => string.Empty;

        public int SheetIndex => 3;

        public int HeaderRowIndex => 2;

        public int SkipRows => 1;

        public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.StopImport;

        public Func<EntityWithRequiredColumn> Factory => () => new EntityWithRequiredColumn { Id = 99, RequiredField = "from profile" };

        Func<object> IImportProfile.Factory => () => new EntityWithRequiredColumn { Id = 99, RequiredField = "from profile" };
    }

    private sealed class TemplateMergeEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    private sealed class TemplateMergeExportProfile : IExportProfile<TemplateMergeEntity>
    {
        public Type SourceType => typeof(TemplateMergeEntity);

        public IReadOnlyList<ColumnConfiguration> Columns { get; } =
        [
            new()
            {
                PropertyName = nameof(TemplateMergeEntity.Id),
                HeaderName = "Identifier",
                Order = 0
            },
            new()
            {
                PropertyName = nameof(TemplateMergeEntity.Name),
                HeaderName = "Profile Display Name",
                Order = 3,
                Format = "N0"
            }
        ];

        public string SheetName => string.Empty;

        public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [];

        public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [];
    }

    private sealed class TemplateMergeImportProfile : IImportProfile<TemplateMergeEntity>
    {
        public Type TargetType => typeof(TemplateMergeEntity);

        public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
        [
            new()
            {
                PropertyName = nameof(TemplateMergeEntity.Name),
                Order = -1,
                Validators =
                [
                    new ColumnValidator { Validate = _ => true, ErrorMessage = "Too long" },
                    new ColumnValidator { Validate = _ => true, ErrorMessage = "too long" },
                    new ColumnValidator { Validate = _ => true, ErrorMessage = "  " }
                ]
            },
            new()
            {
                PropertyName = nameof(TemplateMergeEntity.Id),
                SourceName = "Identifier",
                Order = 0
            }
        ];

        public string SheetName => string.Empty;

        public int SheetIndex => -1;

        public int HeaderRowIndex => 0;

        public int SkipRows => 0;

        public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.CollectErrors;

        public Func<TemplateMergeEntity> Factory => () => new TemplateMergeEntity();

        Func<object> IImportProfile.Factory => () => new TemplateMergeEntity();
    }
}
