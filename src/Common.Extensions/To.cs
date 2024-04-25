// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Primitives;

public static partial class Extensions
{
    /// <summary>
    /// Converts an object to a value type using <see cref="Convert.ChangeType(object,TypeCode)" />.</summary>
    /// <typeparam name="T">The target object type.</typeparam>
    /// <param name="source">The object to be converted.</param>
    /// <param name="throws">if set to <c>true</c> throws exceptions when conversion fails.</param>
    /// <param name="defaultValue">The default value to return when conversion fails.</param>
    /// <returns>
    /// Converted object.
    /// </returns>
    [DebuggerStepThrough]
    public static T To<T>(this object source, bool throws = false, T defaultValue = default, CultureInfo cultureInfo = null)
    {
        if (source is null)
        {
            return defaultValue;
        }

        var targetType = typeof(T);

        try
        {
            if (source.GetType() == typeof(StringValues))
            {
                return source.ToString().To<T>();
            }

            if (targetType == typeof(Guid))
            {
                return (T)TypeDescriptor.GetConverter(targetType).ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                return (T)Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
            }

            if (targetType.IsEnum && (source is string || source is int || source is decimal || source is double || source is float))
            {
                try
                {
                    return (T)Enum.Parse(targetType, source.ToString());
                }
                catch (ArgumentException)
                {
                    return default;
                }
            }

            return (T)source;
        }
        catch (FormatException) when (!throws)
        {
            return defaultValue;
        }
        catch (InvalidCastException) when (!throws)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts an object to a value type using <see cref="Convert.ChangeType(object,TypeCode)" />.</summary>
    /// <param name="source">The object to be converted.</param>
    /// <param name="targetType">The type to be converted to.</param>
    /// <param name="throws">if set to <c>true</c> throws exceptions when conversion fails.</param>
    /// <param name="defaultValue">The default value to return when conversion fails.</param>
    /// <returns>
    /// Converted object.
    /// </returns>
    [DebuggerStepThrough]
    public static object To(this object source, Type targetType, bool throws = false, object defaultValue = default, CultureInfo cultureInfo = null)
    {
        if (source is null)
        {
            if (targetType == typeof(Guid))
            {
                return Guid.Empty;
            }

            return defaultValue;
        }

        try
        {
            if (source.GetType() == typeof(StringValues))
            {
                return source.ToString().To(targetType);
            }

            if (targetType == typeof(Guid))
            {
                return TypeDescriptor.GetConverter(targetType).ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                return Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
            }

            if (targetType.IsEnum && (source is string || source is int || source is decimal || source is double || source is float))
            {
                try
                {
                    return Enum.Parse(targetType, source.ToString());
                }
                catch (ArgumentException)
                {
                    return default;
                }
            }

            return source;
        }
        catch (FormatException) when (!throws)
        {
            return defaultValue;
        }
        catch (InvalidCastException) when (!throws)
        {
            return defaultValue;
        }
    }

    [DebuggerStepThrough]
    public static bool TryTo<T>(this object source, out T result, CultureInfo cultureInfo = null)
    {
        if (source is null)
        {
            result = default;
            return false;
        }

        var targetType = typeof(T);

        try
        {
            if (targetType == typeof(Guid))
            {
                result = (T)TypeDescriptor.GetConverter(targetType).ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
                return true;
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                result = (T)Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType.IsEnum && (source is string || source is int || source is decimal || source is double || source is float))
            {
                result = (T)Enum.Parse(targetType, source.ToString());
                return true;
            }

            result = (T)source;
            return true;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
        catch (InvalidCastException)
        {
            result = default;
            return false;
        }
    }

    [DebuggerStepThrough]
    public static bool TryTo(this object source, Type targetType, out object result, CultureInfo cultureInfo = null)
    {
        if (source is null)
        {
            result = default;
            return false;
        }

        try
        {
            if (targetType == typeof(Guid))
            {
                result = TypeDescriptor.GetConverter(targetType).ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
                return true;
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                result = Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType.IsEnum && source is string)
            {
                result = Enum.Parse(targetType, source.ToString());
                return true;
            }

            if (targetType.IsEnum && source is int v)
            {
                result = Enum.ToObject(targetType, v);
                return true;
            }

            result = source;
            return true;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
        catch (InvalidCastException)
        {
            result = default;
            return false;
        }
    }

    private static TEnum ToEnum<TEnum>(this int source)
        where TEnum : struct
    {
        if (!typeof(TEnum).IsEnum)
        {
            return default;
        }

        if (Enum.IsDefined(typeof(TEnum), source))
        {
            //if a straightforward single value, return that
            return (TEnum)Enum.ToObject(typeof(TEnum), source);
        }

        var values = Enum.GetValues(typeof(TEnum))
            .Cast<int>()
            .ToList();

        var isBitwise = values.Select((n, i) =>
        {
            if (i < 2)
            {
                return n == 0 || n == 1;
            }

            return n / 2 == values[i - 1];
        })
        .All(y => y);

        var maxValue = values.Sum();

        if (Enum.TryParse(source.ToString(), out TEnum result)
            && (source <= maxValue || !isBitwise))
        {
            //if it can be parsed as a bitwise enum with multiple flags,
            //or is not bitwise, return the result of TryParse
            return result;
        }

        //If the value is higher than all possible combinations,
        //remove the high imaginary values not accounted for in the enum
        var excess = Enumerable
            .Range(0, 32)
            .Select(n => (int)Math.Pow(2, n))
            .Where(n => n <= source && n > 0 && !values.Contains(n))
            .Sum();

        return Enum.TryParse((source - excess).ToString(), out result) ? result : default;
    }
}