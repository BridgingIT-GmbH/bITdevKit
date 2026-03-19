// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts values using configurable string mappings.
/// </summary>
/// <typeparam name="T">The target value type.</typeparam>
public sealed class StringMapConverter<T> : IValueConverter<T>
{
    /// <summary>
    /// Gets the mappings used during import.
    /// </summary>
    public IReadOnlyDictionary<string, T> ImportMappings { get; init; } = new Dictionary<string, T>();

    /// <summary>
    /// Gets the mappings used during export.
    /// </summary>
    public IReadOnlyDictionary<T, string> ExportMappings { get; init; } = new Dictionary<T, string>();

    /// <summary>
    /// Gets a value indicating whether import key matching ignores case.
    /// </summary>
    public bool IgnoreCase { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether incoming strings should be trimmed before matching.
    /// </summary>
    public bool TrimInput { get; init; } = true;

    /// <inheritdoc/>
    public object ConvertToExport(T value, ValueConversionContext context)
    {
        if (this.ExportMappings.TryGetValue(value, out var exportValue))
        {
            return exportValue;
        }

        foreach (var mapping in this.ImportMappings)
        {
            if (EqualityComparer<T>.Default.Equals(mapping.Value, value))
            {
                return mapping.Key;
            }
        }

        return value?.ToString();
    }

    /// <inheritdoc/>
    public T ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is T typedValue)
        {
            return typedValue;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        if (this.TrimInput)
        {
            stringValue = stringValue.Trim();
        }

        foreach (var mapping in this.ImportMappings)
        {
            if (string.Equals(mapping.Key, stringValue, this.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return mapping.Value;
            }
        }

        return this.TryConvertFallback(stringValue, out var result) ? result : default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is T typedValue ? typedValue : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private bool TryConvertFallback(string value, out T result)
    {
        if (typeof(T) == typeof(string))
        {
            result = (T)(object)value;
            return true;
        }

        if (typeof(T).IsEnum && Enum.TryParse(typeof(T), value, this.IgnoreCase, out var enumResult))
        {
            result = (T)enumResult;
            return true;
        }

        if (typeof(T) == typeof(Guid) && Guid.TryParse(value, out var guidResult))
        {
            result = (T)(object)guidResult;
            return true;
        }

        try
        {
            result = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
