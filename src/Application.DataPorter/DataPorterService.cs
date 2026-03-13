// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Main service for data export and import operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DataPorterService"/> class.
/// </remarks>
/// <param name="providers">The available data porter providers.</param>
/// <param name="configurationMerger">The configuration merger.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class DataPorterService(
    IEnumerable<IDataPorterProvider> providers,
    ConfigurationMerger configurationMerger,
    ILoggerFactory loggerFactory = null) : IDataExporter, IDataImporter
{
    private readonly IEnumerable<IDataPorterProvider> providers = providers ?? [];
    private readonly ILogger<DataPorterService> logger = loggerFactory?.CreateLogger<DataPorterService>() ?? NullLogger<DataPorterService>.Instance;

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        options ??= new ExportOptions();

        if (data is null)
        {
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Data cannot be null."));
        }

        if (outputStream is null)
        {
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Output stream cannot be null."));
        }

        var providerResult = this.GetProvider(options.Format, requiresExport: true);
        if (providerResult.IsFailure)
        {
            return Result<ExportResult>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var configuration = configurationMerger.BuildExportConfiguration<TSource>(options);
        var provider = providerResult.Value;

        this.logger.LogInformation(
            "Exporting {Count} {Type} records to {Format}",
            data.Count(),
            typeof(TSource).Name,
            options.Format);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            var result = await exportProvider.ExportAsync(
                data,
                outputStream,
                configuration,
                cancellationToken);

            stopwatch.Stop();

            this.logger.LogInformation(
                "Exported {Rows} rows to {Format} in {Duration}ms",
                result.RowsExported,
                options.Format,
                stopwatch.ElapsedMilliseconds);

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Export operation was cancelled");
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Export operation was cancelled."));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Export failed for type {Type}", typeof(TSource).Name);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        await using var stream = new MemoryStream();
        var result = await this.ExportAsync(data, stream, options, cancellationToken);

        if (result.IsFailure)
        {
            return Result<byte[]>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<byte[]>.Success(stream.ToArray());
    }

    /// <inheritdoc/>
    public Task<Result> ExportToStreamAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default) where TSource : class
    {
        options ??= new ExportOptions();

        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> ExportAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExportOptions();

        if (dataSets?.Any() != true)
        {
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Data sets cannot be null or empty."));
        }

        var providerResult = this.GetProvider(options.Format, requiresExport: true);
        if (providerResult.IsFailure)
        {
            return Result<ExportResult>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var provider = providerResult.Value;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            var configurations = dataSets.Select(ds =>
            {
                var config = configurationMerger.BuildExportConfiguration(ds.ItemType, options);
                config.SheetName = ds.SheetName;
                return (ds.Data, config);
            }).ToList();

            var result = await exportProvider.ExportMultipleAsync(
                configurations,
                outputStream,
                cancellationToken);

            stopwatch.Stop();

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Multi-sheet export failed");
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        options ??= new ImportOptions();

        if (inputStream is null)
        {
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new DataPorterError("Input stream cannot be null."));
        }

        var providerResult = this.GetProvider(options.Format, requiresImport: true);
        if (providerResult.IsFailure)
        {
            return Result<ImportResult<TTarget>>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var configuration = configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        this.logger.LogInformation("Importing {Type} from {Format}", typeof(TTarget).Name, options.Format);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (provider is not IDataImportProvider importProvider)
            {
                return Result<ImportResult<TTarget>>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsImport).Select(p => p.Format.ToString())));
            }

            var result = await importProvider.ImportAsync<TTarget>(inputStream, configuration, cancellationToken);

            stopwatch.Stop();

            this.logger.LogInformation("Imported {Success}/{Total} rows from {Format} in {Duration}ms", result.SuccessfulRows, result.TotalRows, options.Format, stopwatch.ElapsedMilliseconds);

            return Result<ImportResult<TTarget>>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Import operation was cancelled");
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new DataPorterError("Import operation was cancelled."));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Import failed for type {Type}", typeof(TTarget).Name);
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new ImportError($"Import failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        byte[] data,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        if (data is null || data.Length == 0)
        {
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new DataPorterError("Data cannot be null or empty."));
        }

        await using var stream = new MemoryStream(data);
        return await this.ImportAsync<TTarget>(stream, options, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportAsyncEnumerable<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        options ??= new ImportOptions();

        if (inputStream is null)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new DataPorterError("Input stream cannot be null."));
            yield break;
        }

        var providerResult = this.GetProvider(options.Format, requiresImport: true);
        if (providerResult.IsFailure)
        {
            yield return Result<TTarget>.Failure()
                .WithErrors(providerResult.Errors);
            yield break;
        }

        var configuration = configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        if (provider is not IDataImportProvider importProvider || !provider.SupportsStreaming)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{options.Format} (streaming)",
                    this.providers.Where(p => p.SupportsImport && p.SupportsStreaming).Select(p => p.Format.ToString())));
            yield break;
        }

        await foreach (var item in importProvider.ImportStreamAsync<TTarget>(inputStream, configuration, cancellationToken))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ValidationResult>> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        options ??= new ImportOptions();

        if (inputStream is null)
        {
            return Result<ValidationResult>.Failure()
                .WithError(new DataPorterError("Input stream cannot be null."));
        }

        var providerResult = this.GetProvider(options.Format, requiresImport: true);
        if (providerResult.IsFailure)
        {
            return Result<ValidationResult>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var configuration = configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        try
        {
            if (provider is not IDataImportProvider importProvider)
            {
                return Result<ValidationResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsImport).Select(p => p.Format.ToString())));
            }

            var result = await importProvider.ValidateAsync<TTarget>(inputStream, configuration, cancellationToken);

            return Result<ValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Validation failed for type {Type}", typeof(TTarget).Name);
            return Result<ValidationResult>.Failure()
                .WithError(new ImportValidationError($"Validation failed: {ex.Message}"));
        }
    }

    private Result<IDataPorterProvider> GetProvider(
        Format format,
        bool requiresExport = false,
        bool requiresImport = false)
    {
        var provider = this.providers.FirstOrDefault(p => p.Format == format);

        if (provider is null)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    format.ToString(),
                    this.providers.Select(p => p.Format.ToString())));
        }

        if (requiresExport && !provider.SupportsExport)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{format} (export)",
                    this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
        }

        if (requiresImport && !provider.SupportsImport)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{format} (import)",
                    this.providers.Where(p => p.SupportsImport).Select(p => p.Format.ToString())));
        }

        return Result<IDataPorterProvider>.Success(provider);
    }
}
