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
/// CSV data porter provider using CsvHelper.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CsvDataPorterProvider"/> class.
/// </remarks>
/// <param name="configuration">The CSV configuration.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class CsvDataPorterProvider(
    CsvConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider
{
    private readonly CsvConfiguration configuration = configuration ?? new CsvConfiguration();
    private readonly ILogger<CsvDataPorterProvider> logger = loggerFactory?.CreateLogger<CsvDataPorterProvider>() ?? NullLogger<CsvDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.Csv;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".csv", ".tsv"];

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
        var plan = this.BuildExportPlan(exportConfiguration);
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = exportConfiguration.IncludeHeaders
        });

        // Write headers
        if (exportConfiguration.IncludeHeaders)
        {
            foreach (var column in plan.Columns)
            {
                csv.WriteField(column.HeaderName);
            }

            await csv.NextRecordAsync();
        }

        var rowsExported = 0;

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var collectionItem in this.GetCollectionItems(item, plan.CollectionProperty))
            {
                this.WriteExportRow(csv, plan, item, collectionItem, exportConfiguration);
                await csv.NextRecordAsync();
                rowsExported++;
                exportConfiguration.ProgressTracker?.ReportProgress(rowsExported, writeStream.BytesWritten);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsExported,
            Duration = TimeSpan.Zero,
            Format = this.Format
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
        var plan = this.BuildExportPlan(exportConfiguration);
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = exportConfiguration.IncludeHeaders
        });

        if (exportConfiguration.IncludeHeaders)
        {
            foreach (var column in plan.Columns)
            {
                csv.WriteField(column.HeaderName);
            }

            await csv.NextRecordAsync();
        }

        var rowsExported = 0;

        await foreach (var item in data.WithCancellation(cancellationToken))
        {
            foreach (var collectionItem in this.GetCollectionItems(item, plan.CollectionProperty))
            {
                this.WriteExportRow(csv, plan, item, collectionItem, exportConfiguration);
                await csv.NextRecordAsync();
                rowsExported++;
                exportConfiguration.ProgressTracker?.ReportProgress(rowsExported, writeStream.BytesWritten);
            }
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = rowsExported,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data,
        ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var totalRows = 0;
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = this.BuildExportPlan(exportConfiguration);

            // Write section header
            await writer.WriteLineAsync($"# {exportConfiguration.SheetName ?? "Data"}");

            await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
            {
                Delimiter = this.configuration.Delimiter,
                HasHeaderRecord = exportConfiguration.IncludeHeaders
            }, leaveOpen: true);

            // Write headers
            if (exportConfiguration.IncludeHeaders)
            {
                foreach (var column in plan.Columns)
                {
                    csv.WriteField(column.HeaderName);
                }

                await csv.NextRecordAsync();
            }

            foreach (var item in data)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var collectionItem in this.GetCollectionItems(item, plan.CollectionProperty))
                {
                    this.WriteExportRow(csv, plan, item, collectionItem, exportConfiguration);
                    await csv.NextRecordAsync();
                    totalRows++;
                    exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {exportConfiguration.SheetName ?? "Data"}");
                }
            }

            await csv.FlushAsync();
            await writer.WriteLineAsync();
        }

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var totalRows = 0;
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = new StreamWriter(writeStream, this.configuration.Encoding, leaveOpen: true);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = this.BuildExportPlan(exportConfiguration);

            await writer.WriteLineAsync($"# {exportConfiguration.SheetName ?? "Data"}");

            await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
            {
                Delimiter = this.configuration.Delimiter,
                HasHeaderRecord = exportConfiguration.IncludeHeaders
            }, leaveOpen: true);

            if (exportConfiguration.IncludeHeaders)
            {
                foreach (var column in plan.Columns)
                {
                    csv.WriteField(column.HeaderName);
                }

                await csv.NextRecordAsync();
            }

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                foreach (var collectionItem in this.GetCollectionItems(item, plan.CollectionProperty))
                {
                    this.WriteExportRow(csv, plan, item, collectionItem, exportConfiguration);
                    await csv.NextRecordAsync();
                    totalRows++;
                    exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {exportConfiguration.SheetName ?? "Data"}");
                }
            }

            await csv.FlushAsync();
            await writer.WriteLineAsync();
        }

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var results = new List<TTarget>();
        var errors = new List<ImportRowError>();
        var totalRows = 0;
        var failedRows = 0;

        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            totalRows += result.ProcessedRows;

            if (result.Item is not null)
            {
                results.Add(result.Item);
            }

            if (result.FatalError is not null)
            {
                failedRows += result.ProcessedRows > 0 ? result.ProcessedRows : 1;
                ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                }, importConfiguration);
                break;
            }

            if (result.Errors.Count > 0)
            {
                failedRows += result.ProcessedRows > 0 ? result.ProcessedRows : result.Errors.Count;
                if (ImportErrorLimit.TryAddRange(errors, result.Errors, importConfiguration))
                {
                    importConfiguration.ProgressTracker?.ReportProgress(totalRows, results.Count, failedRows, errors.Count);
                    break;
                }
            }

            importConfiguration.ProgressTracker?.ReportProgress(totalRows, results.Count, failedRows, errors.Count);
        }

        return new ImportResult<TTarget>
        {
            Data = results,
            TotalRows = totalRows,
            SuccessfulRows = results.Count,
            FailedRows = failedRows,
            Duration = TimeSpan.Zero,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var errorCount = 0;
        var totalRows = 0;
        var successfulRows = 0;
        var failedRows = 0;

        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            if (result.FatalError is not null)
            {
                totalRows += result.ProcessedRows > 0 ? result.ProcessedRows : 1;
                failedRows += result.ProcessedRows > 0 ? result.ProcessedRows : 1;
                errorCount++;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount);
                yield return Result<TTarget>.Failure()
                    .WithError(result.FatalError);
                yield break;
            }

            if (result.Item is not null)
            {
                totalRows += result.ProcessedRows;
                successfulRows += result.ProcessedRows;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount);
                yield return Result<TTarget>.Success(result.Item);
            }
            else if (result.Errors.Count > 0)
            {
                totalRows += result.ProcessedRows > 0 ? result.ProcessedRows : result.Errors.Count;
                failedRows += result.ProcessedRows > 0 ? result.ProcessedRows : result.Errors.Count;
                errorCount += result.Errors.Count;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount);
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportValidationError(
                        result.Errors[0].RowNumber,
                        result.Errors[0].Column,
                        result.Errors[0].Message,
                        result.Errors[0].RawValue));

                if (ImportErrorLimit.IsReached(importConfiguration, errorCount))
                {
                    yield break;
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
        var errors = new List<ImportRowError>();
        var totalRows = 0;
        var validRows = 0;

        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            totalRows += result.ProcessedRows;

            if (result.FatalError is not null)
            {
                ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                }, importConfiguration);
                break;
            }

            if (result.Errors.Count > 0)
            {
                if (ImportErrorLimit.TryAddRange(errors, result.Errors, importConfiguration))
                {
                    break;
                }
            }
            else
            {
                validRows += result.ProcessedRows;
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success(totalRows)
            : ValidationResult.Failure(totalRows, validRows, errors);
    }

    private async IAsyncEnumerable<CsvImportRowResult<TTarget>> ProcessRowsAsync<TTarget>(
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

        for (var i = 0; i < importConfiguration.HeaderRowIndex; i++)
        {
            if (!await csv.ReadAsync())
            {
                yield break;
            }
        }

        if (!await csv.ReadAsync())
        {
            yield break;
        }

        csv.ReadHeader();

        var plan = this.BuildImportPlan(importConfiguration, csv.HeaderRecord ?? []);
        var headerErrors = this.CreateMissingColumnErrors(plan.MissingColumns, importConfiguration.HeaderRowIndex);
        if (headerErrors.Count > 0)
        {
            foreach (var error in headerErrors)
            {
                yield return new CsvImportRowResult<TTarget>(error.RowNumber, 0, null, [error], null);
            }

            yield break;
        }

        for (var i = 0; i < importConfiguration.SkipRows; i++)
        {
            if (!await csv.ReadAsync())
            {
                yield break;
            }
        }

        if (plan.CollectionProperty is null)
        {
            var rowNumber = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows + 1;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                var result = this.ProcessFlatRow<TTarget>(csv, plan, importConfiguration, rowNumber);
                yield return result;

                if ((result.FatalError is not null || result.Errors.Count > 0) &&
                    importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                {
                    yield break;
                }
            }

            yield break;
        }

        var completedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentKey = default(string);
        var currentItem = default(TTarget);
        var currentHasSuccessfulRows = false;
        var rowNumberForGroup = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows + 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumberForGroup++;

            IReadOnlyDictionary<string, string> rowValues = null;
            CsvImportRowResult<TTarget> rowResult = null;

            try
            {
                rowValues = this.ReadRowValues(csv, plan.Columns);
            }
            catch (Exception ex)
            {
                rowResult = new CsvImportRowResult<TTarget>(
                    rowNumberForGroup,
                    1,
                    null,
                    [],
                    new ImportError($"Row {rowNumberForGroup}: {ex.Message}", ex));
            }

            if (rowResult is not null)
            {
                yield return rowResult;

                if (importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                {
                    yield break;
                }

                continue;
            }

            var rowKey = this.GetGroupingKey(rowValues, plan);

            if (currentKey is null)
            {
                currentKey = rowKey;
                currentItem = importConfiguration.Factory is not null
                    ? (TTarget)importConfiguration.Factory()
                    : new TTarget();
            }
            else if (!string.Equals(currentKey, rowKey, StringComparison.OrdinalIgnoreCase))
            {
                if (currentHasSuccessfulRows)
                {
                    yield return new CsvImportRowResult<TTarget>(rowNumberForGroup - 1, 0, currentItem, [], null);
                }

                completedKeys.Add(currentKey);

                if (completedKeys.Contains(rowKey))
                {
                    yield return new CsvImportRowResult<TTarget>(
                        rowNumberForGroup,
                        1,
                        null,
                        [],
                        new ImportError($"Row {rowNumberForGroup}: non-contiguous grouped rows are not supported for streaming CSV import."));
                    yield break;
                }

                currentKey = rowKey;
                currentItem = importConfiguration.Factory is not null
                    ? (TTarget)importConfiguration.Factory()
                    : new TTarget();
                currentHasSuccessfulRows = false;
            }

            var rowErrors = new List<ImportRowError>();

            try
            {
                var success = this.MapRow(rowValues, currentItem, plan, importConfiguration, rowNumberForGroup, rowErrors);
                rowResult = success
                    ? new CsvImportRowResult<TTarget>(rowNumberForGroup, 1, null, [], null)
                    : new CsvImportRowResult<TTarget>(rowNumberForGroup, 1, null, rowErrors, null);

                if (success)
                {
                    currentHasSuccessfulRows = true;
                }
            }
            catch (Exception ex)
            {
                rowResult = new CsvImportRowResult<TTarget>(
                    rowNumberForGroup,
                    1,
                    null,
                    [],
                    new ImportError($"Row {rowNumberForGroup}: {ex.Message}", ex));
            }

            yield return rowResult;

            if ((rowResult.FatalError is not null || rowResult.Errors.Count > 0) &&
                importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
            {
                yield break;
            }
        }

        if (currentKey is not null && currentHasSuccessfulRows)
        {
            yield return new CsvImportRowResult<TTarget>(rowNumberForGroup, 0, currentItem, [], null);
        }
    }

    private CsvImportRowResult<TTarget> ProcessFlatRow<TTarget>(
        CsvReader csv,
        CsvImportPlan plan,
        ImportConfiguration importConfiguration,
        int rowNumber)
        where TTarget : class, new()
    {
        var rowErrors = new List<ImportRowError>();

        try
        {
            var rowValues = this.ReadRowValues(csv, plan.Columns);
            var item = importConfiguration.Factory is not null
                ? (TTarget)importConfiguration.Factory()
                : new TTarget();
            var success = this.MapRow(rowValues, item, plan, importConfiguration, rowNumber, rowErrors);

            return success
                ? new CsvImportRowResult<TTarget>(rowNumber, 1, item, [], null)
                : new CsvImportRowResult<TTarget>(rowNumber, 1, null, rowErrors, null);
        }
        catch (Exception ex)
        {
            return new CsvImportRowResult<TTarget>(
                rowNumber,
                1,
                null,
                [],
                new ImportError($"Row {rowNumber}: {ex.Message}", ex));
        }
    }

    private CsvExportPlan BuildExportPlan(ExportConfiguration config)
    {
        var columns = new List<CsvExportColumn>();
        PropertyInfo collectionProperty = null;
        var typePath = new HashSet<Type> { config.SourceType };

        foreach (var column in config.Columns)
        {
            if (column.Ignore || this.ShouldIgnoreNestedColumn(column))
            {
                continue;
            }

            this.AddExportColumns(columns, column, [column.PropertyInfo], column.HeaderName ?? column.PropertyName, ref collectionProperty, typePath);
        }

        return new CsvExportPlan(columns, collectionProperty);
    }

    private CsvImportPlan BuildImportPlan(ImportConfiguration config, IReadOnlyList<string> headers)
    {
        var columns = new List<CsvImportColumn>();
        var missingColumns = new List<ImportColumnConfiguration>();
        PropertyInfo collectionProperty = null;
        var typePath = new HashSet<Type> { config.TargetType };

        foreach (var column in config.Columns)
        {
            if (column.Ignore || this.ShouldIgnoreNestedColumn(column))
            {
                continue;
            }

            this.AddImportColumns(columns, column, [column.PropertyInfo], column.SourceName ?? column.PropertyName, ref collectionProperty, typePath);
        }

        var mappedColumns = columns
            .Select(column =>
            {
                var header = headers.FirstOrDefault(h => h.Equals(column.HeaderName, StringComparison.OrdinalIgnoreCase));
                if (header is null)
                {
                    missingColumns.Add(column.SourceColumn);
                    return null;
                }

                return column with { HeaderName = header };
            })
            .Where(column => column is not null)
            .Cast<CsvImportColumn>()
            .ToList();

        if (mappedColumns.All(column => !column.IsCollection))
        {
            collectionProperty = null;
        }

        return new CsvImportPlan(mappedColumns, missingColumns, collectionProperty);
    }

    private List<ImportRowError> CreateMissingColumnErrors(
        IReadOnlyList<ImportColumnConfiguration> missingColumns,
        int headerRowIndex)
    {
        return [.. missingColumns
            .Where(column => column.IsRequired)
            .Select(column => new ImportRowError
            {
                RowNumber = headerRowIndex + 1,
                Column = column.SourceName ?? column.PropertyName,
                Message = column.RequiredMessage ?? $"Required column header '{column.SourceName ?? column.PropertyName}' was not found.",
                Severity = ErrorSeverity.Error
            })];
    }

    private void AddExportColumns(
        List<CsvExportColumn> columns,
        ColumnConfiguration sourceColumn,
        IReadOnlyList<PropertyInfo> path,
        string headerName,
        ref PropertyInfo collectionProperty,
        HashSet<Type> typePath)
    {
        var propertyType = path[^1].PropertyType;
        var topLevelProperty = path[0];

        if (sourceColumn.Converter is not null || !propertyType.SupportsStructuredValue())
        {
            columns.Add(new CsvExportColumn(sourceColumn, headerName, [.. path], collectionProperty == topLevelProperty));
            return;
        }

        if (propertyType.IsCollectionType())
        {
            if (path.Count > 1)
            {
                throw new NotSupportedException($"CSV export supports collection flattening only for top-level collection column '{sourceColumn.PropertyName}'.");
            }

            if (collectionProperty is not null && collectionProperty != topLevelProperty)
            {
                throw new NotSupportedException("CSV export supports only one nested collection column at a time.");
            }

            collectionProperty = topLevelProperty;
            var elementType = this.GetCollectionElementType(propertyType) ?? throw new NotSupportedException($"CSV export cannot determine the element type for collection column '{sourceColumn.PropertyName}'.");
            if (!elementType.SupportsStructuredValue())
            {
                columns.Add(new CsvExportColumn(sourceColumn, headerName, [.. path], true));
                return;
            }

            if (!typePath.Add(elementType))
            {
                return;
            }

            foreach (var property in this.GetFlattenableProperties(elementType, writable: false))
            {
                this.AddExportColumns(columns, sourceColumn, [.. path, property], $"{headerName}_{property.Name}", ref collectionProperty, typePath);
            }

            typePath.Remove(elementType);

            return;
        }

        if (!typePath.Add(propertyType))
        {
            return;
        }

        foreach (var property in this.GetFlattenableProperties(propertyType, writable: false))
        {
            this.AddExportColumns(columns, sourceColumn, [.. path, property], $"{headerName}_{property.Name}", ref collectionProperty, typePath);
        }

        typePath.Remove(propertyType);
    }

    private void AddImportColumns(
        List<CsvImportColumn> columns,
        ImportColumnConfiguration sourceColumn,
        IReadOnlyList<PropertyInfo> path,
        string headerName,
        ref PropertyInfo collectionProperty,
        HashSet<Type> typePath)
    {
        var propertyType = path[^1].PropertyType;
        var topLevelProperty = path[0];

        if (sourceColumn.Converter is not null || !propertyType.SupportsStructuredValue())
        {
            columns.Add(new CsvImportColumn(sourceColumn, headerName, [.. path], collectionProperty == topLevelProperty));
            return;
        }

        if (propertyType.IsCollectionType())
        {
            if (path.Count > 1)
            {
                throw new NotSupportedException($"CSV import supports collection flattening only for top-level collection column '{sourceColumn.PropertyName}'.");
            }

            if (collectionProperty is not null && collectionProperty != topLevelProperty)
            {
                throw new NotSupportedException("CSV import supports only one nested collection column at a time.");
            }

            collectionProperty = topLevelProperty;
            var elementType = this.GetCollectionElementType(propertyType) ?? throw new NotSupportedException($"CSV import cannot determine the element type for collection column '{sourceColumn.PropertyName}'.");
            if (!elementType.SupportsStructuredValue())
            {
                columns.Add(new CsvImportColumn(sourceColumn, headerName, [.. path], true));
                return;
            }

            if (!typePath.Add(elementType))
            {
                return;
            }

            foreach (var property in this.GetFlattenableProperties(elementType, writable: true))
            {
                this.AddImportColumns(columns, sourceColumn, [.. path, property], $"{headerName}_{property.Name}", ref collectionProperty, typePath);
            }

            typePath.Remove(elementType);

            return;
        }

        if (!typePath.Add(propertyType))
        {
            return;
        }

        foreach (var property in this.GetFlattenableProperties(propertyType, writable: true))
        {
            this.AddImportColumns(columns, sourceColumn, [.. path, property], $"{headerName}_{property.Name}", ref collectionProperty, typePath);
        }

        typePath.Remove(propertyType);
    }

    private IEnumerable<object> GetCollectionItems(object source, PropertyInfo collectionProperty)
    {
        if (collectionProperty is null)
        {
            return [null];
        }

        if (collectionProperty.GetValue(source) is not IEnumerable collection)
        {
            return [null];
        }

        var items = collection.Cast<object>().ToList();
        return items.Count > 0 ? items : [null];
    }

    private void WriteExportRow(CsvWriter csv, CsvExportPlan plan, object item, object collectionItem, ExportConfiguration config)
    {
        foreach (var column in plan.Columns)
        {
            var value = column.GetValue(item, collectionItem);

            if (column.SourceColumn.Converter is not null)
            {
                var context = new ValueConversionContext
                {
                    PropertyName = column.SourceColumn.PropertyName,
                    PropertyType = column.SourceColumn.PropertyInfo?.PropertyType ?? typeof(object),
                    EntityType = config.SourceType,
                    Format = column.SourceColumn.Format,
                    Culture = config.Culture
                };

                value = column.SourceColumn.Converter.ConvertToExport(value, context);
            }

            csv.WriteField(this.FormatValue(value, column.SourceColumn, config));
        }
    }

    private Dictionary<string, string> ReadRowValues(CsvReader csv, IReadOnlyList<CsvImportColumn> columns)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            var rawValue = csv.GetField(column.HeaderName);
            if (this.configuration.TrimFields && rawValue is not null)
            {
                rawValue = rawValue.Trim();
            }

            values[column.HeaderName] = rawValue;
        }

        return values;
    }

    private PendingCsvItem<TTarget> GetOrCreateItem<TTarget>(
        IReadOnlyDictionary<string, string> rowValues,
        CsvImportPlan plan,
        ImportConfiguration config,
        IDictionary<string, TTarget> groupedResults)
        where TTarget : class, new()
    {
        if (plan.CollectionProperty is null)
        {
            var item = config.Factory is not null
                ? (TTarget)config.Factory()
                : new TTarget();
            return new PendingCsvItem<TTarget>(item, null, true);
        }

        var key = this.GetGroupingKey(rowValues, plan);
        if (groupedResults.TryGetValue(key, out var existing))
        {
            return new PendingCsvItem<TTarget>(existing, key, false);
        }

        var created = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();
        return new PendingCsvItem<TTarget>(created, key, true);
    }

    private string GetGroupingKey(IReadOnlyDictionary<string, string> rowValues, CsvImportPlan plan)
    {
        var idColumn = plan.Columns.FirstOrDefault(column => !column.IsCollection && column.SourceColumn.PropertyName == "Id");
        if (idColumn is not null && rowValues.TryGetValue(idColumn.HeaderName, out var idValue) && !string.IsNullOrWhiteSpace(idValue))
        {
            return idValue;
        }

        return string.Join("|", plan.Columns
            .Where(column => !column.IsCollection)
            .Select(column => rowValues.TryGetValue(column.HeaderName, out var value) ? value : string.Empty));
    }

    private bool MapRow<TTarget>(
        IReadOnlyDictionary<string, string> rowValues,
        TTarget item,
        CsvImportPlan plan,
        ImportConfiguration config,
        int rowNumber,
        List<ImportRowError> errors)
        where TTarget : class, new()
    {
        var hasErrors = false;
        var collectionColumns = plan.Columns.Where(column => column.IsCollection).ToList();
        var collectionItem = this.CreateCollectionItem(plan.CollectionProperty, collectionColumns, rowValues);
        var assignments = new List<Action>();

        foreach (var column in plan.Columns)
        {
            rowValues.TryGetValue(column.HeaderName, out var rawValue);

            try
            {
                if (column.SourceColumn.IsRequired && string.IsNullOrWhiteSpace(rawValue))
                {
                    hasErrors = true;
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = column.HeaderName,
                        Message = column.SourceColumn.RequiredMessage ?? $"{column.SourceColumn.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });

                    continue;
                }

                foreach (var validator in column.SourceColumn.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        hasErrors = true;
                        errors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = column.HeaderName,
                            Message = validator.ErrorMessage,
                            RawValue = rawValue,
                            Severity = ErrorSeverity.Error
                        });
                    }
                }

                if (hasErrors && config.ValidationBehavior == ImportValidationBehavior.SkipRow)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                var convertedValue = this.ConvertImportValue(column, rawValue, config);
                if (column.IsCollection)
                {
                    if (collectionItem is null)
                    {
                        continue;
                    }

                    assignments.Add(() => this.SetNestedValue(collectionItem, [.. column.PropertyPath.Skip(1)], convertedValue));
                }
                else
                {
                    assignments.Add(() => this.SetNestedValue(item, column.PropertyPath, convertedValue));
                }
            }
            catch (Exception ex)
            {
                hasErrors = true;
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Column = column.HeaderName,
                    Message = $"Failed to convert value: {ex.Message}",
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                });
            }
        }

        if (hasErrors)
        {
            return false;
        }

        foreach (var assignment in assignments)
        {
            assignment();
        }

        if (collectionItem is not null)
        {
            this.AddCollectionItem(item, plan.CollectionProperty, collectionItem);
        }

        return true;
    }

    private object ConvertImportValue(CsvImportColumn column, string rawValue, ImportConfiguration config)
    {
        if (column.SourceColumn.Converter is not null || column.SourceColumn.Parser is not null)
        {
            return column.SourceColumn.ConvertValue(rawValue, config.Culture);
        }

        return ConvertToType(rawValue, column.PropertyPath[^1].PropertyType, config.Culture);
    }

    private object CreateCollectionItem(
        PropertyInfo collectionProperty,
        IReadOnlyList<CsvImportColumn> collectionColumns,
        IReadOnlyDictionary<string, string> rowValues)
    {
        if (collectionProperty is null || collectionColumns.Count == 0)
        {
            return null;
        }

        var hasValues = collectionColumns.Any(column =>
            rowValues.TryGetValue(column.HeaderName, out var value) && !string.IsNullOrWhiteSpace(value));

        if (!hasValues)
        {
            return null;
        }

        var elementType = this.GetCollectionElementType(collectionProperty.PropertyType);
        return elementType is null ? null : Activator.CreateInstance(elementType);
    }

    private void AddCollectionItem(object target, PropertyInfo collectionProperty, object collectionItem)
    {
        if (collectionProperty is null || collectionItem is null)
        {
            return;
        }

        var collection = collectionProperty.GetValue(target);
        if (collection is null)
        {
            collection = this.CreateCollectionInstance(collectionProperty.PropertyType);
            collectionProperty.SetValue(target, collection);
        }

        if (collection is IList list)
        {
            list.Add(collectionItem);
            return;
        }

        collectionProperty.PropertyType.GetMethod("Add")?.Invoke(collection, [collectionItem]);
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

    private void SetNestedValue(object target, IReadOnlyList<PropertyInfo> path, object value)
    {
        var current = target;

        for (var i = 0; i < path.Count - 1; i++)
        {
            var property = path[i];
            var nested = property.GetValue(current);
            if (nested is null)
            {
                nested = Activator.CreateInstance(property.PropertyType);
                property.SetValue(current, nested);
            }

            current = nested;
        }

        path[^1].SetValue(current, value);
    }

    private IReadOnlyList<PropertyInfo> GetFlattenableProperties(Type type, bool writable)
    {
        return [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && (writable ? property.CanWrite : property.CanRead))];
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

        var enumerableType = collectionType.GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableType?.GetGenericArguments()[0];
    }

    private string FormatValue(object value, ColumnConfiguration column, ExportConfiguration config)
    {
        if (value is null)
        {
            return column.NullValue ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(column.Format) && value is IFormattable formattable)
        {
            return formattable.ToString(column.Format, config.Culture);
        }

        return value.ToString() ?? string.Empty;
    }

    private bool ShouldIgnoreNestedColumn(ColumnConfiguration column)
    {
        return !this.configuration.UseNesting
            && column.Converter is null
            && column.PropertyInfo?.PropertyType.SupportsStructuredValue() == true;
    }

    private bool ShouldIgnoreNestedColumn(ImportColumnConfiguration column)
    {
        return !this.configuration.UseNesting
            && column.Converter is null
            && column.PropertyInfo?.PropertyType.SupportsStructuredValue() == true;
    }

    private static object ConvertToType(string value, Type targetType, CultureInfo culture)
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

        if (targetType == typeof(long))
        {
            return long.Parse(value, culture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value, culture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(value, culture);
        }

        if (targetType == typeof(float))
        {
            return float.Parse(value, culture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(value, culture);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, culture);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        return Convert.ChangeType(value, targetType, culture);
    }

    private sealed record CsvExportPlan(IReadOnlyList<CsvExportColumn> Columns, PropertyInfo CollectionProperty);

    private sealed record CsvImportPlan(
        IReadOnlyList<CsvImportColumn> Columns,
        IReadOnlyList<ImportColumnConfiguration> MissingColumns,
        PropertyInfo CollectionProperty);

    private sealed record CsvExportColumn(ColumnConfiguration SourceColumn, string HeaderName, PropertyInfo[] PropertyPath, bool IsCollection)
    {
        public object GetValue(object source, object collectionItem)
        {
            var current = this.IsCollection ? collectionItem : source;

            foreach (var property in this.IsCollection ? this.PropertyPath.Skip(1) : this.PropertyPath.AsEnumerable())
            {
                if (current is null)
                {
                    return null;
                }

                current = property.GetValue(current);
            }

            return current;
        }
    }

    private sealed record CsvImportColumn(ImportColumnConfiguration SourceColumn, string HeaderName, PropertyInfo[] PropertyPath, bool IsCollection);

    private sealed record CsvImportRowResult<TTarget>(
        int RowNumber,
        int ProcessedRows,
        TTarget Item,
        IReadOnlyList<ImportRowError> Errors,
        ImportError FatalError)
        where TTarget : class;

    private sealed record PendingCsvItem<TTarget>(TTarget Item, string Key, bool IsNew)
        where TTarget : class;
}
