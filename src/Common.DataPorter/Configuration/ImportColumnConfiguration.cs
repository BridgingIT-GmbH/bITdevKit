// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Reflection;

/// <summary>
/// Represents the configuration for a single column in import operations.
/// </summary>
public sealed class ImportColumnConfiguration : IColumnConfiguration
{
    /// <summary>
    /// Gets or sets the property name this column maps to.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets or sets the source column name/header to map from.
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the source column index (0-based). -1 means use SourceName.
    /// </summary>
    public int SourceIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the column header name (alias for SourceName).
    /// </summary>
    public string HeaderName
    {
        get => this.SourceName;
        set => this.SourceName = value;
    }

    /// <summary>
    /// Gets or sets the column order (0-based). -1 means use natural order.
    /// </summary>
    public int Order { get; set; } = -1;

    /// <summary>
    /// Gets or sets the format string for parsing the column value.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the column width. Not applicable for import.
    /// </summary>
    public double Width { get; set; } = -1;

    /// <summary>
    /// Gets or sets the value to use when the source value is null or empty.
    /// </summary>
    public string NullValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column should be ignored.
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the error message when the required validation fails.
    /// </summary>
    public string RequiredMessage { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment. Not applicable for import.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

    /// <summary>
    /// Gets or sets the vertical alignment. Not applicable for import.
    /// </summary>
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Middle;

    /// <summary>
    /// Gets or sets the value converter for this column.
    /// </summary>
    public IValueConverter Converter { get; set; }

    /// <summary>
    /// Gets or sets the property info for reflection-based value setting.
    /// </summary>
    internal PropertyInfo PropertyInfo { get; set; }

    /// <summary>
    /// Gets or sets the value setter function.
    /// </summary>
    internal Action<object, object> ValueSetter { get; set; }

    /// <summary>
    /// Gets or sets the validators for this column.
    /// </summary>
    public List<ColumnValidator> Validators { get; set; } = [];

    /// <summary>
    /// Gets or sets the custom parser function.
    /// </summary>
    public Func<string, object> Parser { get; set; }

    /// <summary>
    /// Sets the value on the target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="value">The value to set.</param>
    public void SetValue(object target, object value)
    {
        if (target is null)
        {
            return;
        }

        if (this.ValueSetter is not null)
        {
            this.ValueSetter(target, value);
            return;
        }

        this.PropertyInfo?.SetValue(target, value);
    }

    /// <summary>
    /// Converts the raw value to the target type.
    /// </summary>
    /// <param name="rawValue">The raw value from the import source.</param>
    /// <returns>The converted value.</returns>
    public object ConvertValue(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        if (this.Parser is not null)
        {
            return this.Parser(rawValue);
        }

        if (this.Converter is not null)
        {
            var context = new ValueConversionContext
            {
                PropertyName = this.PropertyName,
                PropertyType = this.PropertyInfo?.PropertyType ?? typeof(string),
                EntityType = this.PropertyInfo?.DeclaringType ?? typeof(object),
                Format = this.Format
            };

            return this.Converter.ConvertFromImport(rawValue, context);
        }

        // Default conversion
        var targetType = this.PropertyInfo?.PropertyType ?? typeof(string);
        return ConvertToType(rawValue, targetType);
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
            return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Represents a validator for a column.
/// </summary>
public sealed class ColumnValidator
{
    /// <summary>
    /// Gets or sets the validation function.
    /// </summary>
    public required Func<object, bool> Validate { get; init; }

    /// <summary>
    /// Gets or sets the error message when validation fails.
    /// </summary>
    public required string ErrorMessage { get; init; }
}
