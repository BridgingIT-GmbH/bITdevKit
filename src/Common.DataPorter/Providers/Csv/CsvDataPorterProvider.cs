// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// CSV data porter provider using CsvHelper.
/// </summary>
public sealed class CsvDataPorterProvider : IDataExportProvider, IDataImportProvider
{
    private readonly CsvConfiguration configuration;
    private readonly ILogger<CsvDataPorterProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvDataPorterProvider"/> class.
    /// </summary>
    /// <param name="configuration">The CSV configuration.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public CsvDataPorterProvider(
        CsvConfiguration configuration = null,
        ILoggerFactory loggerFactory = null)
    {
        this.configuration = configuration ?? new CsvConfiguration();
        this.logger = loggerFactory?.CreateLogger<CsvDataPorterProvider>() ?? NullLogger<CsvDataPorterProvider>.Instance;
    }

    /// <inheritdoc/>
    public string Format => "csv";

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
        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = exportConfiguration.IncludeHeaders
        });

        // Write headers
        if (exportConfiguration.IncludeHeaders)
        {
            foreach (var column in exportConfiguration.Columns)
            {
                csv.WriteField(column.HeaderName);
            }

            await csv.NextRecordAsync();
        }

        var rowsExported = 0;

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

                var formattedValue = this.FormatValue(value, column, exportConfiguration);
                csv.WriteField(formattedValue);
            }

            await csv.NextRecordAsync();
            rowsExported++;
        }

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = rowsExported,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportMultipleAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        // CSV doesn't support multiple sheets, so we concatenate with separators
        var totalRows = 0;

        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                foreach (var column in exportConfiguration.Columns)
                {
                    csv.WriteField(column.HeaderName);
                }

                await csv.NextRecordAsync();
            }

            foreach (var item in data)
            {
                foreach (var column in exportConfiguration.Columns)
                {
                    var value = column.GetValue(item);
                    var formattedValue = this.FormatValue(value, column, exportConfiguration);
                    csv.WriteField(formattedValue);
                }

                await csv.NextRecordAsync();
                totalRows++;
            }

            await csv.FlushAsync();
            await writer.WriteLineAsync();
        }

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
        var results = new List<TTarget>();
        var errors = new List<ImportRowError>();

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

        var columnMap = this.BuildColumnMap(csv, importConfiguration);
        var rowNumber = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows;

        // Skip initial rows
        for (var i = 0; i < importConfiguration.SkipRows; i++)
        {
            if (!await csv.ReadAsync())
            {
                break;
            }
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            try
            {
                var item = this.MapRow<TTarget>(csv, columnMap, importConfiguration, rowNumber, errors);
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
            TotalRows = rowNumber - importConfiguration.HeaderRowIndex - importConfiguration.SkipRows,
            SuccessfulRows = results.Count,
            FailedRows = errors.Count,
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
        using var reader = new StreamReader(inputStream, this.configuration.Encoding);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var columnMap = this.BuildColumnMap(csv, importConfiguration);
        var rowNumber = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows;

        // Skip initial rows
        for (var i = 0; i < importConfiguration.SkipRows; i++)
        {
            if (!await csv.ReadAsync())
            {
                yield break;
            }
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var errors = new List<ImportRowError>();

            Result<TTarget>? result = null;
            try
            {
                var item = this.MapRow<TTarget>(csv, columnMap, importConfiguration, rowNumber, errors);

                if (item is not null)
                {
                    result = Result<TTarget>.Success(item);
                }
                else if (errors.Count > 0)
                {
                    result = Result<TTarget>.Failure()
                        .WithError(new ImportValidationError(
                            errors.First().RowNumber,
                            errors.First().Column,
                            errors.First().Message));
                }
            }
            catch (Exception ex)
            {
                result = Result<TTarget>.Failure()
                    .WithError(new ImportError($"Row {rowNumber}: {ex.Message}", ex));
            }

            if (result.HasValue)
            {
                yield return result.Value;
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

        using var reader = new StreamReader(inputStream, this.configuration.Encoding);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var columnMap = this.BuildColumnMap(csv, importConfiguration);
        var rowNumber = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows;
        var validRows = 0;

        // Skip initial rows
        for (var i = 0; i < importConfiguration.SkipRows; i++)
        {
            if (!await csv.ReadAsync())
            {
                break;
            }
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var rowErrors = new List<ImportRowError>();

            foreach (var (columnConfig, headerName) in columnMap)
            {
                var rawValue = csv.GetField(headerName);

                // Validate required
                if (columnConfig.IsRequired && string.IsNullOrWhiteSpace(rawValue))
                {
                    rowErrors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = columnConfig.SourceName,
                        Message = columnConfig.RequiredMessage ?? $"{columnConfig.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });
                }

                // Run custom validators
                foreach (var validator in columnConfig.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        rowErrors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = columnConfig.SourceName,
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

        var totalRows = rowNumber - importConfiguration.HeaderRowIndex - importConfiguration.SkipRows;

        return errors.Count == 0
            ? ValidationResult.Success(totalRows)
            : ValidationResult.Failure(totalRows, validRows, errors);
    }

    private Dictionary<ImportColumnConfiguration, string> BuildColumnMap(
        CsvReader csv,
        ImportConfiguration config)
    {
        var map = new Dictionary<ImportColumnConfiguration, string>();
        var headers = csv.HeaderRecord ?? [];

        foreach (var column in config.Columns)
        {
            if (column.Ignore)
            {
                continue;
            }

            if (column.SourceIndex >= 0 && column.SourceIndex < headers.Length)
            {
                map[column] = headers[column.SourceIndex];
                continue;
            }

            // Find column by header name
            var matchingHeader = headers.FirstOrDefault(
                h => h.Equals(column.SourceName, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(column.PropertyName, StringComparison.OrdinalIgnoreCase));

            if (matchingHeader is not null)
            {
                map[column] = matchingHeader;
            }
        }

        return map;
    }

    private TTarget MapRow<TTarget>(
        CsvReader csv,
        Dictionary<ImportColumnConfiguration, string> columnMap,
        ImportConfiguration config,
        int rowNumber,
        List<ImportRowError> errors)
        where TTarget : class, new()
    {
        var item = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();

        var hasErrors = false;

        foreach (var (columnConfig, headerName) in columnMap)
        {
            var rawValue = csv.GetField(headerName);

            if (this.configuration.TrimFields && rawValue is not null)
            {
                rawValue = rawValue.Trim();
            }

            try
            {
                // Validate required
                if (columnConfig.IsRequired && string.IsNullOrWhiteSpace(rawValue))
                {
                    hasErrors = true;
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = columnConfig.SourceName,
                        Message = columnConfig.RequiredMessage ?? $"{columnConfig.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });

                    continue;
                }

                // Run custom validators
                foreach (var validator in columnConfig.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        hasErrors = true;
                        errors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = columnConfig.SourceName,
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
                var convertedValue = columnConfig.ConvertValue(rawValue);
                columnConfig.SetValue(item, convertedValue);
            }
            catch (Exception ex)
            {
                hasErrors = true;
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Column = columnConfig.SourceName,
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
}
