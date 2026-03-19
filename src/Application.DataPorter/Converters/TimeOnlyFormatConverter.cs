// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts <see cref="TimeOnly"/> values to and from formatted strings.
/// </summary>
public sealed class TimeOnlyFormatConverter : IValueConverter<TimeOnly>
{
    private const string Iso8601Format = "HH:mm:ss.fffffff";

    /// <summary>
    /// Gets the format string to use.
    /// </summary>
    public string Format { get; init; }

    /// <summary>
    /// Gets the culture to use for formatting and parsing.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets a value indicating whether ISO 8601 formatting should be used.
    /// </summary>
    public bool UseIso8601 { get; init; } // https://en.wikipedia.org/wiki/ISO_8601

    /// <inheritdoc/>
    public object ConvertToExport(TimeOnly value, ValueConversionContext context)
    {
        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);

        return string.IsNullOrWhiteSpace(format)
            ? value.ToString(culture)
            : value.ToString(format, culture);
    }

    /// <inheritdoc/>
    public TimeOnly ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is TimeOnly timeOnlyValue)
        {
            return timeOnlyValue;
        }

        if (value is DateTime dateTimeValue)
        {
            return TimeOnly.FromDateTime(dateTimeValue);
        }

        if (value is DateTimeOffset dateTimeOffsetValue)
        {
            return TimeOnly.FromDateTime(dateTimeOffsetValue.DateTime);
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);
        var styles = DateTimeStyles.AllowWhiteSpaces;

        if (!string.IsNullOrWhiteSpace(format) && TimeOnly.TryParseExact(stringValue, format, culture, styles, out var exactResult))
        {
            return exactResult;
        }

        if (this.UseIso8601 && TimeOnly.TryParseExact(stringValue, Iso8601Format, CultureInfo.InvariantCulture, styles, out var isoResult))
        {
            return isoResult;
        }

        if (TimeOnly.TryParse(stringValue, culture, styles, out var parsedResult))
        {
            return parsedResult;
        }

        return default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is TimeOnly timeOnly ? timeOnly : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private string ResolveFormat(ValueConversionContext context)
    {
        return this.UseIso8601 ? Iso8601Format : this.Format ?? context.Format;
    }

    private CultureInfo ResolveCulture(ValueConversionContext context)
    {
        return this.UseIso8601 ? CultureInfo.InvariantCulture : this.Culture ?? context.Culture;
    }
}
