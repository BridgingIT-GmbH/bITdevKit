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
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync<SimpleEntity>((IEnumerable<SimpleEntity>)null, stream);

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
        await using var stream = new MemoryStream();

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
        await using var stream = new MemoryStream();
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
        await using var stream = new MemoryStream();

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
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsync_WithAsyncDataAndProvider_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(new[] { new SimpleEntity { Id = 1, Name = "Test" } }.ToAsyncEnumerable(), stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsync_WithCancellationToken_ReturnsFailureOnCancellation()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider(throwOnCancel: true);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        await using var stream = new MemoryStream();
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
    public async Task ExportToBytesAsync_WithAsyncData_ReturnsByteArray()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);

        // Act
        var result = await sut.ExportToBytesAsync(new[] { new SimpleEntity { Id = 1, Name = "Test" } }.ToAsyncEnumerable());

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
        var result = await sut.ExportToBytesAsync<SimpleEntity>((IEnumerable<SimpleEntity>)null);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportMultipleAsync_WithNullDataSets_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync((IEnumerable<ExportDataSet>)null, stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportMultipleAsync_WithEmptyDataSets_ReturnsFailure()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(Array.Empty<ExportDataSet>(), stream);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task ExportMultipleAsync_WithAsyncDataSets_ReturnsSuccess()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        await using var stream = new MemoryStream();
        var dataSets = new[]
        {
            AsyncExportDataSet.Create(new[] { new SimpleEntity { Id = 1, Name = "Test1" } }.ToAsyncEnumerable(), "Sheet1"),
            AsyncExportDataSet.Create(new[] { new SimpleEntity { Id = 2, Name = "Test2" } }.ToAsyncEnumerable(), "Sheet2")
        };

        // Act
        var result = await sut.ExportAsync(dataSets, stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(2);
    }

    [Fact]
    public async Task ExportAsync_WithExportOptions_UsesProvidedOptions()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider(Format.Csv);
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new[] { new SimpleEntity { Id = 1, Name = "Test" } };
        await using var stream = new MemoryStream();
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

    [Fact]
    public async Task ExportAsync_WithSingleUseEnumerable_DoesNotPreEnumerateInService()
    {
        // Arrange
        var mockProvider = CreateMockExportProvider();
        var sut = new DataPorterService([mockProvider], this.configurationMerger);
        var data = new SingleUseEnumerable<SimpleEntity>([new SimpleEntity { Id = 1, Name = "Test" }]);
        await using var stream = new MemoryStream();

        // Act
        var result = await sut.ExportAsync(data, stream);

        // Assert
        result.ShouldBeSuccess();
        result.Value.TotalRows.ShouldBe(1);
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

    private sealed class SingleUseEnumerable<T>(IReadOnlyList<T> items) : IEnumerable<T>
    {
        private bool enumerated;

        public IEnumerator<T> GetEnumerator()
        {
            if (this.enumerated)
            {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            this.enumerated = true;
            return items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
