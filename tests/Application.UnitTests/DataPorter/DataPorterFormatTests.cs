// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.IO.Compression;
using System.Text;
using BridgingIT.DevKit.Application.DataPorter;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class DataPorterFormatTests
{
    [Fact]
    public void DataPorterFormat_BuiltInAndCustomValues_AreNormalizedAndComparable()
    {
        var customUpper = new Format("EDI-X12");
        var customLower = new Format("edi-x12");

        Format.Excel.ShouldBe(new Format("excel"));
        customUpper.ShouldBe(customLower);
        customUpper.ToString().ShouldBe("edi-x12");
    }

    [Fact]
    public async Task AddProvider_WithCustomProvider_ResolvesCustomExportProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataPorter().AddProvider<TestCustomDataPorterProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IDataExporter>();
        await using var stream = new MemoryStream();

        var result = await exporter.ExportAsync(
            [new SimpleEntity { Id = 1, Name = "Item" }],
            stream,
            new ExportOptions { Format = TestCustomDataPorterProvider.CustomFormat });

        result.ShouldBeSuccess();
        result.Value.Format.ShouldBe(TestCustomDataPorterProvider.CustomFormat);
        Encoding.UTF8.GetString(stream.ToArray()).ShouldBe("custom-export");
    }

    [Fact]
    public async Task AddProvider_WithCustomProvider_ResolvesCustomImportProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataPorter().AddProvider<TestCustomDataPorterProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<IDataImporter>();
        await using var stream = new MemoryStream("custom-import"u8.ToArray());

        var result = await importer.ImportAsync<SimpleEntity>(
            stream,
            new ImportOptions { Format = new Format("EDI-X12") });

        result.ShouldBeSuccess();
        result.Value.Data.Count.ShouldBe(1);
        result.Value.Data[0].Name.ShouldBe("Imported");
    }

    [Fact]
    public async Task ExportAsync_WithCustomProviderAndZipCompression_UsesSupportedExtensionForEntryName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataPorter().AddProvider<TestCustomDataPorterProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetRequiredService<IDataExporter>();
        await using var stream = new MemoryStream();

        var result = await exporter.ExportAsync(
            [new SimpleEntity { Id = 1, Name = "Item" }],
            stream,
            new ExportOptions
            {
                Format = TestCustomDataPorterProvider.CustomFormat,
                Compression = new PayloadCompressionOptions { Kind = PayloadCompressionKind.Zip }
            });

        result.ShouldBeSuccess();
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Count.ShouldBe(1);
        archive.Entries[0].FullName.ShouldBe("export.edi");
    }

    private sealed class TestCustomDataPorterProvider : IDataExportProvider, IDataImportProvider
    {
        public static readonly Format CustomFormat = new("edi-x12");

        public Format Format => CustomFormat;

        public IReadOnlyCollection<string> SupportedExtensions => [".edi"];

        public bool SupportsImport => true;

        public bool SupportsExport => true;

        public bool SupportsStreaming => false;

        public Task<ExportResult> ExportAsync<TSource>(
            IEnumerable<TSource> data,
            Stream outputStream,
            ExportConfiguration configuration,
            CancellationToken cancellationToken = default)
            where TSource : class
        {
            var bytes = Encoding.UTF8.GetBytes("custom-export");
            return this.WriteAndReturnAsync(outputStream, bytes, data.Count(), cancellationToken);
        }

        public async Task<ExportResult> ExportAsync<TSource>(
            IAsyncEnumerable<TSource> data,
            Stream outputStream,
            ExportConfiguration configuration,
            CancellationToken cancellationToken = default)
            where TSource : class
        {
            var count = 0;
            await foreach (var _ in data.WithCancellation(cancellationToken))
            {
                count++;
            }

            var bytes = Encoding.UTF8.GetBytes("custom-export");
            return await this.WriteAndReturnAsync(outputStream, bytes, count, cancellationToken);
        }

        public Task<ExportResult> ExportAsync(
            IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
            Stream outputStream,
            CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.UTF8.GetBytes("custom-export");
            return this.WriteAndReturnAsync(outputStream, bytes, dataSets.Sum(x => x.Data.Count()), cancellationToken);
        }

        public async Task<ExportResult> ExportAsync(
            IEnumerable<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
            Stream outputStream,
            CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var (data, _) in dataSets)
            {
                await foreach (var _ in data.WithCancellation(cancellationToken))
                {
                    count++;
                }
            }

            var bytes = Encoding.UTF8.GetBytes("custom-export");
            return await this.WriteAndReturnAsync(outputStream, bytes, count, cancellationToken);
        }

        public Task<ImportResult<TTarget>> ImportAsync<TTarget>(
            Stream inputStream,
            ImportConfiguration configuration,
            CancellationToken cancellationToken = default)
            where TTarget : class, new()
        {
            return Task.FromResult(new ImportResult<TTarget>
            {
                Data =
                [
                    new TTarget() is SimpleEntity entity
                        ? (TTarget)(object)new SimpleEntity { Id = 1, Name = "Imported" }
                        : new TTarget()
                ],
                TotalRows = 1,
                SuccessfulRows = 1,
                FailedRows = 0,
                Duration = TimeSpan.Zero
            });
        }

        public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
            Stream inputStream,
            ImportConfiguration configuration,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TTarget : class, new()
        {
            await Task.CompletedTask;
            yield return Result<TTarget>.Success(new TTarget());
        }

        public Task<ValidationResult> ValidateAsync<TTarget>(
            Stream inputStream,
            ImportConfiguration configuration,
            CancellationToken cancellationToken = default)
            where TTarget : class, new()
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = true,
                TotalRows = 1,
                ValidRows = 1,
                InvalidRows = 0
            });
        }

        private async Task<ExportResult> WriteAndReturnAsync(
            Stream outputStream,
            byte[] bytes,
            int totalRows,
            CancellationToken cancellationToken)
        {
            await outputStream.WriteAsync(bytes, cancellationToken);

            return new ExportResult
            {
                BytesWritten = bytes.Length,
                TotalRows = totalRows,
                Duration = TimeSpan.Zero,
                Format = CustomFormat
            };
        }
    }
}
