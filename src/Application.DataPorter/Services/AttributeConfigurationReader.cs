// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Reads configuration from DataPorter attributes on types.
/// </summary>
public sealed class AttributeConfigurationReader
{
    private readonly ConcurrentDictionary<Type, ExportConfiguration> exportConfigCache = new();
    private readonly ConcurrentDictionary<Type, ImportConfiguration> importConfigCache = new();
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeConfigurationReader"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving converters.</param>
    public AttributeConfigurationReader(IServiceProvider serviceProvider = null)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Reads export configuration from attributes on the specified type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <returns>The export configuration.</returns>
    public ExportConfiguration ReadExportConfiguration<TSource>()
        where TSource : class
    {
        return this.ReadExportConfiguration(typeof(TSource));
    }

    /// <summary>
    /// Reads export configuration from attributes on the specified type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The export configuration.</returns>
    public ExportConfiguration ReadExportConfiguration(Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);

        return this.exportConfigCache.GetOrAdd(sourceType, type =>
        {
            var config = new ExportConfiguration
            {
                SourceType = type
            };

            // Read sheet attribute
            var sheetAttr = type.GetCustomAttribute<DataPorterSheetAttribute>();
            config.SheetName = sheetAttr?.Name ?? type.Name;

            // Read property attributes
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columnOrder = 0;

            foreach (var property in properties)
            {
                var ignoreAttr = property.GetCustomAttribute<DataPorterIgnoreAttribute>();
                if (ignoreAttr is not null && !ignoreAttr.ImportOnly)
                {
                    continue;
                }

                var columnAttr = property.GetCustomAttribute<DataPorterColumnAttribute>();
                if (columnAttr is not null && !columnAttr.Export)
                {
                    continue;
                }

                var converterAttr = property.GetCustomAttribute<DataPorterConverterAttribute>();
                IValueConverter converter = null;

                if (converterAttr is not null)
                {
                    converter = this.ResolveConverter(converterAttr.ConverterType);
                }

                var columnConfig = new ColumnConfiguration
                {
                    PropertyName = property.Name,
                    HeaderName = columnAttr?.Name ?? property.Name,
                    Order = columnAttr?.Order ?? columnOrder++,
                    Format = columnAttr?.Format,
                    Width = columnAttr?.Width ?? -1,
                    NullValue = columnAttr?.NullValue,
                    HorizontalAlignment = columnAttr?.HorizontalAlignment ?? HorizontalAlignment.Left,
                    VerticalAlignment = columnAttr?.VerticalAlignment ?? VerticalAlignment.Middle,
                    PropertyInfo = property,
                    Converter = converter
                };

                config.Columns.Add(columnConfig);
            }

            // Sort columns by order
            config.Columns = config.Columns.OrderBy(c => c.Order).ToList();

            return config;
        });
    }

    /// <summary>
    /// Reads import configuration from attributes on the specified type.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>The import configuration.</returns>
    public ImportConfiguration ReadImportConfiguration<TTarget>()
        where TTarget : class, new()
    {
        return this.ReadImportConfiguration(typeof(TTarget));
    }

    /// <summary>
    /// Reads import configuration from attributes on the specified type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <returns>The import configuration.</returns>
    public ImportConfiguration ReadImportConfiguration(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        return this.importConfigCache.GetOrAdd(targetType, type =>
        {
            var config = new ImportConfiguration
            {
                TargetType = type
            };

            // Read sheet attribute
            var sheetAttr = type.GetCustomAttribute<DataPorterSheetAttribute>();
            config.SheetName = sheetAttr?.Name;
            config.SheetIndex = sheetAttr?.Index ?? -1;

            // Read property attributes
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columnOrder = 0;

            foreach (var property in properties)
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                var ignoreAttr = property.GetCustomAttribute<DataPorterIgnoreAttribute>();
                if (ignoreAttr is not null && !ignoreAttr.ExportOnly)
                {
                    continue;
                }

                var columnAttr = property.GetCustomAttribute<DataPorterColumnAttribute>();
                if (columnAttr is not null && !columnAttr.Import)
                {
                    continue;
                }

                var converterAttr = property.GetCustomAttribute<DataPorterConverterAttribute>();
                IValueConverter converter = null;

                if (converterAttr is not null)
                {
                    converter = this.ResolveConverter(converterAttr.ConverterType);
                }

                var columnConfig = new ImportColumnConfiguration
                {
                    PropertyName = property.Name,
                    SourceName = columnAttr?.Name ?? property.Name,
                    Order = columnAttr?.Order ?? columnOrder++,
                    Format = columnAttr?.Format,
                    IsRequired = columnAttr?.Required ?? false,
                    RequiredMessage = columnAttr?.RequiredMessage,
                    PropertyInfo = property,
                    Converter = converter
                };

                // Read validation attributes
                var validationAttrs = property.GetCustomAttributes<DataPorterValidationAttribute>();
                foreach (var validationAttr in validationAttrs)
                {
                    var validator = this.CreateValidator(validationAttr, property);
                    if (validator is not null)
                    {
                        columnConfig.Validators.Add(validator);
                    }
                }

                config.Columns.Add(columnConfig);
            }

            return config;
        });
    }

    private IValueConverter ResolveConverter(Type converterType)
    {
        if (this.serviceProvider is not null)
        {
            var converter = this.serviceProvider.GetService(converterType) as IValueConverter;
            if (converter is not null)
            {
                return converter;
            }
        }

        return Activator.CreateInstance(converterType) as IValueConverter;
    }

    private ColumnValidator CreateValidator(DataPorterValidationAttribute attr, PropertyInfo property)
    {
        return attr.Type switch
        {
            ValidationType.Required => new ColumnValidator
            {
                Validate = value => value is not null && !string.IsNullOrWhiteSpace(value.ToString()),
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} is required."
            },
            ValidationType.MinLength when attr.Parameter is int minLength => new ColumnValidator
            {
                Validate = value => value?.ToString()?.Length >= minLength,
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} must be at least {minLength} characters."
            },
            ValidationType.MaxLength when attr.Parameter is int maxLength => new ColumnValidator
            {
                Validate = value => value?.ToString()?.Length <= maxLength,
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} must not exceed {maxLength} characters."
            },
            ValidationType.Range when attr.Parameter is string range => this.CreateRangeValidator(property.Name, range, attr.ErrorMessage),
            ValidationType.Regex when attr.Parameter is string pattern => new ColumnValidator
            {
                Validate = value => value is null || System.Text.RegularExpressions.Regex.IsMatch(value.ToString(), pattern),
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} format is invalid."
            },
            ValidationType.Email => new ColumnValidator
            {
                Validate = value => value is null || IsValidEmail(value.ToString()),
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} must be a valid email address."
            },
            ValidationType.Url => new ColumnValidator
            {
                Validate = value => value is null || Uri.TryCreate(value.ToString(), UriKind.Absolute, out _),
                ErrorMessage = attr.ErrorMessage ?? $"{property.Name} must be a valid URL."
            },
            _ => null
        };
    }

    private ColumnValidator CreateRangeValidator(string propertyName, string range, string errorMessage)
    {
        var parts = range.Split(',');
        if (parts.Length != 2)
        {
            return null;
        }

        if (!decimal.TryParse(parts[0], out var min) || !decimal.TryParse(parts[1], out var max))
        {
            return null;
        }

        return new ColumnValidator
        {
            Validate = value =>
            {
                if (value is null)
                {
                    return true;
                }

                if (!decimal.TryParse(value.ToString(), out var numValue))
                {
                    return false;
                }

                return numValue >= min && numValue <= max;
            },
            ErrorMessage = errorMessage ?? $"{propertyName} must be between {min} and {max}."
        };
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
