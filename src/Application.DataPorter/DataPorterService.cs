// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.IO.Compression;
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
    IRowInterceptorsProvider rowInterceptorsProvider = null,
    ILoggerFactory loggerFactory = null) : IDataExporter, IDataImporter
{
    private readonly IEnumerable<IDataPorterProvider> providers = providers ?? [];
    private readonly IRowInterceptorsProvider rowInterceptorsProvider = rowInterceptorsProvider ?? NullRowInterceptorsProvider.Instance;
    private readonly ILogger<DataPorterService> logger = loggerFactory?.CreateLogger<DataPorterService>() ?? NullLogger<DataPorterService>.Instance;
    private readonly ILoggerFactory loggerFactory = loggerFactory;

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
        configuration.ProgressTracker = options.Progress is null ? null : new ExportProgressTracker(options.Progress, options.Format);
        configuration.RowInterceptionExecutor = new ExportRowInterceptionExecutor<TSource>(
            this.rowInterceptorsProvider.GetExportInterceptors<TSource>(),
            this.loggerFactory?.CreateLogger(typeof(ExportRowInterceptionExecutor<TSource>).FullName ?? typeof(ExportRowInterceptionExecutor<TSource>).Name));
        var provider = providerResult.Value;
        this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyExport, "export", typeof(TSource).Name, options.Format, configuration.Compression, configuration);
        this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyExport, "export", typeof(TSource).Name, options.Format, provider.GetType().Name, "sync");
        this.LogSyncExportStart(data, options.Format, typeof(TSource));
        configuration.ProgressTracker?.ReportStart();

        var stopwatch = Stopwatch.StartNew();
        var artifactStream = new WriteStreamWrapper(outputStream);

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            ExportResult result;
            await using (var compressedOutputStream = this.CreateCompressionWriteStream(artifactStream, configuration.Compression, provider))
            {
                result = await exportProvider.ExportAsync(
                    data,
                    compressedOutputStream ?? artifactStream,
                    configuration,
                    cancellationToken);
            }

            stopwatch.Stop();
            result = result with { BytesWritten = artifactStream.BytesWritten };

            this.logger.LogInformation("{LogKey} export finished (type={Type}, format={Format}, provider={Provider}, rowCount={RowCount}, skippedRows={SkippedRows}, bytesWritten={BytesWritten}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, typeof(TSource).Name, options.Format, provider.GetType().Name, result.TotalRows, result.SkippedRows, result.BytesWritten, stopwatch.Elapsed.TotalMilliseconds);
            configuration.ProgressTracker?.ReportCompleted(result with { Duration = stopwatch.Elapsed });

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} export canceled (type={Type}, format={Format})", Constants.LogKeyExport, typeof(TSource).Name, options.Format);
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Export operation was cancelled."));
        }
        catch (ExportInterceptionAbortedException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} export aborted by row interceptor (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyExport, typeof(TSource).Name, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportInterceptionAbortedError(ex.Message));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} export failed (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyExport, typeof(TSource).Name, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ExportResult>> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportAsync(data, outputStream, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
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
        configuration.ProgressTracker = options.Progress is null ? null : new ExportProgressTracker(options.Progress, options.Format);
        configuration.RowInterceptionExecutor = new ExportRowInterceptionExecutor<TSource>(
            this.rowInterceptorsProvider.GetExportInterceptors<TSource>(),
            this.loggerFactory?.CreateLogger(typeof(ExportRowInterceptionExecutor<TSource>).FullName ?? typeof(ExportRowInterceptionExecutor<TSource>).Name));
        var provider = providerResult.Value;
        this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyExport, "export", typeof(TSource).Name, options.Format, configuration.Compression, configuration);
        this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyExport, "export", typeof(TSource).Name, options.Format, provider.GetType().Name, "async");

        this.logger.LogDebug("{LogKey} export started (type={Type}, format={Format}, mode={Mode})", Constants.LogKeyExport, typeof(TSource).Name, options.Format, "async");
        configuration.ProgressTracker?.ReportStart();

        var stopwatch = Stopwatch.StartNew();
        var artifactStream = new WriteStreamWrapper(outputStream);

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            ExportResult result;
            await using (var compressedOutputStream = this.CreateCompressionWriteStream(artifactStream, configuration.Compression, provider))
            {
                result = await exportProvider.ExportAsync(
                    data,
                    compressedOutputStream ?? artifactStream,
                    configuration,
                    cancellationToken);
            }

            stopwatch.Stop();
            result = result with { BytesWritten = artifactStream.BytesWritten };

            this.logger.LogInformation("{LogKey} export finished (type={Type}, format={Format}, provider={Provider}, rowCount={RowCount}, skippedRows={SkippedRows}, bytesWritten={BytesWritten}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, typeof(TSource).Name, options.Format, provider.GetType().Name, result.TotalRows, result.SkippedRows, result.BytesWritten, stopwatch.Elapsed.TotalMilliseconds);
            configuration.ProgressTracker?.ReportCompleted(result with { Duration = stopwatch.Elapsed });

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} export canceled (type={Type}, format={Format})", Constants.LogKeyExport, typeof(TSource).Name, options.Format);
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Export operation was cancelled."));
        }
        catch (ExportInterceptionAbortedException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} export aborted by row interceptor (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyExport, typeof(TSource).Name, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportInterceptionAbortedError(ex.Message));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} export failed (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyExport, typeof(TSource).Name, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ExportResult>> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportAsync(data, outputStream, Build(optionsBuilder), cancellationToken);
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
    public Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportToBytesAsync(data, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        options ??= new ExportOptions();

        var providerResult = this.GetProvider(options.Format, requiresExport: true);
        if (providerResult.IsFailure)
        {
            return Result<FileContent>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var bytesResult = await this.ExportToBytesAsync(data, options, cancellationToken);
        if (bytesResult.IsFailure)
        {
            return Result<FileContent>.Failure()
                .WithErrors(bytesResult.Errors)
                .WithMessages(bytesResult.Messages);
        }

        return Result<FileContent>.Success(new FileContent(
            bytesResult.Value,
            GetFileName(providerResult.Value, options),
            GetContentType(providerResult.Value, options.Compression)));
    }

    /// <inheritdoc/>
    public Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportToFileContentAsync(data, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IAsyncEnumerable<TSource> data,
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
    public Task<Result<byte[]>> ExportToBytesAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportToBytesAsync(data, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        options ??= new ExportOptions();

        var providerResult = this.GetProvider(options.Format, requiresExport: true);
        if (providerResult.IsFailure)
        {
            return Result<FileContent>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var bytesResult = await this.ExportToBytesAsync(data, options, cancellationToken);
        if (bytesResult.IsFailure)
        {
            return Result<FileContent>.Failure()
                .WithErrors(bytesResult.Errors)
                .WithMessages(bytesResult.Messages);
        }

        return Result<FileContent>.Success(new FileContent(
            bytesResult.Value,
            GetFileName(providerResult.Value, options),
            GetContentType(providerResult.Value, options.Compression)));
    }

    /// <inheritdoc/>
    public Task<Result<FileContent>> ExportToFileContentAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        return this.ExportToFileContentAsync(data, Build(optionsBuilder), cancellationToken);
    }

    // /// <inheritdoc/>
    // public Task<Result> ExportToStreamAsync<TSource>(
    //     IEnumerable<TSource> data,
    //     Stream outputStream,
    //     ExportOptions options = null,
    //     CancellationToken cancellationToken = default) where TSource : class
    // {
    //     options ??= new ExportOptions();

    //     throw new NotImplementedException();
    // }

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> ExportAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExportOptions();

        if (dataSets is null)
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
        var progressTracker = options.Progress is null ? null : new ExportProgressTracker(options.Progress, options.Format);
        List<(IEnumerable<object> Data, ExportConfiguration Configuration)> configurations = null;
        var artifactStream = new WriteStreamWrapper(outputStream);

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            configurations = dataSets.Select(ds =>
            {
                var config = configurationMerger.BuildExportConfiguration(ds.ItemType, options);
                config.SheetName = ds.SheetName;
                config.ProgressTracker = progressTracker;
                config.RowInterceptionExecutor = this.CreateExportRowInterceptionExecutor(ds.ItemType);
                this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, sheetName={SheetName}, compression={Compression}, configuration={Configuration})", Constants.LogKeyExport, "multi dataset export", ds.ItemType.Name, options.Format, config.SheetName, config.Compression, config);
                return (ds.Data, config);
            }).ToList();

            if (configurations.Count == 0)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new DataPorterError("Data sets cannot be null or empty."));
            }

            this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyExport, "multi dataset export", options.Format, provider.GetType().Name, "sync");
            this.logger.LogDebug("{LogKey} multi dataset export started (format={Format}, provider={Provider}, dataSetCount={DataSetCount}, mode={Mode})", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations.Count, "sync");
            progressTracker?.ReportStart();

            ExportResult result;
            await using (var compressedOutputStream = this.CreateCompressionWriteStream(artifactStream, configurations[0].Configuration.Compression, provider))
            {
                result = await exportProvider.ExportAsync(
                    configurations,
                    compressedOutputStream ?? artifactStream,
                    cancellationToken);
            }

            stopwatch.Stop();
            result = result with { BytesWritten = artifactStream.BytesWritten };
            this.logger.LogInformation("{LogKey} multi dataset export finished (format={Format}, provider={Provider}, dataSetCount={DataSetCount}, rowCount={RowCount}, skippedRows={SkippedRows}, bytesWritten={BytesWritten}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations.Count, result.TotalRows, result.SkippedRows, result.BytesWritten, stopwatch.Elapsed.TotalMilliseconds);
            progressTracker?.ReportCompleted(result with { Duration = stopwatch.Elapsed });

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} multi dataset export canceled (format={Format}, provider={Provider}, dataSetCount={DataSetCount}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations?.Count ?? 0, stopwatch.Elapsed.TotalMilliseconds);
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Export operation was cancelled."));
        }
        catch (Exception ex)
        {
            if (ex is ExportInterceptionAbortedException abortedException)
            {
                this.logger.LogWarning(abortedException, "{LogKey} multi dataset export aborted by row interceptor (format={Format}, reason={Reason})", Constants.LogKeyExport, options.Format, abortedException.Message);
                return Result<ExportResult>.Failure()
                    .WithError(new ExportInterceptionAbortedError(abortedException.Message));
            }

            this.logger.LogError(ex, "{LogKey} multi dataset export failed (format={Format}, reason={Reason})", Constants.LogKeyExport, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ExportResult>> ExportAsync(
        IEnumerable<ExportDataSet> dataSets,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
    {
        return this.ExportAsync(dataSets, outputStream, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> ExportAsync(
        IEnumerable<AsyncExportDataSet> dataSets,
        Stream outputStream,
        ExportOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExportOptions();

        if (dataSets is null)
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
        var progressTracker = options.Progress is null ? null : new ExportProgressTracker(options.Progress, options.Format);
        List<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> configurations = null;
        var artifactStream = new WriteStreamWrapper(outputStream);

        try
        {
            if (provider is not IDataExportProvider exportProvider)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsExport).Select(p => p.Format.ToString())));
            }

            configurations = dataSets.Select(ds =>
            {
                var config = configurationMerger.BuildExportConfiguration(ds.ItemType, options);
                config.SheetName = ds.SheetName;
                config.ProgressTracker = progressTracker;
                config.RowInterceptionExecutor = this.CreateExportRowInterceptionExecutor(ds.ItemType);
                this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, sheetName={SheetName}, compression={Compression}, configuration={Configuration})", Constants.LogKeyExport, "multi dataset export", ds.ItemType.Name, options.Format, config.SheetName, config.Compression, config);
                return (ds.Data, config);
            }).ToList();

            if (configurations.Count == 0)
            {
                return Result<ExportResult>.Failure()
                    .WithError(new DataPorterError("Data sets cannot be null or empty."));
            }

            this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyExport, "multi dataset export", options.Format, provider.GetType().Name, "async");
            this.logger.LogDebug("{LogKey} multi dataset export started (format={Format}, provider={Provider}, dataSetCount={DataSetCount}, mode={Mode})", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations.Count, "async");
            progressTracker?.ReportStart();

            ExportResult result;
            await using (var compressedOutputStream = this.CreateCompressionWriteStream(artifactStream, configurations[0].Configuration.Compression, provider))
            {
                result = await exportProvider.ExportAsync(
                    configurations,
                    compressedOutputStream ?? artifactStream,
                    cancellationToken);
            }

            stopwatch.Stop();
            result = result with { BytesWritten = artifactStream.BytesWritten };
            this.logger.LogInformation("{LogKey} multi dataset export finished (format={Format}, provider={Provider}, dataSetCount={DataSetCount}, rowCount={RowCount}, skippedRows={SkippedRows}, bytesWritten={BytesWritten}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations.Count, result.TotalRows, result.SkippedRows, result.BytesWritten, stopwatch.Elapsed.TotalMilliseconds);
            progressTracker?.ReportCompleted(result with { Duration = stopwatch.Elapsed });

            return Result<ExportResult>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} multi dataset export canceled (format={Format}, provider={Provider}, dataSetCount={DataSetCount}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, options.Format, provider.GetType().Name, configurations?.Count ?? 0, stopwatch.Elapsed.TotalMilliseconds);
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Export operation was cancelled."));
        }
        catch (Exception ex)
        {
            if (ex is ExportInterceptionAbortedException abortedException)
            {
                this.logger.LogWarning(abortedException, "{LogKey} async multi dataset export aborted by row interceptor (format={Format}, reason={Reason})", Constants.LogKeyExport, options.Format, abortedException.Message);
                return Result<ExportResult>.Failure()
                    .WithError(new ExportInterceptionAbortedError(abortedException.Message));
            }

            this.logger.LogError(ex, "{LogKey} async multi dataset export failed (format={Format}, reason={Reason})", Constants.LogKeyExport, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Export failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ExportResult>> ExportAsync(
        IEnumerable<AsyncExportDataSet> dataSets,
        Stream outputStream,
        Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
    {
        return this.ExportAsync(dataSets, outputStream, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<ExportResult>> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        TemplateOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        options ??= new TemplateOptions();

        if (outputStream is null)
        {
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Output stream cannot be null."));
        }

        var providerResult = this.GetProvider(options.Format);
        if (providerResult.IsFailure)
        {
            return Result<ExportResult>.Failure()
                .WithErrors(providerResult.Errors);
        }

        var provider = providerResult.Value;
        if (provider is not IDataTemplateProvider templateProvider || !templateProvider.SupportsTemplateExport)
        {
            return Result<ExportResult>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{options.Format} (template)",
                    this.providers
                        .OfType<IDataTemplateProvider>()
                        .Where(p => p.SupportsTemplateExport)
                        .Select(p => p.Format.ToString())));
        }

        var configuration = configurationMerger.BuildTemplateConfiguration<TTarget>(options);
        this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyExport, "template generation", typeof(TTarget).Name, options.Format, configuration.Compression, configuration);
        this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyExport, "template generation", typeof(TTarget).Name, options.Format, provider.GetType().Name, "sync");
        this.logger.LogDebug("{LogKey} template generation started (type={Type}, format={Format}, provider={Provider}, annotationStyle={AnnotationStyle}, includeHints={IncludeHints}, sampleItemCount={SampleItemCount})", Constants.LogKeyExport, typeof(TTarget).Name, options.Format, provider.GetType().Name, configuration.AnnotationStyle, configuration.IncludeHints, configuration.SampleItemCount);

        var stopwatch = Stopwatch.StartNew();
        var artifactStream = new WriteStreamWrapper(outputStream);

        try
        {
            ExportResult result;
            await using (var compressedOutputStream = this.CreateCompressionWriteStream(artifactStream, configuration.Compression, provider))
            {
                result = await templateProvider.GenerateTemplateAsync<TTarget>(
                    compressedOutputStream ?? artifactStream,
                    configuration,
                    cancellationToken);
            }

            stopwatch.Stop();
            var properties = result.Properties?.Clone() ?? [];
            properties.Set("template", true);
            result = result with
            {
                BytesWritten = artifactStream.BytesWritten,
                Duration = stopwatch.Elapsed,
                Properties = properties
            };

            this.logger.LogInformation("{LogKey} template generation finished (type={Type}, format={Format}, provider={Provider}, rowCount={RowCount}, bytesWritten={BytesWritten}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyExport, typeof(TTarget).Name, options.Format, provider.GetType().Name, result.TotalRows, result.BytesWritten, stopwatch.Elapsed.TotalMilliseconds);
            return Result<ExportResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} template generation canceled (type={Type}, format={Format})", Constants.LogKeyExport, typeof(TTarget).Name, options.Format);
            return Result<ExportResult>.Failure()
                .WithError(new DataPorterError("Template generation operation was cancelled."));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} template generation failed (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyExport, typeof(TTarget).Name, options.Format, ex.Message);
            return Result<ExportResult>.Failure()
                .WithError(new ExportError($"Template generation failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ExportResult>> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        Builder<TemplateOptionsBuilder, TemplateOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.GenerateTemplateAsync<TTarget>(outputStream, Build(optionsBuilder), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<byte[]>> GenerateTemplateToBytesAsync<TTarget>(
        TemplateOptions options = null,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        await using var stream = new MemoryStream();
        var result = await this.GenerateTemplateAsync<TTarget>(stream, options, cancellationToken);

        if (result.IsFailure)
        {
            return Result<byte[]>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<byte[]>.Success(stream.ToArray());
    }

    /// <inheritdoc/>
    public Task<Result<byte[]>> GenerateTemplateToBytesAsync<TTarget>(
        Builder<TemplateOptionsBuilder, TemplateOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.GenerateTemplateToBytesAsync<TTarget>(Build(optionsBuilder), cancellationToken);
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
        configuration.ProgressTracker = options.Progress is null ? null : new ImportProgressTracker(options.Progress, options.Format);
        configuration.RowInterceptionExecutor = new ImportRowInterceptionExecutor<TTarget>(
            this.rowInterceptorsProvider.GetImportInterceptors<TTarget>(),
            this.loggerFactory?.CreateLogger(typeof(ImportRowInterceptionExecutor<TTarget>).FullName ?? typeof(ImportRowInterceptionExecutor<TTarget>).Name));
        var provider = providerResult.Value;
        this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyImport, "import", typeof(TTarget).Name, options.Format, configuration.Compression, configuration);
        this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyImport, "import", typeof(TTarget).Name, options.Format, provider.GetType().Name, "aggregate");

        this.logger.LogDebug("{LogKey} import started (type={Type}, format={Format})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format);
        configuration.ProgressTracker?.ReportStart();
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

            ImportResult<TTarget> result;
            await using (var decompressedInputStream = this.OpenCompressionReadStream(inputStream, configuration.Compression, options.Format))
            {
                result = await importProvider.ImportAsync<TTarget>(decompressedInputStream ?? inputStream, configuration, cancellationToken);
            }

            stopwatch.Stop();

            this.logger.LogInformation("{LogKey} import finished (type={Type}, format={Format}, provider={Provider}, rowCount={RowCount}, successfulRows={SuccessfulRows}, failedRows={FailedRows}, skippedRows={SkippedRows}, errorCount={ErrorCount}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name, result.TotalRows, result.SuccessfulRows, result.FailedRows, result.SkippedRows, result.Errors.Count, stopwatch.Elapsed.TotalMilliseconds);
            configuration.ProgressTracker?.ReportCompleted(result with { Duration = stopwatch.Elapsed });

            return Result<ImportResult<TTarget>>.Success(result with { Duration = stopwatch.Elapsed });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("{LogKey} import canceled (type={Type}, format={Format})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format);
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new DataPorterError("Import operation was cancelled."));
        }
        catch (ImportInterceptionAbortedException ex)
        {
            this.logger.LogWarning(ex, "{LogKey} import aborted by row interceptor (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, ex.Message);
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new ImportInterceptionAbortedError(ex.Message));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} import failed (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, ex.Message);
            return Result<ImportResult<TTarget>>.Failure()
                .WithError(new ImportError($"Import failed: {ex.Message}", ex));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.ImportAsync<TTarget>(inputStream, Build(optionsBuilder), cancellationToken);
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
    public Task<Result<ImportResult<TTarget>>> ImportAsync<TTarget>(
        byte[] data,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.ImportAsync<TTarget>(data, Build(optionsBuilder), cancellationToken);
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
        configuration.ProgressTracker = options.Progress is null ? null : new ImportProgressTracker(options.Progress, options.Format);
        configuration.RowInterceptionExecutor = new ImportRowInterceptionExecutor<TTarget>(
            this.rowInterceptorsProvider.GetImportInterceptors<TTarget>(),
            this.loggerFactory?.CreateLogger(typeof(ImportRowInterceptionExecutor<TTarget>).FullName ?? typeof(ImportRowInterceptionExecutor<TTarget>).Name));
        var provider = providerResult.Value;
        var stopwatch = Stopwatch.StartNew();
        var resultCount = 0;
        var successfulRows = 0;
        var failedRows = 0;

        if (provider is not IDataImportProvider importProvider || !provider.SupportsStreaming)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new FormatNotSupportedError(
                    $"{options.Format} (streaming)",
                    this.providers.Where(p => p.SupportsImport && p.SupportsStreaming).Select(p => p.Format.ToString())));
            yield break;
        }

        this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyImport, "import", typeof(TTarget).Name, options.Format, configuration.Compression, configuration);
        this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyImport, "import", typeof(TTarget).Name, options.Format, provider.GetType().Name, "streaming");
        this.logger.LogDebug("{LogKey} streaming import started (type={Type}, format={Format}, provider={Provider})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name);
        configuration.ProgressTracker?.ReportStart();
        await using var decompressedInputStream = this.OpenCompressionReadStream(inputStream, configuration.Compression, options.Format);
        await using var enumerator = importProvider.ImportStreamAsync<TTarget>(decompressedInputStream ?? inputStream, configuration, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            Result<TTarget> item;

            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                item = enumerator.Current;
            }
            catch (OperationCanceledException)
            {
                this.logger.LogWarning("{LogKey} streaming import canceled (type={Type}, format={Format}, provider={Provider}, resultCount={ResultCount}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name, resultCount, stopwatch.Elapsed.TotalMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} streaming import failed (type={Type}, format={Format}, provider={Provider}, resultCount={ResultCount}, reason={Reason}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name, resultCount, ex.Message, stopwatch.Elapsed.TotalMilliseconds);
                throw;
            }

            resultCount++;
            if (item.IsSuccess)
            {
                successfulRows++;
            }
            else
            {
                failedRows++;
            }

            yield return item;
        }

        stopwatch.Stop();
        this.logger.LogInformation("{LogKey} streaming import finished (type={Type}, format={Format}, provider={Provider}, resultCount={ResultCount}, successfulRows={SuccessfulRows}, failedRows={FailedRows}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name, resultCount, successfulRows, failedRows, stopwatch.Elapsed.TotalMilliseconds);
        configuration.ProgressTracker?.ReportCompleted();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<Result<TTarget>> ImportAsyncEnumerable<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.ImportAsyncEnumerable<TTarget>(inputStream, Build(optionsBuilder), cancellationToken);
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (provider is not IDataImportProvider importProvider)
            {
                return Result<ValidationResult>.Failure()
                    .WithError(new FormatNotSupportedError(
                        options.Format.ToString(),
                        this.providers.Where(p => p.SupportsImport).Select(p => p.Format.ToString())));
            }

            this.logger.LogDebug("{LogKey} configuration merged (operation={Operation}, type={Type}, format={Format}, compression={Compression}, configuration={Configuration})", Constants.LogKeyImport, "validation", typeof(TTarget).Name, options.Format, configuration.Compression, configuration);
            this.logger.LogDebug("{LogKey} provider resolved (operation={Operation}, type={Type}, format={Format}, provider={Provider}, mode={Mode})", Constants.LogKeyImport, "validation", typeof(TTarget).Name, options.Format, provider.GetType().Name, "aggregate");
            this.logger.LogDebug("{LogKey} validation started (type={Type}, format={Format}, provider={Provider})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name);
            ValidationResult result;
            await using (var decompressedInputStream = this.OpenCompressionReadStream(inputStream, configuration.Compression, options.Format))
            {
                result = await importProvider.ValidateAsync<TTarget>(decompressedInputStream ?? inputStream, configuration, cancellationToken);
            }
            stopwatch.Stop();
            this.logger.LogInformation("{LogKey} validation finished (type={Type}, format={Format}, provider={Provider}, isValid={IsValid}, rowCount={RowCount}, validRows={ValidRows}, invalidRows={InvalidRows}, errorCount={ErrorCount}) -> took {TimeElapsed:0.0000} ms", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, provider.GetType().Name, result.IsValid, result.TotalRows, result.ValidRows, result.InvalidRows, result.Errors.Count, stopwatch.Elapsed.TotalMilliseconds);

            return Result<ValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} validation failed (type={Type}, format={Format}, reason={Reason})", Constants.LogKeyImport, typeof(TTarget).Name, options.Format, ex.Message);
            return Result<ValidationResult>.Failure()
                .WithError(new ImportValidationError($"Validation failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ValidationResult>> ValidateAsync<TTarget>(
        Stream inputStream,
        Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        return this.ValidateAsync<TTarget>(inputStream, Build(optionsBuilder), cancellationToken);
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

    private void LogSyncExportStart<TSource>(IEnumerable<TSource> data, Format format, Type sourceType)
    {
        if (this.TryGetCount(data, out var count))
        {
            this.logger.LogDebug("{LogKey} export started (type={Type}, format={Format}, rowCount={RowCount}, mode={Mode})", Constants.LogKeyExport, sourceType.Name, format, count, "sync");
            return;
        }

        this.logger.LogDebug("{LogKey} export started (type={Type}, format={Format}, mode={Mode})", Constants.LogKeyExport, sourceType.Name, format, "sync");
    }

    private bool TryGetCount<TSource>(IEnumerable<TSource> data, out int count)
    {
        if (data is ICollection<TSource> typedCollection)
        {
            count = typedCollection.Count;
            return true;
        }

        if (data is IReadOnlyCollection<TSource> typedReadOnlyCollection)
        {
            count = typedReadOnlyCollection.Count;
            return true;
        }

        if (data is System.Collections.ICollection collection)
        {
            count = collection.Count;
            return true;
        }

        count = 0;
        return false;
    }

    private object CreateExportRowInterceptionExecutor(Type itemType)
    {
        var getInterceptorsMethod = typeof(IRowInterceptorsProvider)
            .GetMethod(nameof(IRowInterceptorsProvider.GetExportInterceptors))
            ?.MakeGenericMethod(itemType);
        var interceptors = getInterceptorsMethod?.Invoke(this.rowInterceptorsProvider, null);
        var executorType = typeof(ExportRowInterceptionExecutor<>).MakeGenericType(itemType);
        var logger = this.loggerFactory?.CreateLogger(executorType.FullName ?? executorType.Name);

        return Activator.CreateInstance(executorType, interceptors, logger);
    }

    private Stream CreateCompressionWriteStream(Stream outputStream, PayloadCompressionOptions compression, IDataPorterProvider provider)
    {
        compression ??= PayloadCompressionOptions.None;

        if (compression.Kind == PayloadCompressionKind.None)
        {
            return null;
        }

        var compressionLevel = compression.CompressionLevel ?? CompressionLevel.Optimal;

        if (compression.Kind == PayloadCompressionKind.GZip)
        {
            this.logger.LogDebug("{LogKey} payload compression selected (operation={Operation}, compression={Compression}, compressionLevel={CompressionLevel})", Constants.LogKeyExport, "export", compression.Kind, compressionLevel);
            return CompressionHelper.CreateGZipCompressionStream(outputStream, compressionLevel, leaveOpen: true);
        }

        var entryName = string.IsNullOrWhiteSpace(compression.ZipEntryName) ? GetDefaultZipEntryName(provider) : compression.ZipEntryName;
        this.logger.LogDebug("{LogKey} payload compression selected (operation={Operation}, compression={Compression}, compressionLevel={CompressionLevel}, zipEntryName={ZipEntryName})", Constants.LogKeyExport, "export", compression.Kind, compressionLevel, entryName);
        return CompressionHelper.CreateZipEntryWriteStream(outputStream, entryName, compressionLevel, leaveOpen: true);
    }

    private Stream OpenCompressionReadStream(Stream inputStream, PayloadCompressionOptions compression, Format format)
    {
        compression ??= PayloadCompressionOptions.None;

        if (compression.Kind == PayloadCompressionKind.None)
        {
            return null;
        }

        if (compression.Kind == PayloadCompressionKind.GZip)
        {
            this.logger.LogDebug("{LogKey} payload compression selected (operation={Operation}, compression={Compression})", Constants.LogKeyImport, "import", compression.Kind);
            return CompressionHelper.CreateGZipDecompressionStream(inputStream, leaveOpen: true);
        }

        var entryName = string.IsNullOrWhiteSpace(compression.ZipEntryName) ? "<single-entry>" : compression.ZipEntryName;
        this.logger.LogDebug("{LogKey} payload compression selected (operation={Operation}, compression={Compression}, zipEntryName={ZipEntryName}, format={Format})", Constants.LogKeyImport, "import", compression.Kind, entryName, format);
        return CompressionHelper.OpenZipEntryReadStream(inputStream, compression.ZipEntryName, leaveOpen: true);
    }

    private static ExportOptions Build(Builder<ExportOptionsBuilder, ExportOptions> optionsBuilder)
    {
        return optionsBuilder is null ? null : optionsBuilder(new ExportOptionsBuilder()).Build();
    }

    private static ImportOptions Build(Builder<ImportOptionsBuilder, ImportOptions> optionsBuilder)
    {
        return optionsBuilder is null ? null : optionsBuilder(new ImportOptionsBuilder()).Build();
    }

    private static TemplateOptions Build(Builder<TemplateOptionsBuilder, TemplateOptions> optionsBuilder)
    {
        return optionsBuilder is null ? null : optionsBuilder(new TemplateOptionsBuilder()).Build();
    }

    private static string GetDefaultZipEntryName(IDataPorterProvider provider)
    {
        var extension = provider?.SupportedExtensions?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))?.Trim();

        if (string.IsNullOrWhiteSpace(extension))
        {
            return "export.dat";
        }

        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        return "export" + extension.ToLowerInvariant();
    }

    private static string GetDefaultFileName(IDataPorterProvider provider, PayloadCompressionOptions compression)
    {
        compression ??= PayloadCompressionOptions.None;

        if (compression.Kind == PayloadCompressionKind.GZip)
        {
            return GetDefaultZipEntryName(provider) + ".gz";
        }

        if (compression.Kind == PayloadCompressionKind.Zip)
        {
            return ".zip".Equals(Path.GetExtension(GetDefaultZipEntryName(provider)), StringComparison.OrdinalIgnoreCase)
                ? GetDefaultZipEntryName(provider)
                : "export.zip";
        }

        return GetDefaultZipEntryName(provider);
    }

    private static string GetFileName(IDataPorterProvider provider, ExportOptions options)
    {
        var defaultFileName = GetDefaultFileName(provider, options?.Compression);
        if (string.IsNullOrWhiteSpace(options?.FileName))
        {
            return defaultFileName;
        }

        return EnsureFileNameHasExpectedExtension(options.FileName.Trim(), provider, options.Compression);
    }

    private static string EnsureFileNameHasExpectedExtension(string fileName, IDataPorterProvider provider, PayloadCompressionOptions compression)
    {
        compression ??= PayloadCompressionOptions.None;

        if (compression.Kind == PayloadCompressionKind.GZip)
        {
            var innerExtension = Path.GetExtension(GetDefaultZipEntryName(provider));
            var expectedSuffix = string.Concat(innerExtension, ".gz");
            return fileName.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(fileName) + expectedSuffix
                    : fileName + expectedSuffix;
        }

        if (compression.Kind == PayloadCompressionKind.Zip)
        {
            return fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".zip";
        }

        var extension = Path.GetExtension(GetDefaultZipEntryName(provider));
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + extension;
    }

    private static string GetContentType(IDataPorterProvider provider, PayloadCompressionOptions compression)
    {
        compression ??= PayloadCompressionOptions.None;

        if (compression.Kind == PayloadCompressionKind.GZip)
        {
            return "application/gzip";
        }

        if (compression.Kind == PayloadCompressionKind.Zip)
        {
            return "application/zip";
        }

        var extension = provider?.SupportedExtensions?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))?.Trim().TrimStart('.');
        return ContentTypeExtensions.FromExtension(extension, ContentType.BIN).MimeType();
    }
}
