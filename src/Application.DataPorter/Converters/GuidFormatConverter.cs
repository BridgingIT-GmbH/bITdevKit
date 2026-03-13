// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts <see cref="Guid"/> values to and from formatted strings.
/// </summary>
public sealed class GuidFormatConverter : IValueConverter<Guid>
{
    /// <summary>
    /// Gets the format string to use during export.
    /// </summary>
    public string Format { get; init; } = "D";

    /// <summary>
    /// Gets a value indicating whether exported values should be upper case.
    /// </summary>
    public bool UseUpperCase { get; init; }

    /// <inheritdoc/>
    public object ConvertToExport(Guid value, ValueConversionContext context)
    {
        var format = string.IsNullOrWhiteSpace(this.Format) ? "D" : this.Format;
        var result = value.ToString(format, CultureInfo.InvariantCulture);

        return this.UseUpperCase ? result.ToUpperInvariant() : result;
    }

    /// <inheritdoc/>
    public Guid ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is Guid guidValue)
        {
            return guidValue;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        if (!string.IsNullOrWhiteSpace(this.Format) && Guid.TryParseExact(stringValue, this.Format, out var exactResult))
        {
            return exactResult;
        }

        return Guid.TryParse(stringValue, out var result) ? result : default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is Guid guidValue ? guidValue : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }
}
