// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts <see cref="DateTime"/> values to and from formatted strings.
/// </summary>
public sealed class DateTimeFormatConverter : IValueConverter<DateTime>
{
    private const string Iso8601Format = "O";

    /// <summary>
    /// Gets the format string to use.
    /// </summary>
    public string Format { get; init; }

    /// <summary>
    /// Gets the culture to use for formatting and parsing.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets a value indicating whether ISO 8601 round-trip formatting should be used.
    /// </summary>
    public bool UseIso8601 { get; init; } // https://en.wikipedia.org/wiki/ISO_8601

    /// <summary>
    /// Gets a value indicating whether values should be converted to UTC during export.
    /// </summary>
    public bool ConvertToUtcOnExport { get; init; }

    /// <summary>
    /// Gets a value indicating whether values should be converted to UTC during import.
    /// </summary>
    public bool ConvertToUtcOnImport { get; init; }

    /// <inheritdoc/>
    public object ConvertToExport(DateTime value, ValueConversionContext context)
    {
        var dateTime = this.ConvertToUtcOnExport ? value.ToUniversalTime() : value;
        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);

        return string.IsNullOrWhiteSpace(format)
            ? dateTime.ToString(culture)
            : dateTime.ToString(format, culture);
    }

    /// <inheritdoc/>
    public DateTime ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is DateTime dateTimeValue)
        {
            return this.NormalizeImportedValue(dateTimeValue);
        }

        if (value is DateTimeOffset dateTimeOffsetValue)
        {
            return this.ConvertToUtcOnImport ? dateTimeOffsetValue.UtcDateTime : dateTimeOffsetValue.DateTime;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);
        var styles = this.GetImportStyles();

        if (!string.IsNullOrWhiteSpace(format) && DateTime.TryParseExact(stringValue, format, culture, styles, out var exactResult))
        {
            return this.NormalizeImportedValue(exactResult);
        }

        if (this.UseIso8601 && DateTime.TryParseExact(stringValue, Iso8601Format, CultureInfo.InvariantCulture, styles, out var isoResult))
        {
            return this.NormalizeImportedValue(isoResult);
        }

        if (DateTime.TryParse(stringValue, culture, styles, out var parsedResult))
        {
            return this.NormalizeImportedValue(parsedResult);
        }

        return default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is DateTime dateTime ? dateTime : default, context);
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

    private DateTimeStyles GetImportStyles()
    {
        var styles = DateTimeStyles.AllowWhiteSpaces;

        if (this.ConvertToUtcOnImport)
        {
            styles |= DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        }

        return styles;
    }

    private DateTime NormalizeImportedValue(DateTime value)
    {
        if (!this.ConvertToUtcOnImport)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
