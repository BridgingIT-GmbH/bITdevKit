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

        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);
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
            }
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
        var totalRows = 0;

        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);

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
                }
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

        var plan = this.BuildImportPlan(importConfiguration, csv.HeaderRecord ?? []);
        var rowNumber = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows;
        var groupedResults = new Dictionary<string, TTarget>(StringComparer.OrdinalIgnoreCase);

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
                var rowValues = this.ReadRowValues(csv, plan.Columns);
                var item = this.GetOrCreateItem(rowValues, plan, importConfiguration, groupedResults, results);
                var rowErrors = new List<ImportRowError>();
                var success = this.MapRow(rowValues, item, plan, importConfiguration, rowNumber, rowErrors);

                if (!success && importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                {
                    errors.AddRange(rowErrors);
                    break;
                }

                errors.AddRange(rowErrors);
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
        var result = await this.ImportAsync<TTarget>(inputStream, importConfiguration, cancellationToken);

        if (result.Errors.Count > 0 && result.Data.Count == 0)
        {
            foreach (var error in result.Errors)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportValidationError(error.RowNumber, error.Column, error.Message));
            }

            yield break;
        }

        foreach (var item in result.Data)
        {
            yield return Result<TTarget>.Success(item);
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
                return header is null ? null : column with { HeaderName = header };
            })
            .Where(column => column is not null)
            .Cast<CsvImportColumn>()
            .ToList();

        if (mappedColumns.All(column => !column.IsCollection))
        {
            collectionProperty = null;
        }

        return new CsvImportPlan(mappedColumns, collectionProperty);
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

    private TTarget GetOrCreateItem<TTarget>(
        IReadOnlyDictionary<string, string> rowValues,
        CsvImportPlan plan,
        ImportConfiguration config,
        IDictionary<string, TTarget> groupedResults,
        ICollection<TTarget> results)
        where TTarget : class, new()
    {
        if (plan.CollectionProperty is null)
        {
            var item = config.Factory is not null
                ? (TTarget)config.Factory()
                : new TTarget();
            results.Add(item);
            return item;
        }

        var key = this.GetGroupingKey(rowValues, plan);
        if (groupedResults.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();
        groupedResults[key] = created;
        results.Add(created);
        return created;
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

                var convertedValue = this.ConvertImportValue(column, rawValue);
                if (column.IsCollection)
                {
                    if (collectionItem is null)
                    {
                        continue;
                    }

                    this.SetNestedValue(collectionItem, [.. column.PropertyPath.Skip(1)], convertedValue);
                }
                else
                {
                    this.SetNestedValue(item, column.PropertyPath, convertedValue);
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

        if (collectionItem is not null)
        {
            this.AddCollectionItem(item, plan.CollectionProperty, collectionItem);
        }

        return !(hasErrors && config.ValidationBehavior == ImportValidationBehavior.SkipRow);
    }

    private object ConvertImportValue(CsvImportColumn column, string rawValue)
    {
        if (column.SourceColumn.Converter is not null || column.SourceColumn.Parser is not null)
        {
            return column.SourceColumn.ConvertValue(rawValue);
        }

        return ConvertToType(rawValue, column.PropertyPath[^1].PropertyType);
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

    private static object ConvertToType(string value, Type targetType)
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
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private sealed record CsvExportPlan(IReadOnlyList<CsvExportColumn> Columns, PropertyInfo CollectionProperty);

    private sealed record CsvImportPlan(IReadOnlyList<CsvImportColumn> Columns, PropertyInfo CollectionProperty);

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
}
