﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.ComponentModel;
using System.Reflection;

public static partial class Extensions
{
    public static T ToType<T>(this object value)
    {
        var targetType = typeof(T);
        if (value is null)
        {
            try
            {
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return default;
            }
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        var valueType = value.GetType();

        if (targetType.IsAssignableFrom(valueType))
        {
            return (T)value;
        }

        var targetTypeInfo = targetType.GetTypeInfo();
        if (targetTypeInfo.IsEnum && (value is string || valueType.GetTypeInfo().IsEnum))
        {
            // attempt to match enum by name.
            if (EnumExtensions.TryEnumIsDefined(targetType, value.ToString()))
            {
                var parsedValue = Enum.Parse(targetType, value.ToString() ?? string.Empty, false);

                return (T)parsedValue;
            }

            throw new ArgumentException(
                $"The Enum value of '{value}' is not defined as a valid value for '{targetType.FullName}'.");
        }

        if (targetTypeInfo.IsEnum && valueType.IsNumeric())
        {
            return (T)Enum.ToObject(targetType, value);
        }

        if (converter.CanConvertFrom(valueType))
        {
            var convertedValue = converter.ConvertFrom(value);

            return (T)convertedValue;
        }

        if (value is not IConvertible)
        {
            throw new ArgumentException(
                $"An incompatible value specified. Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}",
                nameof(value));
        }

        try
        {
            var convertedValue = Convert.ChangeType(value, targetType);

            return (T)convertedValue;
        }
        catch (Exception e)
        {
            throw new ArgumentException(
                $"An incompatible value specified. Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}",
                nameof(value),
                e);
        }
    }
}