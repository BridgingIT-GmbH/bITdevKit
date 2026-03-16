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
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider
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
    public async Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var options = this.configuration.GetSerializerOptions();
        var dataList = data.ToList();
        var jsonOptions = new JsonSerializerOptions(options)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        // Transform data if columns are configured
        var exportData = dataList
            .Select(item => this.CreateExportRow(item, exportConfiguration))
            .ToList();

        await JsonSerializer.SerializeAsync(outputStream, exportData, jsonOptions, cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            TotalRows = dataList.Count,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var options = this.configuration.GetSerializerOptions();
        var jsonOptions = new JsonSerializerOptions(options)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        var result = new Dictionary<string, List<Dictionary<string, object>>>();
        var totalRows = 0;

        foreach (var (data, exportConfiguration) in dataSets)
        {
            var sheetName = exportConfiguration.SheetName ?? $"Sheet{result.Count + 1}";
            var dataList = data.ToList();

            var exportData = dataList
                .Select(item => this.CreateExportRow(item, exportConfiguration))
                .ToList();

            result[sheetName] = exportData;
            totalRows += dataList.Count;
        }

        await JsonSerializer.SerializeAsync(outputStream, result, jsonOptions, cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
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
            if (result.FatalError is not null)
            {
                failedRows++;
                totalRows += result.RowNumber > 0 ? 1 : 0;
                errors.Add(new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                });
                break;
            }

            totalRows++;

            if (result.Item is not null)
            {
                results.Add(result.Item);
            }

            if (result.Errors.Count > 0)
            {
                failedRows++;
                errors.AddRange(result.Errors);
            }
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
        await foreach (var result in this.ProcessRowsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            if (result.FatalError is not null)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(result.FatalError);
                yield break;
            }

            if (result.Item is not null)
            {
                yield return Result<TTarget>.Success(result.Item);
            }
            else if (result.Errors.Count > 0)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportValidationError(
                        result.Errors[0].RowNumber,
                        result.Errors[0].Column,
                        result.Errors[0].Message,
                        result.Errors[0].RawValue));
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
                errors.Add(new ImportRowError
                {
                    RowNumber = result.RowNumber,
                    Column = "N/A",
                    Message = result.FatalError.Message,
                    Severity = ErrorSeverity.Critical
                });
                break;
            }

            totalRows++;
            if (result.Errors.Count == 0)
            {
                validRows++;
            }
            else
            {
                errors.AddRange(result.Errors);
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
                var convertedValue = this.ConvertJsonElement(jsonValue, column);
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

    private object ConvertJsonElement(JsonElement element, ImportColumnConfiguration column)
    {
        var options = this.configuration.GetSerializerOptions();
        var targetType = column.PropertyInfo?.PropertyType ?? typeof(string);

        if (column.Converter is not null)
        {
            return element.ValueKind == JsonValueKind.String
                ? column.ConvertValue(element.GetString())
                : column.ConvertValue(element.GetRawText());
        }

        if (targetType.SupportsStructuredValue())
        {
            return JsonSerializer.Deserialize(element.GetRawText(), targetType, options);
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => column.ConvertValue(element.GetString()),
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
