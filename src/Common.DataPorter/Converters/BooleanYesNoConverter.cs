// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Converts boolean values to/from "Yes"/"No" strings.
/// </summary>
public sealed class BooleanYesNoConverter : IValueConverter<bool>
{
    /// <summary>
    /// Gets the "Yes" string value.
    /// </summary>
    public string YesValue { get; init; } = "Yes";

    /// <summary>
    /// Gets the "No" string value.
    /// </summary>
    public string NoValue { get; init; } = "No";

    /// <inheritdoc/>
    public object ConvertToExport(bool value, ValueConversionContext context)
    {
        return value ? this.YesValue : this.NoValue;
    }

    /// <inheritdoc/>
    public bool ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is bool boolValue)
        {
            return boolValue;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        return stringValue.Equals(this.YesValue, StringComparison.OrdinalIgnoreCase) ||
               stringValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               stringValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               stringValue.Equals("y", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is bool b ? b : false, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }
}
