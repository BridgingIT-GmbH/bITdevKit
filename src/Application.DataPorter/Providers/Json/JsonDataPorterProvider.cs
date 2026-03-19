// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Runtime.CompilerServices;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// JSON data porter provider using System.Text.Json.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JsonDataPorterProvider"/> class.
/// </remarks>
/// <param name="configuration">The JSON configuration.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class JsonDataPorterProvider(
    JsonConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider, IDataTemplateProvider
{
    private readonly JsonConfiguration configuration = configuration ?? new JsonConfiguration();
    private readonly ILogger<JsonDataPorterProvider> logger = loggerFactory?.CreateLogger<JsonDataPorterProvider>() ?? NullLogger<JsonDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.Json;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".json"];

    /// <inheritdoc/>
    public bool SupportsImport => true;

    /// <inheritdoc/>
    public bool SupportsExport => true;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public bool SupportsTemplateExport => true;

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var jsonOptions = this.CreateJsonSerializerOptions();
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        var executor = exportConfiguration.GetExportRowInterceptionExecutor<TSource>();
        using var writer = new Utf8JsonWriter(writeStream, new JsonWriterOptions
        {
            Indented = jsonOptions.WriteIndented
        });

        writer.WriteStartArray();
        var totalRows = 0;

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
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, skippedRows: skippedRows);
                continue;
            }

            if (interception?.Outcome == RowInterceptionOutcome.Abort)
            {
                throw new ExportInterceptionAbortedException(interception.Reason);
            }

            this.WriteExportRow(writer, interception?.Item ?? item, exportConfiguration, jsonOptions);
            totalRows++;
            exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, skippedRows: skippedRows);
            if (interception is not null)
            {
                await executor.AfterAsync(interception, cancellationToken);
            }
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
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
        var jsonOptions = this.CreateJsonSerializerOptions();
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        var executor = exportConfiguration.GetExportRowInterceptionExecutor<TSource>();
        using var writer = new Utf8JsonWriter(writeStream, new JsonWriterOptions
        {
            Indented = jsonOptions.WriteIndented
        });

        writer.WriteStartArray();
        var totalRows = 0;

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
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, skippedRows: skippedRows);
                continue;
            }

            if (interception?.Outcome == RowInterceptionOutcome.Abort)
            {
                throw new ExportInterceptionAbortedException(interception.Reason);
            }

            this.WriteExportRow(writer, interception?.Item ?? item, exportConfiguration, jsonOptions);
            totalRows++;
            exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, skippedRows: skippedRows);
            if (interception is not null)
            {
                await executor.AfterAsync(interception, cancellationToken);
            }
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
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
        var jsonOptions = this.CreateJsonSerializerOptions();
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        using var writer = new Utf8JsonWriter(writeStream, new JsonWriterOptions
        {
            Indented = jsonOptions.WriteIndented
        });
        var totalRows = 0;
        var index = 0;

        writer.WriteStartObject();

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheetName = exportConfiguration.SheetName ?? $"Sheet{++index}";
            writer.WritePropertyName(sheetName);
            writer.WriteStartArray();

            foreach (var item in data)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logicalRowNumber++;
                var interception = await ObjectExportRowInterceptionInvoker.BeforeAsync(
                    exportConfiguration.RowInterceptionExecutor,
                    item,
                    logicalRowNumber,
                    this.Format,
                    sheetName,
                    false,
                    cancellationToken);

                if (interception.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                    exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}", skippedRows: skippedRows);
                    continue;
                }

                if (interception.Outcome == RowInterceptionOutcome.Abort)
                {
                    throw new ExportInterceptionAbortedException(interception.Reason);
                }

                this.WriteExportRow(writer, interception.Item, exportConfiguration, jsonOptions);
                totalRows++;
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}", skippedRows: skippedRows);
                await ObjectExportRowInterceptionInvoker.AfterAsync(exportConfiguration.RowInterceptionExecutor, interception.State, cancellationToken);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
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
        var jsonOptions = this.CreateJsonSerializerOptions();
        var writeStream = new WriteStreamWrapper(outputStream);
        var warnings = new List<string>();
        var skippedRows = 0;
        var logicalRowNumber = 0;
        using var writer = new Utf8JsonWriter(writeStream, new JsonWriterOptions
        {
            Indented = jsonOptions.WriteIndented
        });
        var totalRows = 0;
        var index = 0;

        writer.WriteStartObject();

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheetName = exportConfiguration.SheetName ?? $"Sheet{++index}";
            writer.WritePropertyName(sheetName);
            writer.WriteStartArray();

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                logicalRowNumber++;
                var interception = await ObjectExportRowInterceptionInvoker.BeforeAsync(
                    exportConfiguration.RowInterceptionExecutor,
                    item,
                    logicalRowNumber,
                    this.Format,
                    sheetName,
                    true,
                    cancellationToken);

                if (interception.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    warnings.Add($"Row {logicalRowNumber} skipped by export interceptor: {interception.Reason}");
                    exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}", skippedRows: skippedRows);
                    continue;
                }

                if (interception.Outcome == RowInterceptionOutcome.Abort)
                {
                    throw new ExportInterceptionAbortedException(interception.Reason);
                }

                this.WriteExportRow(writer, interception.Item, exportConfiguration, jsonOptions);
                totalRows++;
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}", skippedRows: skippedRows);
                await ObjectExportRowInterceptionInvoker.AfterAsync(exportConfiguration.RowInterceptionExecutor, interception.State, cancellationToken);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
            SkippedRows = skippedRows,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> GenerateTemplateAsync<TTarget>(
        Stream outputStream,
        TemplateConfiguration configuration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var jsonOptions = this.CreateJsonSerializerOptions();
        var writeStream = new WriteStreamWrapper(outputStream);
        var fields = this.GetOrderedTemplateFields(configuration);
        using var writer = new Utf8JsonWriter(writeStream, new JsonWriterOptions
        {
            Indented = jsonOptions.WriteIndented
        });

        if (configuration.UseMetadataWrapper)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("metadata");
            this.WriteTemplateMetadata(writer, configuration, fields);
            writer.WritePropertyName("data");
            this.WriteTemplateSamples(writer, configuration, fields);
            writer.WriteEndObject();
        }
        else
        {
            this.WriteTemplateSamples(writer, configuration, fields);
        }

        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = configuration.SampleItemCount,
            Duration = TimeSpan.Zero,
            Format = this.Format,
            Properties = [.. new Dictionary<string, object> { ["template"] = true }]
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
        var warnings = new List<string>();
        var totalRows = 0;
        var failedRows = 0;
        var skippedRows = 0;
        var executor = importConfiguration.GetImportRowInterceptionExecutor<TTarget>();

        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            if (result.FatalError is not null)
            {
                failedRows++;
                totalRows += result.RowNumber > 0 ? 1 : 0;
                ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                }, importConfiguration);
                break;
            }

            totalRows++;

            if (result.Item is not null)
            {
                var interception = executor is not null
                    ? await executor.BeforeAsync(result.Item, result.RowNumber, this.Format, importConfiguration.SheetName, false, cancellationToken)
                    : null;

                if (interception?.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    failedRows++;
                    warnings.Add($"Row {result.RowNumber} skipped by import interceptor: {interception.Reason}");
                    importConfiguration.ProgressTracker?.ReportProgress(totalRows, results.Count, failedRows, errors.Count, skippedRows: skippedRows);
                    continue;
                }

                if (interception?.Outcome == RowInterceptionOutcome.Abort)
                {
                    throw new ImportInterceptionAbortedException(interception.Reason);
                }

                results.Add(interception?.Item ?? result.Item);
                if (interception is not null)
                {
                    await executor.AfterAsync(interception, cancellationToken);
                }
            }

            if (result.Errors.Count > 0)
            {
                failedRows++;
                if (ImportErrorLimit.TryAddRange(errors, result.Errors, importConfiguration))
                {
                    importConfiguration.ProgressTracker?.ReportProgress(totalRows, results.Count, failedRows, errors.Count);
                    break;
                }
            }

            importConfiguration.ProgressTracker?.ReportProgress(totalRows, results.Count, failedRows, errors.Count, skippedRows: skippedRows);
        }

        return new ImportResult<TTarget>
        {
            Data = results,
            TotalRows = totalRows,
            SuccessfulRows = results.Count,
            FailedRows = failedRows,
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
        var errorCount = 0;
        var totalRows = 0;
        var successfulRows = 0;
        var failedRows = 0;
        var skippedRows = 0;
        var executor = importConfiguration.GetImportRowInterceptionExecutor<TTarget>();

        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            if (result.FatalError is not null)
            {
                totalRows += result.RowNumber > 0 ? 1 : 0;
                failedRows++;
                errorCount++;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                yield return Result<TTarget>.Failure()
                    .WithError(result.FatalError);
                yield break;
            }

            totalRows++;
            if (result.Item is not null)
            {
                var interception = executor is not null
                    ? await executor.BeforeAsync(result.Item, result.RowNumber, this.Format, importConfiguration.SheetName, true, cancellationToken)
                    : null;

                if (interception?.Outcome == RowInterceptionOutcome.Skip)
                {
                    skippedRows++;
                    failedRows++;
                    importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                    continue;
                }

                if (interception?.Outcome == RowInterceptionOutcome.Abort)
                {
                    yield return Result<TTarget>.Failure()
                        .WithError(new ImportInterceptionAbortedError(interception.Reason));
                    yield break;
                }

                successfulRows++;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
                if (interception is not null)
                {
                    await executor.AfterAsync(interception, cancellationToken);
                }
                yield return Result<TTarget>.Success(interception?.Item ?? result.Item);
            }
            else if (result.Errors.Count > 0)
            {
                failedRows++;
                errorCount += result.Errors.Count;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount, skippedRows: skippedRows);
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
            if (result.FatalError is not null)
            {
                totalRows += result.RowNumber > 0 ? 1 : 0;
                ImportErrorLimit.TryAdd(errors, new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                }, importConfiguration);
                break;
            }

            totalRows++;
            if (result.Errors.Count == 0)
            {
                validRows++;
            }
            else
            {
                if (ImportErrorLimit.TryAddRange(errors, result.Errors, importConfiguration))
                {
                    break;
                }
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success(totalRows)
            : ValidationResult.Failure(totalRows, validRows, errors);
    }

    private Dictionary<string, object> CreateExportRow(
        object item,
        ExportConfiguration exportConfiguration)
    {
        var dict = new Dictionary<string, object>();
        foreach (var column in exportConfiguration.Columns)
        {
            var value = column.GetValue(item);

            if (column.Converter is not null)
            {
                var context = new ValueConversionContext
                {
                    PropertyName = column.PropertyName,
                    PropertyType = column.PropertyInfo?.PropertyType ?? typeof(object),
                    EntityType = exportConfiguration.SourceType,
                    Format = column.Format,
                    Culture = exportConfiguration.Culture
                };

                value = column.Converter.ConvertToExport(value, context);
            }

            dict[column.HeaderName ?? column.PropertyName] = value;
        }

        return dict;
    }

    private JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = this.configuration.GetSerializerOptions();
        return new JsonSerializerOptions(options)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
    }

    private void WriteExportRow(
        Utf8JsonWriter writer,
        object item,
        ExportConfiguration exportConfiguration,
        JsonSerializerOptions options)
    {
        var row = this.CreateExportRow(item, exportConfiguration);
        writer.WriteStartObject();

        foreach (var entry in row)
        {
            if (entry.Value is null && options.DefaultIgnoreCondition == System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)
            {
                continue;
            }

            writer.WritePropertyName(entry.Key);
            this.WriteJsonValue(writer, entry.Value, options);
        }

        writer.WriteEndObject();
    }

    private void WriteJsonValue(
        Utf8JsonWriter writer,
        object value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private void WriteTemplateMetadata(
        Utf8JsonWriter writer,
        TemplateConfiguration configuration,
        IReadOnlyList<TemplateFieldConfiguration> fields)
    {
        writer.WriteStartObject();
        writer.WriteString("targetType", configuration.TargetType.Name);
        writer.WriteString("format", this.Format.ToString());
        writer.WriteString("annotationStyle", configuration.AnnotationStyle.ToString());

        if (!string.IsNullOrWhiteSpace(configuration.SheetName))
        {
            writer.WriteString("sheetName", configuration.SheetName);
        }

        writer.WritePropertyName("fields");
        writer.WriteStartArray();

        foreach (var field in fields)
        {
            writer.WriteStartObject();
            writer.WriteString("name", field.HeaderName ?? field.PropertyName);
            writer.WriteString("propertyName", field.PropertyName);
            writer.WriteString("type", field.TypeName);
            writer.WriteBoolean("required", field.IsRequired);

            if (!string.IsNullOrWhiteSpace(field.RequiredMessage))
            {
                writer.WriteString("requiredMessage", field.RequiredMessage);
            }

            if (!string.IsNullOrWhiteSpace(field.Format))
            {
                writer.WriteString("format", field.Format);
            }

            if (configuration.IncludeHints && field.ValidationHints.Count > 0)
            {
                writer.WritePropertyName("validationHints");
                writer.WriteStartArray();
                foreach (var hint in field.ValidationHints)
                {
                    writer.WriteStringValue(hint);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private void WriteTemplateSamples(
        Utf8JsonWriter writer,
        TemplateConfiguration configuration,
        IReadOnlyList<TemplateFieldConfiguration> fields)
    {
        writer.WriteStartArray();

        for (var index = 0; index < configuration.SampleItemCount; index++)
        {
            writer.WriteStartObject();
            foreach (var field in fields)
            {
                writer.WritePropertyName(field.HeaderName ?? field.PropertyName);
                writer.WriteNullValue();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private IReadOnlyList<TemplateFieldConfiguration> GetOrderedTemplateFields(TemplateConfiguration configuration)
    {
        return [.. configuration.Fields
            .OrderBy(field => field.Order >= 0 ? field.Order : int.MaxValue)
            .ThenBy(field => field.HeaderName ?? field.PropertyName, StringComparer.OrdinalIgnoreCase)];
    }

    private async IAsyncEnumerable<JsonImportRowResult<TTarget>> ProcessRowsAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var options = this.configuration.GetSerializerOptions();
        var rowNumber = 0;
        var fatalResult = default(JsonImportRowResult<TTarget>);

        await using var rows = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
            inputStream,
            options,
            cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            JsonElement row;
            try
            {
                if (!await rows.MoveNextAsync())
                {
                    yield break;
                }

                row = rows.Current;
            }
            catch (JsonException ex)
            {
                fatalResult = new JsonImportRowResult<TTarget>(
                    rowNumber > 0 ? rowNumber + 1 : 0,
                    null,
                    [],
                    new ImportError($"Invalid JSON: {ex.Message}", ex));
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            JsonImportRowResult<TTarget> result;

            if (row.ValueKind != JsonValueKind.Object)
            {
                result = new JsonImportRowResult<TTarget>(
                    rowNumber,
                    null,
                    [],
                    new ImportError($"Invalid JSON row {rowNumber}: each JSON array item must be an object."));
            }
            else
            {
                var rowErrors = new List<ImportRowError>();
                TTarget item = null;
                ImportError fatalError = null;

                try
                {
                    item = this.MapRow<TTarget>(row.Clone(), importConfiguration, rowNumber, rowErrors);
                }
                catch (Exception ex)
                {
                    fatalError = new ImportError($"Row {rowNumber}: {ex.Message}", ex);
                }

                result = new JsonImportRowResult<TTarget>(rowNumber, item, rowErrors, fatalError);
            }

            yield return result;

            if (result.FatalError is not null ||
                (result.Errors.Count > 0 && importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport))
            {
                yield break;
            }
        }

        if (fatalResult is not null)
        {
            yield return fatalResult;
        }
    }

    private TTarget MapRow<TTarget>(
        JsonElement row,
        ImportConfiguration config,
        int rowNumber,
        List<ImportRowError> errors)
        where TTarget : class, new()
    {
        var item = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();

        var hasErrors = false;
        var assignments = new List<Action>();

        foreach (var column in config.Columns)
        {
            if (column.Ignore)
            {
                continue;
            }

            var key = column.SourceName ?? column.PropertyName;
            var hasValue = row.TryGetProperty(key, out var jsonValue);
            var rawValue = hasValue ? jsonValue.ToString() : null;

            try
            {
                // Validate required
                if (column.IsRequired && (!hasValue || jsonValue.ValueKind == JsonValueKind.Null))
                {
                    hasErrors = true;
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = key,
                        Message = column.RequiredMessage ?? $"{column.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });

                    continue;
                }

                if (!hasValue || jsonValue.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                // Run custom validators
                foreach (var validator in column.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        hasErrors = true;
                        errors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = key,
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

                // Convert and set value
                var convertedValue = this.ConvertJsonElement(jsonValue, column, config);
                assignments.Add(() => column.SetValue(item, convertedValue));
            }
            catch (Exception ex)
            {
                hasErrors = true;
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Column = key,
                    Message = $"Failed to convert value: {ex.Message}",
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                });
            }
        }

        if (hasErrors)
        {
            return null;
        }

        foreach (var assignment in assignments)
        {
            assignment();
        }

        return item;
    }

    private object ConvertJsonElement(JsonElement element, ImportColumnConfiguration column, ImportConfiguration config)
    {
        var options = this.configuration.GetSerializerOptions();
        var targetType = column.PropertyInfo?.PropertyType ?? typeof(string);

        if (column.Converter is not null)
        {
            return element.ValueKind == JsonValueKind.String
                ? column.ConvertValue(element.GetString(), config.Culture)
                : column.ConvertValue(element.GetRawText(), config.Culture);
        }

        if (targetType.SupportsStructuredValue())
        {
            return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => column.ConvertValue(element.GetString(), config.Culture),
            JsonValueKind.Number when targetType == typeof(int) || targetType == typeof(int?) => element.GetInt32(),
            JsonValueKind.Number when targetType == typeof(long) || targetType == typeof(long?) => element.GetInt64(),
            JsonValueKind.Number when targetType == typeof(decimal) || targetType == typeof(decimal?) => element.GetDecimal(),
            JsonValueKind.Number when targetType == typeof(double) || targetType == typeof(double?) => element.GetDouble(),
            JsonValueKind.Number when targetType == typeof(float) || targetType == typeof(float?) => element.GetSingle(),
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private sealed record JsonImportRowResult<TTarget>(
        int RowNumber,
        TTarget Item,
        IReadOnlyList<ImportRowError> Errors,
        ImportError FatalError)
        where TTarget : class;
}
