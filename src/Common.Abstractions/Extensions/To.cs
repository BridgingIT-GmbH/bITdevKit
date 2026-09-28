// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Primitives;

public static partial class Extensions
{
    /// <param name="source">The value to convert.</param>
    extension(object source)
    {
        /// <summary>Converts a value to a requested type using invariant or caller-supplied culture and special handling for common framework types.</summary>
        /// <typeparam name="TValue">The target type.</typeparam>
        /// <param name="throws">Whether supported format and cast failures should be propagated.</param>
        /// <param name="defaultValue">The value returned for null input or a suppressed conversion failure.</param>
        /// <param name="cultureInfo">The culture used for textual and convertible values; invariant culture is used when omitted.</param>
        /// <returns>The converted value or <paramref name="defaultValue"/> when conversion is suppressed.</returns>
        [DebuggerStepThrough]
        public TValue To<TValue>(
            bool throws = false,
            TValue defaultValue = default,
            CultureInfo cultureInfo = null)
        {
            if (source is null)
            {
                return defaultValue;
            }

            var targetType = typeof(TValue);

            try
            {
                if (source.GetType() == typeof(StringValues))
                {
                    return source.ToString().To<TValue>();
                }

                if (targetType == typeof(Guid))
                {
                    return (TValue)TypeDescriptor.GetConverter(targetType)
                        .ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
                }

                if (targetType == typeof(DateTime))
                {
                    if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var result))
                    {
                        return (TValue)(object)result;
                    }

                    if (throws)
                    {
                        throw new FormatException($"Unable to convert '{source}' to DateTime.");
                    }

                    return defaultValue;
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    if (DateTimeOffset.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var result))
                    {
                        return (TValue)(object)result;
                    }

                    if (throws)
                    {
                        throw new FormatException($"Unable to convert '{source}' to DateTimeOffset.");
                    }

                    return defaultValue;
                }

                if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
                {
                    return (TValue)Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);
                }

                if (targetType.IsEnum &&
                    (source is string || source is int || source is decimal || source is double || source is float))
                {
                    try
                    {
                        return (TValue)Enum.Parse(targetType, source.ToString());
                    }
                    catch (ArgumentException)
                    {
                        return default;
                    }
                }

                return (TValue)source;
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

        /// <summary>Converts a value to a runtime-selected type using invariant or caller-supplied culture.</summary>
        /// <param name="targetType">The requested result type.</param>
        /// <param name="throws">Whether supported format and cast failures should be propagated.</param>
        /// <param name="defaultValue">The value returned for null input or a suppressed conversion failure.</param>
        /// <param name="cultureInfo">The culture used for textual and convertible values; invariant culture is used when omitted.</param>
        /// <returns>The converted value; null input converted to <see cref="Guid"/> returns <see cref="Guid.Empty"/>.</returns>
        [DebuggerStepThrough]
        public object To(
            Type targetType,
            bool throws = false,
            object defaultValue = null,
            CultureInfo cultureInfo = null)
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
                    return TypeDescriptor.GetConverter(targetType)
                        .ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));
                }

                if (targetType == typeof(DateTime))
                {
                    if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var result))
                    {
                        return result;
                    }

                    if (throws)
                    {
                        throw new FormatException($"Unable to convert '{source}' to DateTime.");
                    }

                    return defaultValue;
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    if (DateTimeOffset.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var result))
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

                if (targetType.IsEnum &&
                    (source is string || source is int || source is decimal || source is double || source is float))
                {
                    try
                    {
                        return Enum.Parse(targetType, source.ToString());
                    }
                    catch (ArgumentException)
                    {
                        return null;
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

        /// <summary>Attempts to convert a value to a requested type without propagating format, overflow, or cast failures.</summary>
        /// <typeparam name="TValue">The target type.</typeparam>
        /// <param name="result">The converted value when successful; otherwise, <see langword="default"/>.</param>
        /// <param name="cultureInfo">The culture used for textual and convertible values; invariant culture is used when omitted.</param>
        /// <returns><see langword="true"/> when conversion succeeds.</returns>
        [DebuggerStepThrough]
        public bool TryTo<TValue>(out TValue result, CultureInfo cultureInfo = null)
        {
            if (source is null)
            {
                result = default;

                return false;
            }

            var targetType = typeof(TValue);

            try
            {
                if (targetType == typeof(Guid))
                {
                    result = (TValue)TypeDescriptor.GetConverter(targetType)
                        .ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));

                    return true;
                }

                if (targetType == typeof(DateTime))
                {
                    if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var dateTimeResult))
                    {
                        result = (TValue)(object)dateTimeResult;

                        return true;
                    }

                    result = default;

                    return false;
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    if (DateTimeOffset.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var dateTimeResult))
                    {
                        result = (TValue)(object)dateTimeResult;

                        return true;
                    }

                    result = default;

                    return false;
                }

                if (targetType is IConvertible || (targetType.IsValueType && !targetType.IsEnum))
                {
                    result = (TValue)Convert.ChangeType(source, targetType, cultureInfo ?? CultureInfo.InvariantCulture);

                    return true;
                }

                if (targetType.IsEnum &&
                    (source is string || source is int || source is decimal || source is double || source is float))
                {
                    try
                    {
                        result = (TValue)Enum.Parse(targetType, source.ToString());

                        return true;
                    }
                    catch (ArgumentException)
                    {
                        result = default;

                        return false;
                    }
                }

                result = (TValue)source;

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

        /// <summary>Attempts to convert a value to a runtime-selected type without propagating format, overflow, or cast failures.</summary>
        /// <param name="targetType">The requested result type.</param>
        /// <param name="result">The converted value when successful; otherwise, <see langword="null"/>.</param>
        /// <param name="cultureInfo">The culture used for textual and convertible values; invariant culture is used when omitted.</param>
        /// <returns><see langword="true"/> when conversion succeeds.</returns>
        [DebuggerStepThrough]
        public bool TryTo(Type targetType, out object result, CultureInfo cultureInfo = null)
        {
            if (source is null)
            {
                result = null;

                return false;
            }

            try
            {
                if (targetType == typeof(Guid))
                {
                    result = TypeDescriptor.GetConverter(targetType)
                        .ConvertFrom(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture));

                    return true;
                }

                if (targetType == typeof(DateTime))
                {
                    if (DateTime.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var dateTimeResult))
                    {
                        result = dateTimeResult;

                        return true;
                    }

                    result = null;

                    return false;
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    if (DateTimeOffset.TryParse(Convert.ToString(source, cultureInfo ?? CultureInfo.InvariantCulture),
                            cultureInfo ?? CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var dateTimeResult))
                    {
                        result = dateTimeResult;

                        return true;
                    }

                    result = null;

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
                        result = null;

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
                result = null;

                return false;
            }
            catch (FormatException)
            {
                result = null;

                return false;
            }
            catch (InvalidCastException)
            {
                result = null;

                return false;
            }
        }
    }

}
