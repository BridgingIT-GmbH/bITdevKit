// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Main service for data export and import operations.
/// </summary>
public sealed class DataPorterService : IDataExporter, IDataImporter
{
    private readonly IEnumerable<IDataPorterProvider> providers;
    private readonly ConfigurationMerger configurationMerger;
    private readonly ILogger<DataPorterService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterService"/> class.
    /// </summary>
    /// <param name="providers">The available data porter providers.</param>
    /// <param name="configurationMerger">The configuration merger.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public DataPorterService(
        IEnumerable<IDataPorterProvider> providers,
        ConfigurationMerger configurationMerger,
        ILoggerFactory loggerFactory = null)
    {
        this.providers = providers ?? [];
        this.configurationMerger = configurationMerger;
        this.logger = loggerFactory?.CreateLogger<DataPorterService>() ?? NullLogger<DataPorterService>.Instance;
    }

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

        var configuration = this.configurationMerger.BuildExportConfiguration<TSource>(options);
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
                        options.Format,
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format)));
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
        using var stream = new MemoryStream();
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
    public async Task<Result<ExportResult>> ExportMultipleAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExportOptions();

        if (dataSets is null || !dataSets.Any())
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
                        options.Format,
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format)));
            }

            var configurations = dataSets.Select(ds =>
            {
                var config = this.configurationMerger.BuildExportConfiguration(ds.ItemType, options);
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

        var configuration = this.configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        this.logger.LogInformation(
            "Importing {Type} from {Format}",
            typeof(TTarget).Name,
            options.Format);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (provider is not IDataImportProvider importProvider)
            {
                return Result<ImportResult<TTarget>>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format,
                        this.providers.Where(p => p.SupportsImport).Select(p => p.Format)));
            }

            var result = await importProvider.ImportAsync<TTarget>(
                inputStream,
                configuration,
                cancellationToken);

            stopwatch.Stop();

            this.logger.LogInformation(
                "Imported {Success}/{Total} rows from {Format} in {Duration}ms",
                result.SuccessfulRows,
                result.TotalRows,
                options.Format,
                stopwatch.ElapsedMilliseconds);

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
    public async Task<Result<ImportResult<TTarget>>> ImportFromBytesAsync<TTarget>(
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

        using var stream = new MemoryStream(data);
        return await this.ImportAsync<TTarget>(stream, options, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
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

        var configuration = this.configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        if (provider is not IDataImportProvider importProvider || !provider.SupportsStreaming)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{options.Format} (streaming)",
                    this.providers.Where(p => p.SupportsImport && p.SupportsStreaming).Select(p => p.Format)));
            yield break;
        }

        await foreach (var item in importProvider.ImportStreamAsync<TTarget>(
            inputStream,
            configuration,
            cancellationToken))
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

        var configuration = this.configurationMerger.BuildImportConfiguration<TTarget>(options);
        var provider = providerResult.Value;

        try
        {
            if (provider is not IDataImportProvider importProvider)
            {
                return Result<ValidationResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format,
                        this.providers.Where(p => p.SupportsImport).Select(p => p.Format)));
            }

            var result = await importProvider.ValidateAsync<TTarget>(
                inputStream,
                configuration,
                cancellationToken);

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
        string format,
        bool requiresExport = false,
        bool requiresImport = false)
    {
        var provider = this.providers.FirstOrDefault(
            p => p.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    format,
                    this.providers.Select(p => p.Format)));
        }

        if (requiresExport && !provider.SupportsExport)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{format} (export)",
                    this.providers.Where(p => p.SupportsExport).Select(p => p.Format)));
        }

        if (requiresImport && !provider.SupportsImport)
        {
            return Result<IDataPorterProvider>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{format} (import)",
                    this.providers.Where(p => p.SupportsImport).Select(p => p.Format)));
        }

        return Result<IDataPorterProvider>.Success(provider);
    }
}

/// <summary>
/// Provider interface for export operations.
/// </summary>
public interface IDataExportProvider : IDataPorterProvider
{
    /// <summary>
    /// Exports data to a stream.
    /// </summary>
    Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TSource : class;

    /// <summary>
    /// Exports multiple data sets to a stream.
    /// </summary>
    Task<ExportResult> ExportMultipleAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider interface for import operations.
/// </summary>
public interface IDataImportProvider : IDataPorterProvider
{
    /// <summary>
    /// Imports data from a stream.
    /// </summary>
    Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Streams import results for large files.
    /// </summary>
    IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();

    /// <summary>
    /// Validates import data without importing.
    /// </summary>
    Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new();
}
