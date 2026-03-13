// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class DataPorterServiceImportTests
{
    private readonly ProfileRegistry profileRegistry;
    private readonly AttributeConfigurationReader attributeReader;
    private readonly ConfigurationMerger configurationMerger;

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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);
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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);
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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);
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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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
        using var stream = new MemoryStream([1, 2, 3, 4]);

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

    private static TestImportProvider CreateMockImportProvider(
        Format format = Format.Excel,
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
}
