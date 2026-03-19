// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;

/// <summary>
/// Converts <see cref="DateTimeOffset"/> values to and from formatted strings.
/// </summary>
public sealed class DateTimeOffsetFormatConverter : IValueConverter<DateTimeOffset>
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
    public object ConvertToExport(DateTimeOffset value, ValueConversionContext context)
    {
        var dateTimeOffset = this.ConvertToUtcOnExport ? value.ToUniversalTime() : value;
        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);

        return string.IsNullOrWhiteSpace(format)
            ? dateTimeOffset.ToString(culture)
            : dateTimeOffset.ToString(format, culture);
    }

    /// <inheritdoc/>
    public DateTimeOffset ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is DateTimeOffset dateTimeOffsetValue)
        {
            return this.NormalizeImportedValue(dateTimeOffsetValue);
        }

        if (value is DateTime dateTimeValue)
        {
            var dateTimeOffset = dateTimeValue.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(dateTimeValue, TimeSpan.Zero)
                : new DateTimeOffset(dateTimeValue);

            return this.NormalizeImportedValue(dateTimeOffset);
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        var format = this.ResolveFormat(context);
        var culture = this.ResolveCulture(context);
        var styles = this.GetImportStyles();

        if (!string.IsNullOrWhiteSpace(format) && DateTimeOffset.TryParseExact(stringValue, format, culture, styles, out var exactResult))
        {
            return this.NormalizeImportedValue(exactResult);
        }

        if (this.UseIso8601 && DateTimeOffset.TryParseExact(stringValue, Iso8601Format, CultureInfo.InvariantCulture, styles, out var isoResult))
        {
            return this.NormalizeImportedValue(isoResult);
        }

        if (DateTimeOffset.TryParse(stringValue, culture, styles, out var parsedResult))
        {
            return this.NormalizeImportedValue(parsedResult);
        }

        return default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is DateTimeOffset dateTimeOffset ? dateTimeOffset : default, context);
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

    private DateTimeOffset NormalizeImportedValue(DateTimeOffset value)
    {
        return this.ConvertToUtcOnImport ? value.ToUniversalTime() : value;
    }
}
