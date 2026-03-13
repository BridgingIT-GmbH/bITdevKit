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
    public bool SupportsStreaming => false;

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var rows = this.BuildRows(data.Cast<object>().ToList(), exportConfiguration);
        var payloadColumns = rows
            .SelectMany(row => row.Payload.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        foreach (var column in baseColumnNames.Concat(payloadColumns))
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = rows.Count,
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
        var rows = new List<CsvTypedRow>();

        foreach (var (data, configuration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.AddRange(this.BuildRows(data.ToList(), configuration));
        }

        var payloadColumns = rows
            .SelectMany(row => row.Payload.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var writer = new StreamWriter(outputStream, this.configuration.Encoding, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(this.configuration.Culture)
        {
            Delimiter = this.configuration.Delimiter,
            HasHeaderRecord = true
        });

        foreach (var column in baseColumnNames.Concat(payloadColumns))
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();

        foreach (var row in rows)
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

        await csv.FlushAsync();
        await writer.FlushAsync(cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = rows.Count,
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
        }

        var errors = new List<ImportRowError>();
        var imported = this.Materialize<TTarget>(rows, importConfiguration, errors);

        return new ImportResult<TTarget>
        {
            Data = imported,
            TotalRows = rows.Count,
            SuccessfulRows = imported.Count,
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
            var item = importConfiguration.Factory is not null
                ? (TTarget)importConfiguration.Factory()
                : new TTarget();
            var rowLookup = group.ToDictionary(row => row.RecordId, StringComparer.OrdinalIgnoreCase);
            var rootRow = group.FirstOrDefault(row => string.Equals(row.RecordId, row.RootId, StringComparison.OrdinalIgnoreCase))
                ?? group.First();

            this.ApplyRootPayload(item, rootRow, rootColumns, importConfiguration, errors);
            this.ApplyChildren(item, group.ToList(), rowLookup, rootColumns, importConfiguration, errors);
            results.Add(item);
        }

        return results;
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
            if (column.Ignore)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType ?? typeof(string);
            if (column.Converter is null && propertyType.SupportsStructuredValue())
            {
                continue;
            }

            if (!row.Payload.TryGetValue(column.SourceName ?? column.PropertyName, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            try
            {
                var convertedValue = column.ConvertValue(rawValue);
                column.SetValue(target, convertedValue);
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = 0,
                    Column = column.SourceName ?? column.PropertyName,
                    Message = ex.Message,
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                });
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
                    var childItem = Activator.CreateInstance(elementType);
                    this.ApplyStructuredPayload(childItem, childRow, elementType, rowLookup, errors);
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
                this.ApplyStructuredPayload(childItem, childRow, propertyType, rowLookup, errors);
                column.SetValue(target, childItem);
            }
        }
    }

    private void ApplyStructuredPayload(
        object target,
        CsvTypedRow row,
        Type targetType,
        IReadOnlyDictionary<string, CsvTypedRow> rowLookup,
        ICollection<ImportRowError> errors)
    {
        foreach (var property in this.GetFlattenableProperties(targetType))
        {
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
                        var child = Activator.CreateInstance(elementType);
                        this.ApplyStructuredPayload(child, childRow, elementType, rowLookup, errors);
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
                    this.ApplyStructuredPayload(child, childRow, property.PropertyType, rowLookup, errors);
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
                property.SetValue(target, this.ConvertToType(rawValue, property.PropertyType));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = 0,
                    Column = property.Name,
                    Message = ex.Message,
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                });
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

    private object ConvertToType(string value, Type targetType)
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

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
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
