// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Converts string values by trimming and normalizing them.
/// </summary>
public sealed class StringTrimConverter : IValueConverter<string>
{
    /// <summary>
    /// Gets a value indicating whether leading whitespace should be trimmed.
    /// </summary>
    public bool TrimStart { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether trailing whitespace should be trimmed.
    /// </summary>
    public bool TrimEnd { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether line endings should be normalized to LF.
    /// </summary>
    public bool NormalizeLineEndings { get; init; }

    /// <summary>
    /// Gets a value indicating whether empty or whitespace-only values should return null.
    /// </summary>
    public bool ConvertEmptyToNull { get; init; }

    /// <inheritdoc/>
    public object ConvertToExport(string value, ValueConversionContext context)
    {
        return this.Normalize(value);
    }

    /// <inheritdoc/>
    public string ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.Normalize(value?.ToString());
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value?.ToString(), context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private string Normalize(string value)
    {
        if (value == null)
        {
            return this.ConvertEmptyToNull ? null : string.Empty;
        }

        var result = value;

        if (this.NormalizeLineEndings)
        {
            result = result.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        if (this.TrimStart && this.TrimEnd)
        {
            result = result.Trim();
        }
        else if (this.TrimStart)
        {
            result = result.TrimStart();
        }
        else if (this.TrimEnd)
        {
            result = result.TrimEnd();
        }

        if (this.ConvertEmptyToNull && string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return result;
    }
}
