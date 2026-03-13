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
        var exportData = dataList.Select(item =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var column in exportConfiguration.Columns)
            {
                var value = column.GetValue(item);

                // Apply converter if present
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
        }).ToList();

        await JsonSerializer.SerializeAsync(outputStream, exportData, jsonOptions, cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = dataList.Count,
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

            var exportData = dataList.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var column in exportConfiguration.Columns)
                {
                    var value = column.GetValue(item);
                    dict[column.HeaderName ?? column.PropertyName] = value;
                }

                return dict;
            }).ToList();

            result[sheetName] = exportData;
            totalRows += dataList.Count;
        }

        await JsonSerializer.SerializeAsync(outputStream, result, jsonOptions, cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = totalRows,
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
        var options = this.configuration.GetSerializerOptions();
        var results = new List<TTarget>();
        var errors = new List<ImportRowError>();

        try
        {
            var jsonData = await JsonSerializer.DeserializeAsync<List<Dictionary<string, JsonElement>>>(
                inputStream, options, cancellationToken);

            if (jsonData is null)
            {
                return new ImportResult<TTarget>
                {
                    Data = [],
                    TotalRows = 0,
                    SuccessfulRows = 0,
                    FailedRows = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var rowNumber = 0;
            foreach (var row in jsonData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                try
                {
                    var item = this.MapRow<TTarget>(row, importConfiguration, rowNumber, errors);
                    if (item is not null)
                    {
                        results.Add(item);
                    }
                    else if (importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport && errors.Count > 0)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = "N/A",
                        Message = ex.Message,
                        Severity = ErrorSeverity.Error
                    });

                    if (importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                    {
                        break;
                    }
                }
            }

            return new ImportResult<TTarget>
            {
                Data = results,
                TotalRows = jsonData.Count,
                SuccessfulRows = results.Count,
                FailedRows = jsonData.Count - results.Count,
                Duration = TimeSpan.Zero,
                Errors = errors
            };
        }
        catch (JsonException ex)
        {
            return new ImportResult<TTarget>
            {
                Data = [],
                TotalRows = 0,
                SuccessfulRows = 0,
                FailedRows = 0,
                Duration = TimeSpan.Zero,
                Errors =
                [
                    new ImportRowError
                    {
                        RowNumber = 0,
                        Column = "N/A",
                        Message = $"Invalid JSON: {ex.Message}",
                        Severity = ErrorSeverity.Critical
                    }
                ]
            };
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var options = this.configuration.GetSerializerOptions();

        List<Dictionary<string, JsonElement>> jsonData = null;
        Result<TTarget>? deserializationError = null;

        try
        {
            jsonData = await JsonSerializer.DeserializeAsync<List<Dictionary<string, JsonElement>>>(
                inputStream, options, cancellationToken);
        }
        catch (JsonException ex)
        {
            deserializationError = Result<TTarget>.Failure()
                .WithError(new ImportError($"Invalid JSON: {ex.Message}"));
        }

        if (deserializationError.HasValue)
        {
            yield return deserializationError.Value;
            yield break;
        }

        if (jsonData is null)
        {
            yield break;
        }

        var rowNumber = 0;
        foreach (var row in jsonData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var errors = new List<ImportRowError>();
            TTarget item = null;
            Result<TTarget>? rowError = null;

            try
            {
                item = this.MapRow<TTarget>(row, importConfiguration, rowNumber, errors);
            }
            catch (Exception ex)
            {
                rowError = Result<TTarget>.Failure()
                    .WithError(new ImportError($"Row {rowNumber}: {ex.Message}", ex));
            }

            if (rowError.HasValue)
            {
                yield return rowError.Value;
                continue;
            }

            if (item is not null)
            {
                yield return Result<TTarget>.Success(item);
            }
            else if (errors.Count > 0)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportValidationError(
                        errors[0].RowNumber,
                        errors[0].Column,
                        errors[0].Message));
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
        var options = this.configuration.GetSerializerOptions();
        var errors = new List<ImportRowError>();

        try
        {
            var jsonData = await JsonSerializer.DeserializeAsync<List<Dictionary<string, JsonElement>>>(
                inputStream, options, cancellationToken);

            if (jsonData is null)
            {
                return ValidationResult.Success(0);
            }

            var validRows = 0;
            var rowNumber = 0;

            foreach (var row in jsonData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                var rowErrors = new List<ImportRowError>();

                foreach (var column in importConfiguration.Columns)
                {
                    if (column.Ignore)
                    {
                        continue;
                    }

                    var key = column.SourceName ?? column.PropertyName;
                    var hasValue = row.TryGetValue(key, out var jsonValue);
                    var rawValue = hasValue ? jsonValue.ToString() : null;

                    // Validate required
                    if (column.IsRequired && (!hasValue || jsonValue.ValueKind == JsonValueKind.Null))
                    {
                        rowErrors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = key,
                            Message = column.RequiredMessage ?? $"{column.PropertyName} is required",
                            RawValue = rawValue,
                            Severity = ErrorSeverity.Error
                        });
                    }

                    // Run custom validators
                    foreach (var validator in column.Validators)
                    {
                        if (!validator.Validate(rawValue))
                        {
                            rowErrors.Add(new ImportRowError
                            {
                                RowNumber = rowNumber,
                                Column = key,
                                Message = validator.ErrorMessage,
                                RawValue = rawValue,
                                Severity = ErrorSeverity.Error
                            });
                        }
                    }
                }

                if (rowErrors.Count == 0)
                {
                    validRows++;
                }
                else
                {
                    errors.AddRange(rowErrors);
                }
            }

            return errors.Count == 0
                ? ValidationResult.Success(jsonData.Count)
                : ValidationResult.Failure(jsonData.Count, validRows, errors);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure(0, 0, [new ImportRowError
            {
                RowNumber = 0,
                Column = "N/A",
                Message = $"Invalid JSON: {ex.Message}",
                Severity = ErrorSeverity.Critical
            }]);
        }
    }

    private TTarget MapRow<TTarget>(
        Dictionary<string, JsonElement> row,
        ImportConfiguration config,
        int rowNumber,
        List<ImportRowError> errors)
        where TTarget : class, new()
    {
        var item = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();

        var hasErrors = false;

        foreach (var column in config.Columns)
        {
            if (column.Ignore)
            {
                continue;
            }

            var key = column.SourceName ?? column.PropertyName;
            var hasValue = row.TryGetValue(key, out var jsonValue);
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
                column.SetValue(item, convertedValue);
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

        return hasErrors && config.ValidationBehavior == ImportValidationBehavior.SkipRow
            ? null
            : item;
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
}
