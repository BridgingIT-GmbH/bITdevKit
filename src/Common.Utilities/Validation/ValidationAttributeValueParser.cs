// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.ComponentModel;
using System.Globalization;

/// <summary>
/// Converts string-based validation-attribute values into strongly typed values for generated FluentValidation rules.
/// </summary>
public static class ValidationAttributeValueParser
{
    /// <summary>
    /// Parses the specified invariant string value into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type to parse.</typeparam>
    /// <param name="value">The invariant string value.</param>
    /// <returns>The parsed value.</returns>
    public static T Parse<T>(string value)
    {
        return (T)Parse(typeof(T), value);
    }

    /// <summary>
    /// Parses the specified invariant string value into the provided target type.
    /// </summary>
    /// <param name="targetType">The target type to parse.</param>
    /// <param name="value">The invariant string value.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target type cannot be converted from an invariant string.</exception>
    public static object Parse(Type targetType, string value)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (actualType == typeof(string))
        {
            return value;
        }

        if (actualType.IsEnum)
        {
            return Enum.Parse(actualType, value, ignoreCase: false);
        }

        if (actualType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (actualType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        }

        if (actualType == typeof(DateTime))
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture);
        }

        if (actualType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        var converter = TypeDescriptor.GetConverter(actualType);
        if (converter?.CanConvertFrom(typeof(string)) == true)
        {
            return converter.ConvertFromInvariantString(value);
        }

        throw new InvalidOperationException($"Type '{actualType.FullName}' cannot be converted from an invariant string value.");
    }
}
