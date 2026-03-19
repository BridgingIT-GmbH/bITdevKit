// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts <see cref="decimal"/> values to and from formatted strings.
/// </summary>
public sealed class DecimalFormatConverter : IValueConverter<decimal>
{
    /// <summary>
    /// Gets the format string to use during export.
    /// </summary>
    public string Format { get; init; }

    /// <summary>
    /// Gets the culture to use for formatting and parsing.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets a value indicating whether invariant culture should be used.
    /// </summary>
    public bool UseInvariantCulture { get; init; }

    /// <summary>
    /// Gets the number styles to use during import.
    /// </summary>
    public NumberStyles NumberStyles { get; init; } = NumberStyles.Number;

    /// <inheritdoc/>
    public object ConvertToExport(decimal value, ValueConversionContext context)
    {
        var culture = this.ResolveCulture(context);

        return string.IsNullOrWhiteSpace(this.Format)
            ? value.ToString(culture)
            : value.ToString(this.Format, culture);
    }

    /// <inheritdoc/>
    public decimal ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is decimal decimalValue)
        {
            return decimalValue;
        }

        var culture = this.ResolveCulture(context);

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
        {
            return Convert.ToDecimal(value, culture);
        }

        if (value is float or double)
        {
            return Convert.ToDecimal(value, culture);
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        return decimal.TryParse(stringValue, this.NumberStyles, culture, out var result)
            ? result
            : default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is decimal decimalValue ? decimalValue : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private CultureInfo ResolveCulture(ValueConversionContext context)
    {
        return this.UseInvariantCulture ? CultureInfo.InvariantCulture : this.Culture ?? context.Culture;
    }
}
