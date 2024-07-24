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

            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                                      cultureInfo ?? CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                      out DateTime result))
                {
                    return (T)(object)result;
                }

                if (throws)
                {
                    throw new FormatException($"Unable to convert '{source}' to DateTime.");
                }

                return defaultValue;
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

            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                                      cultureInfo ?? CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                      out DateTime result))
                {
                    return result;
                }

                if (throws)
                {
                    throw new FormatException($"Unable to convert '{source}' to DateTime.");
                }

                return defaultValue;
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

            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                                      cultureInfo ?? CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                      out DateTime dateTimeResult))
                {
                    result = (T)(object)dateTimeResult;
                    return true;
                }

                result = default;
                return false;
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                result = (T)Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType.IsEnum && (source is string || source is int || source is decimal || source is double || source is float))
            {
                try
                {
                    result = (T)Enum.Parse(targetType, source.ToString());
                    return true;
                }
                catch (ArgumentException)
                {
                    result = default;
                    return false;
                }
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

            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                                      cultureInfo ?? CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                      out DateTime dateTimeResult))
                {
                    result = dateTimeResult;
                    return true;
                }

                result = default;
                return false;
            }

            if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
            {
                result = Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType.IsEnum && source is string)
            {
                try
                {
                    result = Enum.Parse(targetType, source.ToString());
                    return true;
                }
                catch (ArgumentException)
                {
                    result = default;
                    return false;
                }
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
            return result;
        }

        var excess = Enumerable
            .Range(0, 32)
            .Select(n => (int)Math.Pow(2, n))
            .Where(n => n <= source && n > 0 && !values.Contains(n))
            .Sum();

        return Enum.TryParse((source - excess).ToString(), out result) ? result : default;
    }
}