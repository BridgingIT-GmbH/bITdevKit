// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts enum values to and from their names or numeric values.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public sealed class EnumValueConverter<TEnum> : IValueConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets a value indicating whether exported values should use the enum's numeric value.
    /// </summary>
    public bool ExportAsNumeric { get; init; }

    /// <summary>
    /// Gets a value indicating whether numeric values are allowed during import.
    /// </summary>
    public bool AllowNumericImport { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether import should ignore casing for enum names.
    /// </summary>
    public bool IgnoreCase { get; init; } = true;

    /// <inheritdoc/>
    public object ConvertToExport(TEnum value, ValueConversionContext context)
    {
        if (!this.ExportAsNumeric)
        {
            return value.ToString();
        }

        return Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TEnum)), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public TEnum ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is TEnum enumValue)
        {
            return enumValue;
        }

        if (this.AllowNumericImport && this.TryConvertNumericValue(value, out var numericResult))
        {
            return numericResult;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        if (!this.AllowNumericImport && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return default;
        }

        return Enum.TryParse<TEnum>(stringValue, this.IgnoreCase, out var result) ? result : default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is TEnum enumValue ? enumValue : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private bool TryConvertNumericValue(object value, out TEnum result)
    {
        try
        {
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                result = (TEnum)Enum.ToObject(typeof(TEnum), value);
                return true;
            }

            result = default;
            return false;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
