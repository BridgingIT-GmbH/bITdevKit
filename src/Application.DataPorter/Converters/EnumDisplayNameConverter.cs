// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.ComponentModel.DataAnnotations;
using System.Reflection;

/// <summary>
/// Converts enum values to/from their display names.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public sealed class EnumDisplayNameConverter<TEnum> : IValueConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public object ConvertToExport(TEnum value, ValueConversionContext context)
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var displayAttr = member?.GetCustomAttribute<DisplayAttribute>();

        return displayAttr?.Name ?? value.ToString();
    }

    /// <inheritdoc/>
    public TEnum ConvertFromImport(object value, ValueConversionContext context)
    {
        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return default;
        }

        // Try parse by enum name first
        if (Enum.TryParse<TEnum>(stringValue, ignoreCase: true, out var result))
        {
            return result;
        }

        // Try find by display name
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            var member = typeof(TEnum).GetMember(enumValue.ToString()).FirstOrDefault();
            var displayAttr = member?.GetCustomAttribute<DisplayAttribute>();

            if (displayAttr?.Name?.Equals(stringValue, StringComparison.OrdinalIgnoreCase) == true)
            {
                return enumValue;
            }
        }

        return default;
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value is TEnum e ? e : default, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }
}
