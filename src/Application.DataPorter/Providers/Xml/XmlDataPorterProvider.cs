// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Collections;
using System.Globalization;
using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

/// <summary>
/// XML data porter provider using System.Xml.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="XmlDataPorterProvider"/> class.
/// </remarks>
/// <param name="configuration">The XML configuration.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class XmlDataPorterProvider(
    XmlConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider
{
    private readonly XmlConfiguration configuration = configuration ?? new XmlConfiguration();
    private readonly ILogger<XmlDataPorterProvider> logger = loggerFactory?.CreateLogger<XmlDataPorterProvider>() ?? NullLogger<XmlDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.Xml;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".xml"];

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
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;
        var writeStream = new WriteStreamWrapper(outputStream);

        var rootName = this.configuration.RootElementName;
        var itemName = this.configuration.ItemElementName;

        await using var writer = XmlWriter.Create(writeStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, rootName, null);

        var totalRows = 0;

        foreach (var item in data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await this.WriteExportItemAsync(writer, item, itemName, exportConfiguration, cancellationToken);
            totalRows++;
            exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten);
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

        return new ExportResult
        {
            BytesWritten = writeStream.BytesWritten,
            TotalRows = totalRows,
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
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;
        var writeStream = new WriteStreamWrapper(outputStream);

        var rootName = this.configuration.RootElementName;
        var itemName = this.configuration.ItemElementName;

        await using var writer = XmlWriter.Create(writeStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, rootName, null);

        var totalRows = 0;

        await foreach (var item in data.WithCancellation(cancellationToken))
        {
            await this.WriteExportItemAsync(writer, item, itemName, exportConfiguration, cancellationToken);
            totalRows++;
            exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten);
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

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
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;

        var totalRows = 0;
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = XmlWriter.Create(writeStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "DataSets", null);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = this.SanitizeElementName(exportConfiguration.SheetName ?? $"Sheet{totalRows + 1}");

            await writer.WriteStartElementAsync(null, sheetName, null);

            foreach (var item in data)
            {
                await this.WriteExportItemAsync(writer, item, this.configuration.ItemElementName, exportConfiguration, cancellationToken);
                totalRows++;
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}");
            }

            await writer.WriteEndElementAsync();
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

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
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;

        var totalRows = 0;
        var writeStream = new WriteStreamWrapper(outputStream);

        await using var writer = XmlWriter.Create(writeStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "DataSets", null);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = this.SanitizeElementName(exportConfiguration.SheetName ?? $"Sheet{totalRows + 1}");

            await writer.WriteStartElementAsync(null, sheetName, null);

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                await this.WriteExportItemAsync(writer, item, this.configuration.ItemElementName, exportConfiguration, cancellationToken);
                totalRows++;
                exportConfiguration.ProgressTracker?.ReportProgress(totalRows, writeStream.BytesWritten, message: $"Exported {totalRows} rows from {sheetName}");
            }

            await writer.WriteEndElementAsync();
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

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

        await foreach (var result in this.ProcessElementsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
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
                results.Add(result.Item);
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

        await foreach (var result in this.ProcessElementsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
        {
            if (result.FatalError is not null)
            {
                totalRows += result.RowNumber > 0 ? 1 : 0;
                failedRows++;
                errorCount++;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount);
                yield return Result<TTarget>.Failure()
                    .WithError(result.FatalError);
                yield break;
            }

            totalRows++;
            if (result.Item is not null)
            {
                successfulRows++;
                importConfiguration.ProgressTracker?.ReportProgress(totalRows, successfulRows, failedRows, errorCount);
                yield return Result<TTarget>.Success(result.Item);
            }
            else if (result.Errors.Count > 0)
            {
                failedRows++;
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

        await foreach (var result in this.ProcessElementsAsync<TTarget>(inputStream, importConfiguration, cancellationToken))
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

    private async IAsyncEnumerable<XmlImportRowResult<TTarget>> ProcessElementsAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var settings = this.configuration.GetReaderSettings();
        settings.Async = true;
        var rowNumber = 0;
        var fatalResult = default(XmlImportRowResult<TTarget>);

        using var reader = XmlReader.Create(inputStream, settings);
        XmlNodeType rootNodeType;

        try
        {
            rootNodeType = await reader.MoveToContentAsync();
        }
        catch (XmlException ex)
        {
            fatalResult = new XmlImportRowResult<TTarget>(0, null, [], new ImportError($"Invalid XML: {ex.Message}", ex));
            rootNodeType = XmlNodeType.None;
        }

        if (fatalResult is not null)
        {
            yield return fatalResult;
            yield break;
        }

        if (rootNodeType != XmlNodeType.Element)
        {
            yield break;
        }

        if (reader.IsEmptyElement)
        {
            yield break;
        }

        var rootDepth = reader.Depth;
        await reader.ReadAsync();

        while (!reader.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element && reader.Depth == rootDepth + 1)
            {
                rowNumber++;

                XElement element = null;
                try
                {
                    element = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                }
                catch (XmlException ex)
                {
                    fatalResult = new XmlImportRowResult<TTarget>(
                        rowNumber,
                        null,
                        [],
                        new ImportError($"Invalid XML: {ex.Message}", ex));
                    break;
                }

                var rowErrors = new List<ImportRowError>();
                TTarget item = null;
                ImportError fatalError = null;

                try
                {
                    item = this.MapElement<TTarget>(element, importConfiguration, rowNumber, rowErrors);
                }
                catch (Exception ex)
                {
                    fatalError = new ImportError($"Row {rowNumber}: {ex.Message}", ex);
                }

                var result = new XmlImportRowResult<TTarget>(rowNumber, item, rowErrors, fatalError);
                yield return result;

                if (result.FatalError is not null ||
                    (result.Errors.Count > 0 && importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport))
                {
                    yield break;
                }

                continue;
            }

            if (!await reader.ReadAsync())
            {
                break;
            }
        }

        if (fatalResult is not null)
        {
            yield return fatalResult;
        }
    }

    private async Task WriteExportItemAsync(
        XmlWriter writer,
        object item,
        string itemElementName,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteStartElementAsync(null, itemElementName, null);

        foreach (var column in exportConfiguration.Columns)
        {
            await this.WriteExportColumnAsync(writer, item, column, exportConfiguration, cancellationToken);
        }

        await writer.WriteEndElementAsync();
    }

    private async Task WriteExportColumnAsync(
        XmlWriter writer,
        object item,
        ColumnConfiguration column,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = column.GetValue(item);
        var propertyType = column.PropertyInfo?.PropertyType ?? value?.GetType() ?? typeof(object);

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

        var elementName = this.SanitizeElementName(column.HeaderName ?? column.PropertyName);

        if (column.Converter is null && propertyType.SupportsStructuredValue())
        {
            await this.WriteStructuredElementAsync(writer, elementName, value, propertyType, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return;
        }

        if (this.configuration.UseAttributes && this.IsSimpleType(value))
        {
            await writer.WriteAttributeStringAsync(null, elementName, null, this.FormatValue(value));
            return;
        }

        await writer.WriteStartElementAsync(null, elementName, null);
        await writer.WriteStringAsync(this.FormatValue(value));
        await writer.WriteEndElementAsync();
    }

    private TTarget MapElement<TTarget>(
        XElement element,
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
            var childElement = element.Element(key);
            var attributeValue = element.Attribute(key)?.Value;
            var hasValue = childElement is not null || attributeValue is not null;
            var rawValue = childElement?.ToString(SaveOptions.DisableFormatting) ?? attributeValue;
            var targetType = column.PropertyInfo?.PropertyType ?? typeof(string);

            try
            {
                // Validate required
                if (column.IsRequired && !hasValue)
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

                if (!hasValue)
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
                object convertedValue;

                if (column.Converter is null && childElement is not null && targetType.SupportsStructuredValue())
                {
                    convertedValue = this.DeserializeStructuredValue(childElement, targetType, config.Culture);
                }
                else
                {
                    convertedValue = column.ConvertValue(childElement?.Value ?? attributeValue, config.Culture);
                }

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

    private string SanitizeElementName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Element";
        }

        // Remove invalid XML characters and replace spaces
        var sanitized = new string([.. name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')]);

        // Ensure starts with letter or underscore
        if (sanitized.Length == 0 || (!char.IsLetter(sanitized[0]) && sanitized[0] != '_'))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    private string FormatValue(object value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString(this.configuration.DateFormat),
            DateTimeOffset dto => dto.ToString(this.configuration.DateFormat),
            bool b => b.ToString().ToLowerInvariant(),
            _ => value.ToString() ?? string.Empty
        };
    }

    private async Task WriteStructuredElementAsync(XmlWriter writer, string elementName, object value, Type propertyType, HashSet<object> visited)
    {
        if (value is null)
        {
            await writer.WriteStartElementAsync(null, elementName, null);
            await writer.WriteEndElementAsync();
            return;
        }

        var serializedElement = this.SerializeStructuredValue(value, propertyType, elementName, visited);
        await writer.WriteRawAsync(serializedElement.ToString(SaveOptions.DisableFormatting));
    }

    private XElement SerializeStructuredValue(object value, Type propertyType, string elementName, HashSet<object> visited)
    {
        return this.SerializeStructuredObject(value, value?.GetType() ?? propertyType, elementName, visited);
    }

    private XElement SerializeStructuredObject(object value, Type propertyType, string elementName, HashSet<object> visited)
    {
        if (value is null)
        {
            return new XElement(elementName);
        }

        if (!propertyType.IsValueType && !visited.Add(value))
        {
            return new XElement(elementName);
        }

        try
        {
            if (propertyType.IsCollectionType())
            {
                var element = new XElement(elementName);
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    if (!item.GetType().IsValueType && visited.Contains(item))
                    {
                        continue;
                    }

                    element.Add(this.SerializeStructuredObject(item, item.GetType(), "Item", visited));
                }

                return element;
            }

            var elementResult = new XElement(elementName);
            foreach (var property in propertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            {
                var propertyValue = property.GetValue(value);
                var propertyElementName = this.SanitizeElementName(property.Name);

                if (propertyValue is null)
                {
                    continue;
                }

                if (!property.PropertyType.IsValueType && visited.Contains(propertyValue))
                {
                    continue;
                }

                if (property.PropertyType.SupportsStructuredValue())
                {
                    elementResult.Add(this.SerializeStructuredObject(propertyValue, property.PropertyType, propertyElementName, visited));
                }
                else
                {
                    elementResult.Add(new XElement(propertyElementName, this.FormatValue(propertyValue)));
                }
            }

            return elementResult;
        }
        finally
        {
            if (!propertyType.IsValueType)
            {
                visited.Remove(value);
            }
        }
    }

    private object DeserializeStructuredValue(XElement element, Type targetType, CultureInfo culture)
    {
        if (!element.HasElements && !element.HasAttributes && string.IsNullOrWhiteSpace(element.Value))
        {
            return null;
        }

        if (targetType.IsCollectionType())
        {
            var collection = this.CreateCollectionInstance(targetType);
            var elementType = this.GetCollectionElementType(targetType) ?? typeof(string);

            foreach (var child in element.Elements())
            {
                var item = elementType.SupportsStructuredValue()
                    ? this.DeserializeStructuredValue(child, elementType, culture)
                    : this.ConvertToType(child.Value, elementType, culture);

                this.AddCollectionItem(collection, item);
            }

            return collection;
        }

        if (!targetType.SupportsStructuredValue())
        {
            return this.ConvertToType(element.Value, targetType, culture);
        }

        var instance = Activator.CreateInstance(targetType);
        foreach (var property in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            var child = element.Element(this.SanitizeElementName(property.Name));
            if (child is null)
            {
                continue;
            }

            var value = property.PropertyType.SupportsStructuredValue()
                ? this.DeserializeStructuredValue(child, property.PropertyType, culture)
                : this.ConvertToType(child.Value, property.PropertyType, culture);

            property.SetValue(instance, value);
        }

        return instance;
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

    private bool IsSimpleType(object value)
    {
        if (value is null)
        {
            return true;
        }

        return value.GetType().IsSimpleType();
    }

    private sealed record XmlImportRowResult<TTarget>(
        int RowNumber,
        TTarget Item,
        IReadOnlyList<ImportRowError> Errors,
        ImportError FatalError)
        where TTarget : class;
}
