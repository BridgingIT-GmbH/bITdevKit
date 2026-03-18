// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Text;
using System.Globalization;
using System.IO.Compression;
using BridgingIT.DevKit.Application.DataPorter;
using ClosedXML.Excel;

[UnitTest("Common")]
public class DataPorterServiceImportTests
{
    private readonly ProfileRegistry profileRegistry;
    private readonly AttributeConfigurationReader attributeReader;
    private readonly ConfigurationMerger configurationMerger;

    public static TheoryData<Format> TabularFormats => [Format.Csv, Format.Excel];

    public static TheoryData<Format> AggregateFormats => [Format.Csv, Format.Excel, Format.Json, Format.Xml];

    public static TheoryData<Format> CultureSpecificFormats => [Format.Excel, Format.Json, Format.Xml];

    public DataPorterServiceImportTests()
    {
        this.profileRegistry = new ProfileRegistry();
        this.attributeReader = new AttributeConfigurationReader();
        this.configurationMerger = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
    }

    [Fact]
    public async Task ImportAsync_WithNullStream_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>((Stream)null);

        // Assert
        result.ShouldBeFailure();
        result.HasError<DataPorterError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_WithNoProviders_ReturnsFailure()
    {
        // Arrange
        var sut = new DataPorterService([], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream);

        // Assert
        result.ShouldBeFailure();
        result.HasError<FormatNotSupportedError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider(Format.Csv);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var options = new ImportOptions { Format = Format.Excel };

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, options);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportAsync_WithProviderThatDoesNotSupportImport_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportOnlyProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportAsync_WithValidStreamAndProvider_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ImportAsync_WithCancellationToken_ReturnsFailureOnCancellation()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider(throwOnCancel: true);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, cancellationToken: cts.Token);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithNullData_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>((byte[])null);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithEmptyData_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>([]);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new byte[] { 1, 2, 3, 4 };

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(data);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithGZipCompression_ReadsCompressedBytes()
    {
        // Arrange
        var sut = new DataPorterService([new CsvDataPorterProvider()], this.configurationMerger);
        var csvBytes = Encoding.UTF8.GetBytes("Id,Name\r\n1,Test\r\n");
        var compressedBytes = await CompressionHelper.CompressAsync(csvBytes);

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(compressedBytes, new ImportOptions
        {
            Format = Format.Csv,
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.GZip }
        });

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Name.ShouldBe("Test");
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithZipCompression_ReadsCompressedBytes()
    {
        // Arrange
        var sut = new DataPorterService([new CsvDataPorterProvider()], this.configurationMerger);
        var archiveBytes = await CreateZipBytesAsync("payload.csv", "Id,Name\r\n1,Test\r\n");

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(archiveBytes, new ImportOptions
        {
            Format = Format.Csv,
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.Zip, ZipEntryName = "payload.csv" }
        });

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Name.ShouldBe("Test");
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithWrongCompression_ReturnsFailure()
    {
        // Arrange
        var sut = new DataPorterService([new CsvDataPorterProvider()], this.configurationMerger);
        var csvBytes = Encoding.UTF8.GetBytes("Id,Name\r\n1,Test\r\n");

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(csvBytes, new ImportOptions
        {
            Format = Format.Csv,
            Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.GZip }
        });

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportFromBytesAsync_WithProgress_ForwardsProgressReports()
    {
        // Arrange
        var sut = new DataPorterService([new ExcelDataPorterProvider()], this.configurationMerger);
        var progress = new TestProgress<ImportProgressReport>();
        var data = CreateExcelStream(
            [nameof(SimpleEntity.Id), nameof(SimpleEntity.Name)],
            Enumerable.Range(1, 30).Select(i => new object[] { i, $"Item {i}" }).ToArray()).ToArray();

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(data, new ImportOptions { Format = Format.Excel, Progress = progress });

        // Assert
        result.ShouldBeSuccess();
        progress.Reports.Count.ShouldBeGreaterThanOrEqualTo(3);
        progress.Reports[0].Messages.ShouldContain("Starting import");
        progress.Reports.ShouldContain(report => !report.IsCompleted && report.ProcessedRows == 25);
        progress.Reports[^1].IsCompleted.ShouldBeTrue();
        progress.Reports[^1].ProcessedRows.ShouldBe(30);
    }

    [Fact]
    public async Task ValidateAsync_WithNullStream_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ValidateAsync<SimpleEntity>(null);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ValidateAsync_WithValidStream_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await sut.ValidateAsync<SimpleEntity>(stream);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ValidateAsync_WithNoProviders_ReturnsFailure()
    {
        // Arrange
        var sut = new DataPorterService([], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var result = await sut.ValidateAsync<SimpleEntity>(stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ImportAsync_WithImportOptions_UsesProvidedOptions()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider(Format.Csv);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var options = new ImportOptions
        {
            Format = Format.Csv,
            SheetName = "CustomSheet",
            HeaderRowIndex = 1,
            SkipRows = 2
        };

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, options);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ImportStreamAsync_WithNullStream_YieldsFailure()
    {
        // Arrange
        var mockProvider = CreateMockStreamingProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(null))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
    }

    [Fact]
    public async Task ImportStreamAsync_WithNoProviders_YieldsFailure()
    {
        // Arrange
        var sut = new DataPorterService([], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
    }

    [Fact]
    public async Task ImportStreamAsync_WithNonStreamingProvider_YieldsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
    }

    [Fact]
    public async Task ImportStreamAsync_WithValidStreamingProvider_YieldsResults()
    {
        // Arrange
        var mockProvider = CreateMockStreamingProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(2);
        results.All(r => r.IsSuccess).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(TabularFormats))]
    public async Task ImportAsync_WithMissingRequiredHeader_ReturnsSchemaError(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMissingRequiredHeaderStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var result = await sut.ImportAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.ShouldBeEmpty();
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.Errors[0].Column.ShouldBe(nameof(EntityWithRequiredColumn.RequiredField));
        result.Value.Errors[0].Message.ShouldContain("Required");
    }

    [Theory]
    [MemberData(nameof(TabularFormats))]
    public async Task ValidateAsync_WithMissingRequiredHeader_ReturnsInvalidResult(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMissingRequiredHeaderStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var result = await sut.ValidateAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.IsValid.ShouldBeFalse();
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.Errors[0].Column.ShouldBe(nameof(EntityWithRequiredColumn.RequiredField));
    }

    [Theory]
    [MemberData(nameof(TabularFormats))]
    public async Task ImportStreamAsync_WithMissingRequiredHeader_YieldsFailure(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMissingRequiredHeaderStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var results = new List<Result<EntityWithRequiredColumn>>();
        await foreach (var result in sut.ImportAsyncEnumerable<EntityWithRequiredColumn>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
        results[0].Errors[0].Message.ShouldContain("Required");
    }

    [Theory]
    [MemberData(nameof(TabularFormats))]
    public async Task ImportAsync_WithMissingOptionalHeader_ContinuesImport(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMissingOptionalHeaderStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Errors.ShouldBeEmpty();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Id.ShouldBe(1);
        result.Value.Data[0].Name.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AggregateFormats))]
    public async Task ImportAsync_WithCollectErrors_SkipsInvalidRowsAndKeepsValidRows(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMixedValidityStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var result = await sut.ImportAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Id.ShouldBe(1);
        result.Value.Data[0].RequiredField.ShouldBe("value");
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.Errors[0].Column.ShouldBe(nameof(EntityWithRequiredColumn.RequiredField));
    }

    [Theory]
    [MemberData(nameof(AggregateFormats))]
    public async Task ImportAsync_WithMaxErrors_StopsAfterConfiguredErrorLimit(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMultipleInvalidRowsStream(format);
        var options = new ImportOptions
        {
            Format = format,
            MaxErrors = 1
        };

        // Act
        var result = await sut.ImportAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Id.ShouldBe(1);
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.TotalRows.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(AggregateFormats))]
    public async Task ValidateAsync_WithMaxErrors_StopsAfterConfiguredErrorLimit(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMultipleInvalidRowsStream(format);
        var options = new ImportOptions
        {
            Format = format,
            MaxErrors = 1
        };

        // Act
        var result = await sut.ValidateAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.IsValid.ShouldBeFalse();
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.TotalRows.ShouldBe(2);
        result.Value.ValidRows.ShouldBe(1);
    }

    [Fact]
    public async Task ImportAsync_WithCsvTypedConversionError_DoesNotReturnPartialAggregate()
    {
        // Arrange
        var sut = new DataPorterService([new CsvTypedDataPorterProvider()], this.configurationMerger);
        await using var stream = CreateInvalidCsvTypedStream();
        var options = new ImportOptions { Format = Format.CsvTyped };

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.ShouldBeEmpty();
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.Errors[0].Column.ShouldBe(nameof(SimpleEntity.Id));
    }

    [Fact]
    public async Task ImportAsync_WithProgress_ReportsKnownTotalAndCompletion()
    {
        // Arrange
        var sut = new DataPorterService([new ExcelDataPorterProvider()], this.configurationMerger);
        var progress = new TestProgress<ImportProgressReport>();
        await using var stream = CreateExcelStream(
            [nameof(SimpleEntity.Id), nameof(SimpleEntity.Name)],
            Enumerable.Range(1, 30).Select(i => new object[] { i, $"Item {i}" }).ToArray());

        // Act
        var result = await sut.ImportAsync<SimpleEntity>(stream, new ImportOptions { Format = Format.Excel, Progress = progress });

        // Assert
        result.ShouldBeSuccess();
        progress.Reports.Count.ShouldBeGreaterThanOrEqualTo(3);
        progress.Reports[0].Messages.ShouldContain("Starting import");
        progress.Reports.ShouldContain(report => !report.IsCompleted && report.ProcessedRows == 25 && report.TotalRows == 30 && report.PercentageComplete.HasValue);
        progress.Reports[^1].IsCompleted.ShouldBeTrue();
        progress.Reports[^1].ProcessedRows.ShouldBe(30);
        progress.Reports[^1].SuccessfulRows.ShouldBe(30);
    }

    [Theory]
    [MemberData(nameof(CultureSpecificFormats))]
    public async Task ImportAsync_WithCultureSpecificDecimal_UsesConfiguredCulture(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateLocalizedDecimalStream(format);
        var options = new ImportOptions
        {
            Format = format,
            Culture = CultureInfo.GetCultureInfo("de-DE")
        };

        // Act
        var result = await sut.ImportAsync<EntityWithDecimalAmount>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Errors.ShouldBeEmpty();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Amount.ShouldBe(1.23m);
    }

    [Fact]
    public async Task ImportAsync_WithCultureSpecificCsvDecimal_UsesConfiguredCulture()
    {
        // Arrange
        var provider = new CsvDataPorterProvider(new CsvConfiguration { Delimiter = ";" });
        var sut = new DataPorterService([provider], this.configurationMerger);
        await using var stream = CreateLocalizedDecimalStream(Format.Csv);
        var options = new ImportOptions
        {
            Format = Format.Csv,
            Culture = CultureInfo.GetCultureInfo("de-DE")
        };

        // Act
        var result = await sut.ImportAsync<EntityWithDecimalAmount>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Errors.ShouldBeEmpty();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Amount.ShouldBe(1.23m);
    }

    [Fact]
    public async Task ImportAsync_WithCsvHeaderRowIndex_UsesConfiguredHeaderRow()
    {
        // Arrange
        var sut = new DataPorterService([new CsvDataPorterProvider()], this.configurationMerger);
        await using var stream = CreateCsvWithPreambleStream();
        var options = new ImportOptions
        {
            Format = Format.Csv,
            HeaderRowIndex = 1
        };

        // Act
        var result = await sut.ImportAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Errors.ShouldBeEmpty();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Id.ShouldBe(1);
        result.Value.Data[0].RequiredField.ShouldBe("value");
    }

    [Fact]
    public async Task ImportAsync_WithCsvTypedMissingRequiredValue_ReturnsErrorAndNoData()
    {
        // Arrange
        var sut = new DataPorterService([new CsvTypedDataPorterProvider()], this.configurationMerger);
        await using var stream = CreateCsvTypedMissingRequiredStream();
        var options = new ImportOptions { Format = Format.CsvTyped };

        // Act
        var result = await sut.ImportAsync<EntityWithRequiredColumn>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.ShouldBeEmpty();
        result.Value.Errors.Count.ShouldBe(1);
        result.Value.Errors[0].Column.ShouldBe(nameof(EntityWithRequiredColumn.RequiredField));
        result.Value.Errors[0].Message.ShouldContain("required");
    }

    [Fact]
    public async Task ImportAsync_WithCsvTypedInvalidValidatorValue_ReturnsErrorAndNoData()
    {
        // Arrange
        var sut = new DataPorterService([new CsvTypedDataPorterProvider()], this.configurationMerger);
        await using var stream = CreateCsvTypedInvalidValidatorStream();
        var options = new ImportOptions { Format = Format.CsvTyped };

        // Act
        var result = await sut.ImportAsync<EntityWithValidation>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.ShouldBeEmpty();
        result.Value.Errors.Count.ShouldBe(2);
        result.Value.Errors.Select(e => e.Column).ShouldContain(nameof(EntityWithValidation.Email));
        result.Value.Errors.Select(e => e.Column).ShouldContain(nameof(EntityWithValidation.MinLengthField));
    }

    [Fact]
    public async Task ImportAsync_WithCsvTypedMaxErrors_StopsAtConfiguredLimit()
    {
        // Arrange
        var sut = new DataPorterService([new CsvTypedDataPorterProvider()], this.configurationMerger);
        await using var stream = CreateCsvTypedInvalidValidatorStream();
        var options = new ImportOptions
        {
            Format = Format.CsvTyped,
            MaxErrors = 1
        };

        // Act
        var result = await sut.ImportAsync<EntityWithValidation>(stream, options);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Data.ShouldBeEmpty();
        result.Value.Errors.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(AggregateFormats))]
    public async Task ImportStreamAsync_WithCollectErrors_YieldsSuccessForValidRowAndFailureForInvalidRow(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMixedValidityStream(format);
        var options = new ImportOptions { Format = format };

        // Act
        var results = new List<Result<EntityWithRequiredColumn>>();
        await foreach (var result in sut.ImportAsyncEnumerable<EntityWithRequiredColumn>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(2);
        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Count(r => r.IsFailure).ShouldBe(1);
        results.Single(r => r.IsSuccess).Value.RequiredField.ShouldBe("value");
        results.Single(r => r.IsFailure).Errors[0].Message.ShouldContain("required");
    }

    [Theory]
    [MemberData(nameof(AggregateFormats))]
    public async Task ImportStreamAsync_WithMaxErrors_StopsAfterConfiguredErrorLimit(Format format)
    {
        // Arrange
        var sut = new DataPorterService([CreateProvider(format)], this.configurationMerger);
        await using var stream = CreateMultipleInvalidRowsStream(format);
        var options = new ImportOptions
        {
            Format = format,
            MaxErrors = 1
        };

        // Act
        var results = new List<Result<EntityWithRequiredColumn>>();
        await foreach (var result in sut.ImportAsyncEnumerable<EntityWithRequiredColumn>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBeSuccess();
        results[0].Value.Id.ShouldBe(1);
        results[1].ShouldBeFailure();
        results[1].Errors[0].Message.ShouldContain("required");
    }

    [Fact]
    public async Task ImportStreamAsync_WithMalformedXml_YieldsFailureResult()
    {
        // Arrange
        var sut = new DataPorterService([new XmlDataPorterProvider()], this.configurationMerger);
        await using var stream = new MemoryStream("<items><item></items>"u8.ToArray());
        var options = new ImportOptions { Format = Format.Xml };

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
        results[0].HasError<ImportError>().ShouldBeTrue();
        results[0].Errors[0].Message.ShouldContain("Invalid XML");
    }

    [Fact]
    public async Task ImportStreamAsync_WithProgress_ReportsUnknownTotalAndCompletion()
    {
        // Arrange
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger);
        var progress = new TestProgress<ImportProgressReport>();
        await using var stream = CreateLargeJsonImportStream(30);

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream, new ImportOptions { Format = Format.Json, Progress = progress }))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(30);
        progress.Reports.ShouldContain(report => !report.IsCompleted && report.ProcessedRows == 25 && report.PercentageComplete == null);
        progress.Reports[^1].IsCompleted.ShouldBeTrue();
        progress.Reports[^1].ProcessedRows.ShouldBe(30);
        progress.Reports[^1].SuccessfulRows.ShouldBe(30);
    }

    [Fact]
    public async Task ImportStreamAsync_WithMalformedExcel_YieldsFailureResult()
    {
        // Arrange
        var sut = new DataPorterService([new ExcelDataPorterProvider()], this.configurationMerger);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var options = new ImportOptions { Format = Format.Excel };

        // Act
        var results = new List<Result<SimpleEntity>>();
        await foreach (var result in sut.ImportAsyncEnumerable<SimpleEntity>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ShouldBeFailure();
        results[0].HasError<ImportError>().ShouldBeTrue();
        results[0].Errors[0].Message.ShouldContain("Invalid Excel workbook");
    }

    [Fact]
    public async Task ImportStreamAsync_WithMalformedXmlAfterValidRow_YieldsSuccessThenFailure()
    {
        // Arrange
        var sut = new DataPorterService([new XmlDataPorterProvider()], this.configurationMerger);
        await using var stream = new MemoryStream("""
<Root>
  <Item>
    <Id>1</Id>
    <RequiredField>value</RequiredField>
  </Item>
  <Item>
    <Id>2</Id>
</Root>
"""u8.ToArray());
        var options = new ImportOptions { Format = Format.Xml };

        // Act
        var results = new List<Result<EntityWithRequiredColumn>>();
        await foreach (var result in sut.ImportAsyncEnumerable<EntityWithRequiredColumn>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBeSuccess();
        results[0].Value.Id.ShouldBe(1);
        results[0].Value.RequiredField.ShouldBe("value");
        results[1].ShouldBeFailure();
        results[1].HasError<ImportError>().ShouldBeTrue();
        results[1].Errors[0].Message.ShouldContain("Invalid XML");
    }

    [Fact]
    public async Task ImportStreamAsync_WithMalformedJsonAfterValidRow_YieldsSuccessThenFailure()
    {
        // Arrange
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.configurationMerger);
        await using var stream = new MemoryStream("""
[
  { "Id": 1, "RequiredField": "value" },
  { "Id": 2
"""u8.ToArray());
        var options = new ImportOptions { Format = Format.Json };

        // Act
        var results = new List<Result<EntityWithRequiredColumn>>();
        await foreach (var result in sut.ImportAsyncEnumerable<EntityWithRequiredColumn>(stream, options))
        {
            results.Add(result);
        }

        // Assert
        results.Count.ShouldBe(2);
        results[0].ShouldBeSuccess();
        results[0].Value.Id.ShouldBe(1);
        results[0].Value.RequiredField.ShouldBe("value");
        results[1].ShouldBeFailure();
        results[1].HasError<ImportError>().ShouldBeTrue();
        results[1].Errors[0].Message.ShouldContain("Invalid JSON");
    }

    private static TestImportProvider CreateMockImportProvider(
        Format? format = null,
        bool throwOnCancel = false)
    {
        return new TestImportProvider(format, throwOnCancel);
    }

    private static TestExportOnlyProvider CreateMockExportOnlyProvider()
    {
        return new TestExportOnlyProvider();
    }

    private static TestStreamingImportProvider CreateMockStreamingProvider()
    {
        return new TestStreamingImportProvider();
    }

    private static IDataPorterProvider CreateProvider(Format format)
    {
        return format.Key switch
        {
            "csv" => new CsvDataPorterProvider(),
            "excel" => new ExcelDataPorterProvider(),
            "json" => new JsonDataPorterProvider(),
            "xml" => new XmlDataPorterProvider(),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateMissingRequiredHeaderStream(Format format)
    {
        return format.Key switch
        {
            "csv" => new MemoryStream("Id\r\n1\r\n"u8.ToArray()),
            "excel" => CreateExcelStream([nameof(EntityWithRequiredColumn.Id)], [new object[] { 1 }]),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateMissingOptionalHeaderStream(Format format)
    {
        return format.Key switch
        {
            "csv" => new MemoryStream("Id\r\n1\r\n"u8.ToArray()),
            "excel" => CreateExcelStream([nameof(SimpleEntity.Id)], [new object[] { 1 }]),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateMixedValidityStream(Format format)
    {
        return format.Key switch
        {
            "csv" => new MemoryStream("Id,RequiredField\r\n1,value\r\n2,\r\n"u8.ToArray()),
            "excel" => CreateExcelStream(
                [nameof(EntityWithRequiredColumn.Id), nameof(EntityWithRequiredColumn.RequiredField)],
                [new object[] { 1, "value" }, new object[] { 2, null }]),
            "json" => new MemoryStream("""
[
  { "Id": 1, "RequiredField": "value" },
  { "Id": 2 }
]
"""u8.ToArray()),
            "xml" => new MemoryStream("""
<Root>
  <Item>
    <Id>1</Id>
    <RequiredField>value</RequiredField>
  </Item>
  <Item>
    <Id>2</Id>
  </Item>
</Root>
"""u8.ToArray()),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateMultipleInvalidRowsStream(Format format)
    {
        return format.Key switch
        {
            "csv" => new MemoryStream("Id,RequiredField\r\n1,value\r\n2,\r\n3,after-limit\r\n4,\r\n"u8.ToArray()),
            "excel" => CreateExcelStream(
                [nameof(EntityWithRequiredColumn.Id), nameof(EntityWithRequiredColumn.RequiredField)],
                [new object[] { 1, "value" }, new object[] { 2, null }, new object[] { 3, "after-limit" }, new object[] { 4, null }]),
            "json" => new MemoryStream("""
[
  { "Id": 1, "RequiredField": "value" },
  { "Id": 2 },
  { "Id": 3, "RequiredField": "after-limit" },
  { "Id": 4 }
]
"""u8.ToArray()),
            "xml" => new MemoryStream("""
<Root>
  <Item>
    <Id>1</Id>
    <RequiredField>value</RequiredField>
  </Item>
  <Item>
    <Id>2</Id>
  </Item>
  <Item>
    <Id>3</Id>
    <RequiredField>after-limit</RequiredField>
  </Item>
  <Item>
    <Id>4</Id>
  </Item>
</Root>
"""u8.ToArray()),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateInvalidCsvTypedStream()
    {
        return new MemoryStream("""
RecordType,RootId,RecordId,ParentId,Collection,Index,Id,Name
SimpleEntity,root-1,root-1,,,,abc,Broken
"""u8.ToArray());
    }

    private static MemoryStream CreateLocalizedDecimalStream(Format format)
    {
        return format.Key switch
        {
            "csv" => new MemoryStream("Amount\r\n1,23\r\n"u8.ToArray()),
            "excel" => CreateExcelStream([nameof(EntityWithDecimalAmount.Amount)], [new object[] { "1,23" }]),
            "json" => new MemoryStream("""
[
  { "Amount": "1,23" }
]
"""u8.ToArray()),
            "xml" => new MemoryStream("""
<Root>
  <Item>
    <Amount>1,23</Amount>
  </Item>
</Root>
"""u8.ToArray()),
            _ => throw new NotSupportedException()
        };
    }

    private static MemoryStream CreateLargeJsonImportStream(int count)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => $$"""  { "Id": {{i}}, "Name": "Item {{i}}" }""");

        return new MemoryStream(Encoding.UTF8.GetBytes("[\n" + string.Join(",\n", items) + "\n]"));
    }

    private static MemoryStream CreateCsvWithPreambleStream()
    {
        return new MemoryStream("""
Report for import
Id,RequiredField
1,value
"""u8.ToArray());
    }

    private static MemoryStream CreateCsvTypedMissingRequiredStream()
    {
        return new MemoryStream("""
RecordType,RootId,RecordId,ParentId,Collection,Index,Id,RequiredField
EntityWithRequiredColumn,root-1,root-1,,,,1,
"""u8.ToArray());
    }

    private static MemoryStream CreateCsvTypedInvalidValidatorStream()
    {
        return new MemoryStream("""
RecordType,RootId,RecordId,ParentId,Collection,Index,Id,Email,MinLengthField
EntityWithValidation,root-1,root-1,,,,1,not-an-email,no
"""u8.ToArray());
    }

    private static async Task<byte[]> CreateZipBytesAsync(string entryName, string content)
    {
        await using var stream = new MemoryStream();
        await using (var entryStream = CompressionHelper.CreateZipEntryWriteStream(stream, entryName))
        {
            await using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: true);
            await writer.WriteAsync(content);
            await writer.FlushAsync();
        }

        return stream.ToArray();
    }

    private static MemoryStream CreateExcelStream(string[] headers, object[][] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = row[columnIndex]?.ToString();
                }
            }

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }
}
