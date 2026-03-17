// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Typed-row CSV data porter provider for hierarchical object graphs.
/// </summary>
public sealed class CsvTypedDataPorterProvider(
    CsvTypedConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider
{
    private static readonly string[] baseColumnNames = ["RecordType", "RootId", "RecordId", "ParentId", "Collection", "Index"];
    private readonly CsvTypedConfiguration configuration = configuration ?? new CsvTypedConfiguration();
    private readonly ILogger<CsvTypedDataPorterProvider> logger = loggerFactory?.CreateLogger<CsvTypedDataPorterProvider>() ?? NullLogger<CsvTypedDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.CsvTyped;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".csv"];

    /// <inheritdoc/>
    public bool SupportsImport => true;

    /// <inheritdoc/>
    public bool SupportsExport => true;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var payloadColumns = this.BuildPayloadColumns([exportConfiguration]);
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        var executor = exportConfiguration.GetExportRowInterceptionExecutor<TSource>();

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        await this.WriteHeaderAsync(csv, payloadColumns);

        var rowsWritten = 0;
        var rootColumns = exportConfiguration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logicalRowNumber++;
            var interception = executor is not null
                ? await executor.BeforeAsync(item, logicalRowNumber, this.Format, exportConfiguration.SheetName, false, cancellationToken)
                : null;

            if (interception?.Outcome == RowInterceptionOutcome.Skip)
            {
                skippedRows++;
                warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                exportConfiguration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, skippedRows: skippedRows);
                continue;
            }

            if (interception?.Outcome == RowInterceptionOutcome.Abort)
            {
                throw new ExportInterceptionAbortedException(interception.Reason);
            }

            rowsWritten += await this.WriteRootRowsAsync(csv, interception?.Item ?? item, rootColumns, exportConfiguration, payloadColumns, cancellationToken);
            exportConfiguration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, skippedRows: skippedRows);
            if (interception is not null)
            {
                await executor.AfterAsync(interception, cancellationToken);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsWritten,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var payloadColumns = this.BuildPayloadColumns([exportConfiguration]);
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        var executor = exportConfiguration.GetExportRowInterceptionExecutor<TSource>();

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        await this.WriteHeaderAsync(csv, payloadColumns);

        var rowsWritten = 0;
        var rootColumns = exportConfiguration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();

        await foreach (var item in data.WithCancellation(cancellationToken))
        {
            logicalRowNumber++;
            var interception = executor is not null
                ? await executor.BeforeAsync(item, logicalRowNumber, this.Format, exportConfiguration.SheetName, true, cancellationToken)
                : null;

            if (interception?.Outcome == RowInterceptionOutcome.Skip)
            {
                skippedRows++;
                warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                exportConfiguration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, skippedRows: skippedRows);
                continue;
            }

            if (interception?.Outcome == RowInterceptionOutcome.Abort)
            {
                throw new ExportInterceptionAbortedException(interception.Reason);
            }

            rowsWritten += await this.WriteRootRowsAsync(csv, interception?.Item ?? item, rootColumns, exportConfiguration, payloadColumns, cancellationToken);
            exportConfiguration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, skippedRows: skippedRows);
            if (interception is not null)
            {
                await executor.AfterAsync(interception, cancellationToken);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsWritten,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var dataSetList = dataSets.ToList();
        var payloadColumns = this.BuildPayloadColumns(dataSetList.Select(dataSet => dataSet.Configuration));
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        await this.WriteHeaderAsync(csv, payloadColumns);

        var rowsWritten = 0;

        foreach (var (data, configuration) in dataSetList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootColumns = configuration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();

            foreach (var item in data)
            {
                logicalRowNumber++;
                var interception = await ObjectExportRowInterceptionInvoker.BeforeAsync(
                    configuration.RowInterceptionExecutor,
                    item,
                    logicalRowNumber,
                    this.Format,
                    configuration.SheetName,
                    false,
                    cancellationToken);

                if (interception.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                    configuration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, message: $"Exported {rowsWritten} rows from {configuration.SheetName ?? "Data"}", skippedRows: skippedRows);
                    continue;
                }

                if (interception.Outcome == RowInterceptionOutcome.Abort)
                {
                    throw new ExportInterceptionAbortedException(interception.Reason);
                }

                rowsWritten += await this.WriteRootRowsAsync(csv, interception.Item, rootColumns, configuration, payloadColumns, cancellationToken);
                configuration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, message: $"Exported {rowsWritten} rows from {configuration.SheetName ?? "Data"}", skippedRows: skippedRows);
                await ObjectExportRowInterceptionInvoker.AfterAsync(configuration.RowInterceptionExecutor, interception.State, cancellationToken);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsWritten,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var dataSetList = dataSets.ToList();
        var payloadColumns = this.BuildPayloadColumns(dataSetList.Select(dataSet => dataSet.Configuration));
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        await this.WriteHeaderAsync(csv, payloadColumns);

        var rowsWritten = 0;

        foreach (var (data, configuration) in dataSetList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootColumns = configuration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();
            var logicalRowNumber = 0;

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                logicalRowNumber++;
                var interception = await ObjectExportRowInterceptionInvoker.BeforeAsync(
                    configuration.RowInterceptionExecutor,
                    item,
                    logicalRowNumber,
                    this.Format,
                    configuration.SheetName,
                    true,
                    cancellationToken);

                if (interception.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                    configuration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, message: $"Exported {rowsWritten} rows from {configuration.SheetName ?? "Data"}", skippedRows: skippedRows);
                    continue;
                }

                if (interception.Outcome == RowInterceptionOutcome.Abort)
                {
                    throw new ExportInterceptionAbortedException(interception.Reason);
                }

                rowsWritten += await this.WriteRootRowsAsync(csv, interception.Item, rootColumns, configuration, payloadColumns, cancellationToken);
                configuration.ProgressTracker?.ReportProgress(rowsWritten, writeStream.BytesWritten, message: $"Exported {rowsWritten} rows from {configuration.SheetName ?? "Data"}", skippedRows: skippedRows);
                await ObjectExportRowInterceptionInvoker.AfterAsync(configuration.RowInterceptionExecutor, interception.State, cancellationToken);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsWritten,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Warnings = warnings
        };
    }

    private async Task WriteHeaderAsync(CsvWriter csv, IReadOnlyList<string> payloadColumns)
    {
        foreach (var column in baseColumnNames.Concat(payloadColumns))
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();
    }

    private IReadOnlyList<string> BuildPayloadColumns(IEnumerable<ExportConfiguration> configurations)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            var rootColumns = configuration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();

            foreach (var column in rootColumns)
            {
                var propertyType = column.PropertyInfo?.PropertyType ?? typeof(object);
                if (column.Converter is not null || !propertyType.SupportsStructuredValue())
                {
                    columns.Add(column.HeaderName ?? column.PropertyName);
                    continue;
                }

                var nestedType = propertyType.IsCollectionType()
                    ? this.GetCollectionElementType(propertyType)
                    : propertyType;

                if (nestedType is not null)
                {
                    this.CollectPayloadColumns(nestedType, columns, new HashSet<Type>());
                }
            }
        }

        return [.. columns.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
    }

    private void CollectPayloadColumns(Type sourceType, ISet<string> payloadColumns, ISet<Type> visitedTypes)
    {
        if (!visitedTypes.Add(sourceType))
        {
            return;
        }

        foreach (var property in this.GetFlattenableProperties(sourceType))
        {
            if (property.PropertyType.SupportsStructuredValue())
            {
                var nestedType = property.PropertyType.IsCollectionType()
                    ? this.GetCollectionElementType(property.PropertyType)
                    : property.PropertyType;

                if (nestedType is not null)
                {
                    this.CollectPayloadColumns(nestedType, payloadColumns, visitedTypes);
                }

                continue;
            }

            payloadColumns.Add(this.GetPayloadColumnName(property.Name));
        }
    }

    private async Task<int> WriteRootRowsAsync(
        CsvWriter csv,
        object item,
        IReadOnlyList<ColumnConfiguration> rootColumns,
        ExportConfiguration configuration,
        IReadOnlyList<string> payloadColumns,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootId = this.GetIdentifier(item) ?? Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        var rootRecordType = this.GetRecordTypeName(configuration.SourceType, rootColumns, isCollectionItem: false);
        var rootPayload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        visited.Add(item);

        this.WriteRootPayload(rootPayload, item, rootColumns, configuration);
        await this.WriteCsvTypedRowAsync(csv, new CsvTypedRow(rootRecordType, rootId, rootId, null, null, null, rootPayload), payloadColumns);

        return 1 + await this.WriteChildRowsAsync(csv, item, rootId, rootId, rootColumns, configuration, visited, payloadColumns, cancellationToken);
    }

    private async Task<int> WriteChildRowsAsync(
        CsvWriter csv,
        object parent,
        string rootId,
        string parentId,
        IReadOnlyList<ColumnConfiguration> columns,
        ExportConfiguration configuration,
        HashSet<object> visited,
        IReadOnlyList<string> payloadColumns,
        CancellationToken cancellationToken)
    {
        var rowsWritten = 0;

        foreach (var column in columns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (column.Ignore || column.Converter is not null)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType;
            if (propertyType is null || !propertyType.SupportsStructuredValue())
            {
                continue;
            }

            var value = column.GetValue(parent);
            if (value is null)
            {
                continue;
            }

            if (propertyType.IsCollectionType())
            {
                var index = 0;
                foreach (var item in (IEnumerable)value)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item is null)
                    {
                        index++;
                        continue;
                    }

                    if (!item.GetType().IsValueType && !visited.Add(item))
                    {
                        index++;
                        continue;
                    }

                    var itemType = item.GetType();
                    var recordType = this.GetRecordTypeName(itemType, [], isCollectionItem: true, propertyName: column.PropertyName);
                    var recordId = this.GetIdentifier(item) ?? $"{rootId}:{column.PropertyName}:{index}";
                    var payload = this.BuildPayload(item, itemType, configuration.Culture);
                    await this.WriteCsvTypedRowAsync(csv, new CsvTypedRow(recordType, rootId, recordId, parentId, column.PropertyName, index, payload), payloadColumns);
                    rowsWritten++;

                    try
                    {
                        rowsWritten += await this.WriteNestedStructuredRowsAsync(csv, item, rootId, recordId, itemType, configuration, visited, payloadColumns, cancellationToken);
                    }
                    finally
                    {
                        if (!itemType.IsValueType)
                        {
                            visited.Remove(item);
                        }
                    }

                    index++;
                }

                continue;
            }

            if (!value.GetType().IsValueType && !visited.Add(value))
            {
                continue;
            }

            var nestedRecordType = this.GetRecordTypeName(propertyType, [], isCollectionItem: false, propertyName: column.PropertyName);
            var nestedRecordId = this.GetIdentifier(value) ?? $"{rootId}:{column.PropertyName}";
            var rowPayload = this.BuildPayload(value, propertyType, configuration.Culture);
            await this.WriteCsvTypedRowAsync(csv, new CsvTypedRow(nestedRecordType, rootId, nestedRecordId, parentId, column.PropertyName, null, rowPayload), payloadColumns);
            rowsWritten++;

            try
            {
                rowsWritten += await this.WriteNestedStructuredRowsAsync(csv, value, rootId, nestedRecordId, propertyType, configuration, visited, payloadColumns, cancellationToken);
            }
            finally
            {
                if (!propertyType.IsValueType)
                {
                    visited.Remove(value);
                }
            }
        }

        return rowsWritten;
    }

    private async Task<int> WriteNestedStructuredRowsAsync(
        CsvWriter csv,
        object value,
        string rootId,
        string parentId,
        Type sourceType,
        ExportConfiguration configuration,
        HashSet<object> visited,
        IReadOnlyList<string> payloadColumns,
        CancellationToken cancellationToken)
    {
        var nestedColumns = this.GetFlattenableProperties(sourceType)
            .Select((property, order) => new ColumnConfiguration
            {
                PropertyName = property.Name,
                HeaderName = this.GetPayloadColumnName(property.Name),
                Order = order,
                PropertyInfo = property
            })
            .ToList();

        return await this.WriteChildRowsAsync(csv, value, rootId, parentId, nestedColumns, configuration, visited, payloadColumns, cancellationToken);
    }

    private async Task WriteCsvTypedRowAsync(CsvWriter csv, CsvTypedRow row, IReadOnlyList<string> payloadColumns)
    {
        csv.WriteField(row.RecordType);
        csv.WriteField(row.RootId);
        csv.WriteField(row.RecordId);
        csv.WriteField(row.ParentId);
        csv.WriteField(row.Collection);
        csv.WriteField(row.Index?.ToString(CultureInfo.InvariantCulture));

        foreach (var column in payloadColumns)
        {
            csv.WriteField(row.Payload.TryGetValue(column, out var value) ? value : string.Empty);
        }

        await csv.NextRecordAsync();
    }

    /// <inheritdoc/>
    public async Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        using var reader = new StreamReader(inputStream, this.configuration.Encoding);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = this.configuration.TrimFields ? TrimOptions.Trim : TrimOptions.None
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var rows = new List<CsvTypedRow>();
        var rowNumber = 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            rows.Add(this.ReadRow(csv, headers));
            importConfiguration.ProgressTracker?.ReportProgress(rows.Count, 0, 0, 0);
        }

        var errors = new List<ImportRowError>();
        var warnings = new List<string>();
        var skippedRows = 0;
        var executor = importConfiguration.GetImportRowInterceptionExecutor<TTarget>();
        var imported = this.Materialize<TTarget>(rows, importConfiguration, errors);
        var accepted = new List<TTarget>();
        var logicalRowNumber = 0;

        foreach (var item in imported)
        {
            logicalRowNumber++;
            var interception = executor is not null
                ? await executor.BeforeAsync(item, logicalRowNumber, this.Format, importConfiguration.SheetName, false, cancellationToken)
                : null;

            if (interception?.Outcome == RowInterceptionOutcome.Skip)
            {
                skippedRows++;
                warnings.Add($"Row {logicalRowNumber} skipped by import interceptor: {interception.Reason}");
                importConfiguration.ProgressTracker?.ReportProgress(
                    rows.Count,
                    accepted.Count,
                    errors.Count + skippedRows,
                    errors.Count,
                    rows.Count,
                    skippedRows: skippedRows,
                    force: true);
                continue;
            }

            if (interception?.Outcome == RowInterceptionOutcome.Abort)
            {
                throw new ImportInterceptionAbortedException(interception.Reason);
            }

            accepted.Add(interception?.Item ?? item);
            if (interception is not null)
            {
                await executor.AfterAsync(interception, cancellationToken);
            }

            if (logicalRowNumber % 25 == 0)
            {
                importConfiguration.ProgressTracker?.ReportProgress(
                    rows.Count,
                    accepted.Count,
                    errors.Count + skippedRows,
                    errors.Count,
                    rows.Count,
                    skippedRows: skippedRows,
                    force: true);
            }
        }

        return new ImportResult<TTarget>
        {
            Data = accepted,
            TotalRows = rows.Count,
            SuccessfulRows = accepted.Count,
            FailedRows = errors.Count + skippedRows,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        using var reader = new StreamReader(inputStream, this.configuration.Encoding);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = this.configuration.TrimFields ? TrimOptions.Trim : TrimOptions.None
        });

        if (!await csv.ReadAsync())
        {
            yield break;
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var completedRootIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentRows = new List<CsvTypedRow>();
        var currentRootId = default(string);
        var errorCount = 0;
        var processedRows = 0;
        var successfulRows = 0;
        var failedRows = 0;
        var skippedRows = 0;
        var logicalRowNumber = 0;
        var executor = importConfiguration.GetImportRowInterceptionExecutor<TTarget>();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            CsvTypedRow row = null;
            ImportError readError = null;
            try
            {
                row = this.ReadRow(csv, headers);
            }
            catch (Exception ex)
            {
                readError = new ImportError($"Failed to read typed CSV row: {ex.Message}", ex);
            }

            if (readError is not null)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(readError);
                yield break;
            }

            if (currentRootId is null)
            {
                currentRootId = row.RootId;
                currentRows.Add(row);
                processedRows++;
                importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount);
                continue;
            }

            if (string.Equals(currentRootId, row.RootId, StringComparison.OrdinalIgnoreCase))
            {
                currentRows.Add(row);
                processedRows++;
                importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount);
                continue;
            }

            foreach (var result in this.MaterializeStreamGroup<TTarget>(currentRows, importConfiguration))
            {
                if (result.IsSuccess)
                {
                    logicalRowNumber++;
                    var interception = executor is not null
                        ? await executor.BeforeAsync(result.Value, logicalRowNumber, this.Format, importConfiguration.SheetName, true, cancellationToken)
                        : null;

                    if (interception?.Outcome == RowInterceptionOutcome.Skip)
                    {
                        skippedRows++;
                        failedRows++;
                        importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                        continue;
                    }

                    if (interception?.Outcome == RowInterceptionOutcome.Abort)
                    {
                        failedRows++;
                        importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                        yield return Result<TTarget>.Failure()
                            .WithError(new ImportInterceptionAbortedError(interception.Reason));
                        yield break;
                    }

                    successfulRows++;
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    if (interception is not null)
                    {
                        await executor.AfterAsync(interception, cancellationToken);
                    }

                    yield return Result<TTarget>.Success(interception?.Item ?? result.Value);
                    continue;
                }

                if (result.IsFailure)
                {
                    failedRows++;
                    errorCount++;
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    yield return result;
                    if (ImportErrorLimit.IsReached(importConfiguration, errorCount))
                    {
                        yield break;
                    }
                }
                else
                {
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    yield return result;
                }
            }

            completedRootIds.Add(currentRootId);
            if (completedRootIds.Contains(row.RootId))
            {
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportError($"Non-contiguous RootId '{row.RootId}' is not supported for streaming typed CSV import."));
                yield break;
            }

            currentRows = [row];
            currentRootId = row.RootId;
            processedRows++;
            importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount);
        }

        if (currentRows.Count > 0)
        {
            foreach (var result in this.MaterializeStreamGroup<TTarget>(currentRows, importConfiguration))
            {
                if (result.IsSuccess)
                {
                    logicalRowNumber++;
                    var interception = executor is not null
                        ? await executor.BeforeAsync(result.Value, logicalRowNumber, this.Format, importConfiguration.SheetName, true, cancellationToken)
                        : null;

                    if (interception?.Outcome == RowInterceptionOutcome.Skip)
                    {
                        skippedRows++;
                        failedRows++;
                        importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                        continue;
                    }

                    if (interception?.Outcome == RowInterceptionOutcome.Abort)
                    {
                        failedRows++;
                        importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                        yield return Result<TTarget>.Failure()
                            .WithError(new ImportInterceptionAbortedError(interception.Reason));
                        yield break;
                    }

                    successfulRows++;
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    if (interception is not null)
                    {
                        await executor.AfterAsync(interception, cancellationToken);
                    }

                    yield return Result<TTarget>.Success(interception?.Item ?? result.Value);
                    continue;
                }

                if (result.IsFailure)
                {
                    failedRows++;
                    errorCount++;
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    yield return result;
                    if (ImportErrorLimit.IsReached(importConfiguration, errorCount))
                    {
                        yield break;
                    }
                }
                else
                {
                    importConfiguration.ProgressTracker?.ReportProgress(processedRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    yield return result;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var result = await this.ImportAsync<TTarget>(inputStream, importConfiguration, cancellationToken);

        return result.Errors.Count == 0
            ? ValidationResult.Success(result.TotalRows)
            : ValidationResult.Failure(result.TotalRows, result.SuccessfulRows, result.Errors);
    }

    private List<CsvTypedRow> BuildRows(IReadOnlyList<object> data, ExportConfiguration configuration)
    {
        var rows = new List<CsvTypedRow>();
        var rootColumns = configuration.Columns.Where(column => !column.Ignore).OrderBy(column => column.Order).ToList();

        foreach (var item in data)
        {
            var rootId = this.GetIdentifier(item) ?? Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
            var rootRecordType = this.GetRecordTypeName(configuration.SourceType, rootColumns, isCollectionItem: false);
            var rootPayload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            visited.Add(item);

            rows.Add(new CsvTypedRow(rootRecordType, rootId, rootId, null, null, null, rootPayload));
            this.WriteRootPayload(rootPayload, item, rootColumns, configuration);
            this.AddChildRows(rows, item, rootId, rootId, rootColumns, configuration, visited);
        }

        return rows;
    }

    private void WriteRootPayload(
        IDictionary<string, string> payload,
        object item,
        IReadOnlyList<ColumnConfiguration> columns,
        ExportConfiguration configuration)
    {
        foreach (var column in columns)
        {
            var propertyType = column.PropertyInfo?.PropertyType ?? typeof(object);
            if (column.Converter is null && propertyType.SupportsStructuredValue())
            {
                continue;
            }

            var value = column.GetValue(item);
            if (column.Converter is not null)
            {
                var context = new ValueConversionContext
                {
                    PropertyName = column.PropertyName,
                    PropertyType = propertyType,
                    EntityType = configuration.SourceType,
                    Format = column.Format,
                    Culture = configuration.Culture
                };

                value = column.Converter.ConvertToExport(value, context);
            }

            payload[column.HeaderName ?? column.PropertyName] = this.FormatValue(value, column, configuration);
        }
    }

    private void AddChildRows(
        ICollection<CsvTypedRow> rows,
        object parent,
        string rootId,
        string parentId,
        IReadOnlyList<ColumnConfiguration> columns,
        ExportConfiguration configuration,
        HashSet<object> visited)
    {
        foreach (var column in columns)
        {
            if (column.Ignore || column.Converter is not null)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType;
            if (propertyType is null || !propertyType.SupportsStructuredValue())
            {
                continue;
            }

            var value = column.GetValue(parent);
            if (value is null)
            {
                continue;
            }

            if (propertyType.IsCollectionType())
            {
                var index = 0;
                foreach (var item in (IEnumerable)value)
                {
                    if (item is null)
                    {
                        index++;
                        continue;
                    }

                    if (!item.GetType().IsValueType && !visited.Add(item))
                    {
                        index++;
                        continue;
                    }

                    var itemType = item.GetType();
                    var recordType = this.GetRecordTypeName(itemType, [], isCollectionItem: true, propertyName: column.PropertyName);
                    var recordId = this.GetIdentifier(item) ?? $"{rootId}:{column.PropertyName}:{index}";
                    var payload = this.BuildPayload(item, itemType, configuration.Culture);
                    rows.Add(new CsvTypedRow(recordType, rootId, recordId, parentId, column.PropertyName, index, payload));

                    try
                    {
                        this.AddNestedStructuredRows(rows, item, rootId, recordId, itemType, configuration, visited);
                    }
                    finally
                    {
                        if (!itemType.IsValueType)
                        {
                            visited.Remove(item);
                        }
                    }

                    index++;
                }
            }
            else
            {
                if (!value.GetType().IsValueType && !visited.Add(value))
                {
                    continue;
                }

                var recordType = this.GetRecordTypeName(propertyType, [], isCollectionItem: false, propertyName: column.PropertyName);
                var recordId = this.GetIdentifier(value) ?? $"{rootId}:{column.PropertyName}";
                var payload = this.BuildPayload(value, propertyType, configuration.Culture);
                rows.Add(new CsvTypedRow(recordType, rootId, recordId, parentId, column.PropertyName, null, payload));

                try
                {
                    this.AddNestedStructuredRows(rows, value, rootId, recordId, propertyType, configuration, visited);
                }
                finally
                {
                    if (!propertyType.IsValueType)
                    {
                        visited.Remove(value);
                    }
                }
            }
        }
    }

    private void AddNestedStructuredRows(
        ICollection<CsvTypedRow> rows,
        object value,
        string rootId,
        string parentId,
        Type sourceType,
        ExportConfiguration configuration,
        HashSet<object> visited)
    {
        var nestedColumns = this.GetFlattenableProperties(sourceType)
            .Select((property, order) => new ColumnConfiguration
            {
                PropertyName = property.Name,
                HeaderName = this.GetPayloadColumnName(property.Name),
                Order = order,
                PropertyInfo = property
            })
            .ToList();

        this.AddChildRows(rows, value, rootId, parentId, nestedColumns, configuration, visited);
    }

    private Dictionary<string, string> BuildPayload(object value, Type sourceType, CultureInfo culture)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in this.GetFlattenableProperties(sourceType))
        {
            if (property.PropertyType.SupportsStructuredValue())
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            if (propertyValue is null)
            {
                continue;
            }

            payload[this.GetPayloadColumnName(property.Name)] = this.FormatPrimitive(propertyValue, culture);
        }

        return payload;
    }

    private CsvTypedRow ReadRow(CsvReader csv, IReadOnlyList<string> headers)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers.Except(baseColumnNames, StringComparer.OrdinalIgnoreCase))
        {
            payload[header] = csv.GetField(header);
        }

        return new CsvTypedRow(
            csv.GetField("RecordType"),
            csv.GetField("RootId"),
            csv.GetField("RecordId"),
            csv.GetField("ParentId"),
            csv.GetField("Collection"),
            int.TryParse(csv.GetField("Index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : null,
            payload);
    }

    private IReadOnlyList<TTarget> Materialize<TTarget>(
        IReadOnlyList<CsvTypedRow> rows,
        ImportConfiguration importConfiguration,
        ICollection<ImportRowError> errors)
        where TTarget : class, new()
    {
        var results = new List<TTarget>();
        var groupedRows = rows.GroupBy(row => row.RootId, StringComparer.OrdinalIgnoreCase);
        var rootColumns = importConfiguration.Columns.Where(column => !column.Ignore).ToList();

        foreach (var group in groupedRows)
        {
            if (ImportErrorLimit.IsReached(importConfiguration, errors.Count))
            {
                break;
            }

            var item = importConfiguration.Factory is not null
                ? (TTarget)importConfiguration.Factory()
                : new TTarget();
            var rowLookup = group.ToDictionary(row => row.RecordId, StringComparer.OrdinalIgnoreCase);
            var rootRow = group.FirstOrDefault(row => string.Equals(row.RecordId, row.RootId, StringComparison.OrdinalIgnoreCase))
                ?? group.First();
            var groupErrors = new List<ImportRowError>();

            this.ApplyRootPayload(item, rootRow, rootColumns, importConfiguration, groupErrors);
            this.ApplyChildren(item, group.ToList(), rowLookup, rootColumns, importConfiguration, groupErrors);

            foreach (var error in groupErrors)
            {
                if (ImportErrorLimit.TryAdd(errors, error, importConfiguration))
                {
                    break;
                }
            }

            if (groupErrors.Count == 0)
            {
                results.Add(item);
            }

            if (ImportErrorLimit.IsReached(importConfiguration, errors.Count))
            {
                break;
            }
        }

        return results;
    }

    private IEnumerable<Result<TTarget>> MaterializeStreamGroup<TTarget>(
        IReadOnlyList<CsvTypedRow> rows,
        ImportConfiguration importConfiguration)
        where TTarget : class, new()
    {
        var errors = new List<ImportRowError>();
        var imported = this.Materialize<TTarget>(rows, importConfiguration, errors);

        foreach (var error in errors)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new ImportValidationError(error.RowNumber, error.Column, error.Message, error.RawValue));
        }

        foreach (var item in imported)
        {
            yield return Result<TTarget>.Success(item);
        }
    }

    private void ApplyRootPayload(
        object target,
        CsvTypedRow row,
        IReadOnlyList<ImportColumnConfiguration> columns,
        ImportConfiguration configuration,
        ICollection<ImportRowError> errors)
    {
        foreach (var column in columns)
        {
            if (ImportErrorLimit.IsReached(configuration, errors.Count))
            {
                return;
            }

            if (column.Ignore)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType ?? typeof(string);
            if (column.Converter is null && propertyType.SupportsStructuredValue())
            {
                continue;
            }

            var key = column.SourceName ?? column.PropertyName;
            var hasValue = row.Payload.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue);

            if (column.IsRequired && !hasValue)
            {
                if (ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = 0,
                    Column = key,
                    Message = column.RequiredMessage ?? $"{column.PropertyName} is required",
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                }, configuration))
                {
                    return;
                }

                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            var hasErrors = false;

            foreach (var validator in column.Validators)
            {
                if (!validator.Validate(rawValue))
                {
                    hasErrors = true;
                    if (ImportErrorLimit.TryAdd(errors, new ImportRowError
                    {
                        RowNumber = 0,
                        Column = key,
                        Message = validator.ErrorMessage,
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    }, configuration))
                    {
                        return;
                    }
                }
            }

            if (hasErrors)
            {
                continue;
            }

            try
            {
                var convertedValue = column.ConvertValue(rawValue, configuration.Culture);
                column.SetValue(target, convertedValue);
            }
            catch (Exception ex)
            {
                if (ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = 0,
                    Column = column.SourceName ?? column.PropertyName,
                    Message = ex.Message,
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                }, configuration))
                {
                    return;
                }
            }
        }
    }

    private void ApplyChildren(
        object target,
        IReadOnlyList<CsvTypedRow> rows,
        IReadOnlyDictionary<string, CsvTypedRow> rowLookup,
        IReadOnlyList<ImportColumnConfiguration> columns,
        ImportConfiguration configuration,
        ICollection<ImportRowError> errors)
    {
        var rootId = this.GetIdentifier(target);
        var childRows = rows.Where(row => string.Equals(row.ParentId, rootId, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var column in columns)
        {
            if (ImportErrorLimit.IsReached(configuration, errors.Count))
            {
                return;
            }

            if (column.Ignore || column.Converter is not null)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType;
            if (propertyType is null || !propertyType.SupportsStructuredValue())
            {
                continue;
            }

            if (propertyType.IsCollectionType())
            {
                var collectionRows = childRows
                    .Where(row => string.Equals(row.Collection, column.PropertyName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(row => row.Index ?? int.MaxValue)
                    .ToList();

                var collection = this.CreateCollectionInstance(propertyType);
                var elementType = this.GetCollectionElementType(propertyType);
                foreach (var childRow in collectionRows)
                {
                    if (ImportErrorLimit.IsReached(configuration, errors.Count))
                    {
                        return;
                    }

                    var childItem = Activator.CreateInstance(elementType);
                    this.ApplyStructuredPayload(childItem, childRow, elementType, configuration, rowLookup, errors);
                    this.AddCollectionItem(collection, childItem);
                }

                column.SetValue(target, collection);
            }
            else
            {
                var childRow = childRows.FirstOrDefault(row => string.Equals(row.Collection, column.PropertyName, StringComparison.OrdinalIgnoreCase));
                if (childRow is null)
                {
                    continue;
                }

                var childItem = Activator.CreateInstance(propertyType);
                this.ApplyStructuredPayload(childItem, childRow, propertyType, configuration, rowLookup, errors);
                column.SetValue(target, childItem);
            }
        }
    }

    private void ApplyStructuredPayload(
        object target,
        CsvTypedRow row,
        Type targetType,
        ImportConfiguration configuration,
        IReadOnlyDictionary<string, CsvTypedRow> rowLookup,
        ICollection<ImportRowError> errors)
    {
        foreach (var property in this.GetFlattenableProperties(targetType))
        {
            if (ImportErrorLimit.IsReached(configuration, errors.Count))
            {
                return;
            }

            if (property.PropertyType.SupportsStructuredValue())
            {
                if (property.PropertyType.IsCollectionType())
                {
                    var elementType = this.GetCollectionElementType(property.PropertyType);
                    var collection = this.CreateCollectionInstance(property.PropertyType);
                    var collectionRows = rowLookup.Values
                        .Where(candidate => string.Equals(candidate.ParentId, row.RecordId, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(candidate.Collection, property.Name, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(candidate => candidate.Index ?? int.MaxValue)
                        .ToList();

                    foreach (var childRow in collectionRows)
                    {
                        if (ImportErrorLimit.IsReached(configuration, errors.Count))
                        {
                            return;
                        }

                        var child = Activator.CreateInstance(elementType);
                        this.ApplyStructuredPayload(child, childRow, elementType, configuration, rowLookup, errors);
                        this.AddCollectionItem(collection, child);
                    }

                    property.SetValue(target, collection);
                }
                else
                {
                    var childRow = rowLookup.Values.FirstOrDefault(candidate =>
                        string.Equals(candidate.ParentId, row.RecordId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(candidate.Collection, property.Name, StringComparison.OrdinalIgnoreCase));

                    if (childRow is null)
                    {
                        continue;
                    }

                    var child = Activator.CreateInstance(property.PropertyType);
                    this.ApplyStructuredPayload(child, childRow, property.PropertyType, configuration, rowLookup, errors);
                    property.SetValue(target, child);
                }

                continue;
            }

            if (!row.Payload.TryGetValue(this.GetPayloadColumnName(property.Name), out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            try
            {
                property.SetValue(target, this.ConvertToType(rawValue, property.PropertyType, configuration.Culture));
            }
            catch (Exception ex)
            {
                if (ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = 0,
                    Column = property.Name,
                    Message = ex.Message,
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                }, configuration))
                {
                    return;
                }
            }
        }
    }

    private IReadOnlyList<PropertyInfo> GetFlattenableProperties(Type type)
    {
        return [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.CanRead)];
    }

    private string GetRecordTypeName(Type type, IReadOnlyList<ColumnConfiguration> columns, bool isCollectionItem, string propertyName = null)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            if (isCollectionItem && propertyName.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                return propertyName[..^2];
            }

            return propertyName;
        }

        var typeName = type.Name;
        if (typeName.EndsWith("Entity", StringComparison.OrdinalIgnoreCase))
        {
            return typeName[..^6];
        }

        if (typeName.EndsWith("ValueObject", StringComparison.OrdinalIgnoreCase))
        {
            return typeName[..^11];
        }

        return typeName;
    }

    private string GetIdentifier(object value)
    {
        var idProperty = value?.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var idValue = idProperty?.GetValue(value);
        return idValue switch
        {
            null => null,
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            _ => Convert.ToString(idValue, CultureInfo.InvariantCulture)
        };
    }

    private string GetPayloadColumnName(string propertyName)
    {
        return propertyName switch
        {
            "Name" => "AddressName",
            "Line1" => "Street",
            _ => propertyName
        };
    }

    private string FormatValue(object value, ColumnConfiguration column, ExportConfiguration configuration)
    {
        if (value is null)
        {
            return column.NullValue ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(column.Format) && value is IFormattable formattable)
        {
            return formattable.ToString(column.Format, configuration.Culture);
        }

        return this.FormatPrimitive(value, configuration.Culture);
    }

    private string FormatPrimitive(object value, CultureInfo culture)
    {
        return value switch
        {
            null => string.Empty,
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, culture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private object CreateCollectionInstance(Type collectionType)
    {
        if (!collectionType.IsInterface && !collectionType.IsAbstract)
        {
            return Activator.CreateInstance(collectionType);
        }

        var elementType = this.GetCollectionElementType(collectionType) ?? typeof(object);
        return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
    }

    private void AddCollectionItem(object collection, object item)
    {
        if (collection is IList list)
        {
            list.Add(item);
            return;
        }

        collection.GetType().GetMethod("Add")?.Invoke(collection, [item]);
    }

    private Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        return collectionType.GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private object ConvertToType(string value, Type targetType, CultureInfo culture)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType is not null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            targetType = underlyingType;
        }

        if (targetType == typeof(int))
        {
            return int.Parse(value, culture);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        return Convert.ChangeType(value, targetType, culture);
    }

    private sealed record CsvTypedRow(
        string RecordType,
        string RootId,
        string RecordId,
        string ParentId,
        string Collection,
        int? Index,
        Dictionary<string, string> Payload);
}
