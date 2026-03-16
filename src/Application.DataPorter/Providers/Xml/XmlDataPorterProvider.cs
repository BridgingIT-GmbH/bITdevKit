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
        var dataList = data.ToList();
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;

        var rootName = this.configuration.RootElementName;
        var itemName = this.configuration.ItemElementName;

        await using var writer = XmlWriter.Create(outputStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, rootName, null);

        foreach (var item in dataList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await this.WriteExportItemAsync(writer, item, itemName, exportConfiguration, cancellationToken);
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

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
        var settings = this.configuration.GetWriterSettings();
        settings.Async = true;

        var totalRows = 0;

        await using var writer = XmlWriter.Create(outputStream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "DataSets", null);

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = this.SanitizeElementName(exportConfiguration.SheetName ?? $"Sheet{totalRows + 1}");
            var dataList = data.ToList();

            await writer.WriteStartElementAsync(null, sheetName, null);

            foreach (var item in dataList)
            {
                await this.WriteExportItemAsync(writer, item, this.configuration.ItemElementName, exportConfiguration, cancellationToken);
            }

            await writer.WriteEndElementAsync();
            totalRows += dataList.Count;
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();

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

        try
        {
            var document = await XDocument.LoadAsync(inputStream, LoadOptions.None, cancellationToken);
            var items = document.Root?.Elements().ToList() ?? [];

            var rowNumber = 0;
            foreach (var element in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                try
                {
                    var item = this.MapElement<TTarget>(element, importConfiguration, rowNumber, errors);
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
                TotalRows = items.Count,
                SuccessfulRows = results.Count,
                FailedRows = items.Count - results.Count,
                Duration = TimeSpan.Zero,
                Errors = errors
            };
        }
        catch (XmlException ex)
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
                        Message = $"Invalid XML: {ex.Message}",
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
        XDocument document;
        Result<TTarget>? loadError = null;
        try
        {
            // Load and validate XML document outside iterator block
            document = await XDocument.LoadAsync(inputStream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        }
        catch (XmlException ex)
        {
            loadError = Result<TTarget>.Failure()
                .WithError(new ImportError($"Invalid XML: {ex.Message}", ex));
            document = null;
        }

        if (loadError.HasValue)
        {
            yield return loadError.Value;
            yield break;
        }

        if (document.Root is null)
        {
            yield return Result<TTarget>.Failure()
                .WithError(new ImportError("Invalid XML: Root element not found"));
            yield break;
        }

        var items = document.Root.Elements().ToList();

        var rowNumber = 0;
        foreach (var element in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var errors = new List<ImportRowError>();

            var item = this.MapElement<TTarget>(element, importConfiguration, rowNumber, errors);

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
        var errors = new List<ImportRowError>();

        try
        {
            var document = await XDocument.LoadAsync(inputStream, LoadOptions.None, cancellationToken);
            var items = document.Root?.Elements().ToList() ?? [];

            var validRows = 0;
            var rowNumber = 0;

            foreach (var element in items)
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
                    var childElement = element.Element(key);
                    var attributeValue = element.Attribute(key)?.Value;
                    var hasValue = childElement is not null || attributeValue is not null;
                    var rawValue = childElement?.ToString(SaveOptions.DisableFormatting) ?? attributeValue;
                    var targetType = column.PropertyInfo?.PropertyType ?? typeof(string);

                    // Validate required
                    if (column.IsRequired && !hasValue)
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
                ? ValidationResult.Success(items.Count)
                : ValidationResult.Failure(items.Count, validRows, errors);
        }
        catch (XmlException ex)
        {
            return ValidationResult.Failure(0, 0, [new ImportRowError
            {
                RowNumber = 0,
                Column = "N/A",
                Message = $"Invalid XML: {ex.Message}",
                Severity = ErrorSeverity.Critical
            }]);
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
                    convertedValue = this.DeserializeStructuredValue(childElement, targetType);
                }
                else
                {
                    convertedValue = column.ConvertValue(childElement?.Value ?? attributeValue);
                }

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

    private object DeserializeStructuredValue(XElement element, Type targetType)
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
                    ? this.DeserializeStructuredValue(child, elementType)
                    : this.ConvertToType(child.Value, elementType);

                this.AddCollectionItem(collection, item);
            }

            return collection;
        }

        if (!targetType.SupportsStructuredValue())
        {
            return this.ConvertToType(element.Value, targetType);
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
                ? this.DeserializeStructuredValue(child, property.PropertyType)
                : this.ConvertToType(child.Value, property.PropertyType);

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

    private bool IsSimpleType(object value)
    {
        if (value is null)
        {
            return true;
        }

        return value.GetType().IsSimpleType();
    }
}
