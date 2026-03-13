// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DataPorterServiceExportTests
{
    private readonly ProfileRegistry profileRegistry;
    private readonly AttributeConfigurationReader attributeReader;
    private readonly ConfigurationMerger configurationMerger;

    public DataPorterServiceExportTests()
    {
        this.profileRegistry = new ProfileRegistry();
        this.attributeReader = new AttributeConfigurationReader();
        this.configurationMerger = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
    }

    [Fact]
    public async Task ExportAsync_WithNullData_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync<SimpleEntity>(null, stream);

        // Assert
        result.ShouldBeFailure();
        result.HasError<DataPorterError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithNullStream_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };

        // Act
        var result = await sut.ExportAsync(data, null);

        // Assert
        result.ShouldBeFailure();
        result.HasError<DataPorterError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithNoProviders_ReturnsFailure()
    {
        // Arrange
        var sut = new DataPorterService([], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream);

        // Assert
        result.ShouldBeFailure();
        result.HasError<FormatNotSupportedError>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider(Format.Csv);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();
        var options = new ExportOptions { Format = Format.Excel };

        // Act
        var result = await sut.ExportAsync(data, stream, options);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportAsync_WithProviderThatDoesNotSupportExport_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockImportOnlyProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportAsync_WithValidDataAndProvider_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.RowsExported.ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsync_WithCancellationToken_ReturnsFailureOnCancellation()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider(throwOnCancel: true);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await sut.ExportAsync(data, stream, cancellationToken: cts.Token);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportToBytesAsync_WithValidData_ReturnsByteArray()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };

        // Act
        var result = await sut.ExportToBytesAsync(data);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportToBytesAsync_WithNullData_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ExportToBytesAsync<SimpleEntity>(null);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportMultipleAsync_WithNullDataSets_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(null, stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportMultipleAsync_WithEmptyDataSets_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync([], stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportAsync_WithExportOptions_UsesProvidedOptions()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider(Format.Csv);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        using var stream = new MemoryStream();
        var options = new ExportOptions
        {
            Format = Format.Csv,
            SheetName = "CustomSheet",
            IncludeHeaders = false
        };

        // Act
        var result = await sut.ExportAsync(data, stream, options);

        // Assert
        result.ShouldBeSuccess();
    }

    private static TestExportProvider CreateMockExportProvider(
        Format format = Format.Excel,
        bool throwOnCancel = false)
    {
        return new TestExportProvider(format, throwOnCancel);
    }

    private static TestImportOnlyProvider CreateMockImportOnlyProvider()
    {
        return new TestImportOnlyProvider();
    }
}
