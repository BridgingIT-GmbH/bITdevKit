// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Common;

/// <summary>
/// Defines how smart enumeration values are represented during import and export.
/// </summary>
public enum EnumerationConverterMode
{
    Id,
    Value
}

/// <summary>
/// Converts smart enumerations based on <see cref="IEnumeration"/> using their identifier or value.
/// </summary>
/// <typeparam name="TEnumeration">The smart enumeration type.</typeparam>
public class EnumerationConverter<TEnumeration> : EnumerationConverter<TEnumeration, int, string>
    where TEnumeration : class, IEnumeration;

/// <summary>
/// Converts smart enumerations based on <see cref="IEnumeration{TValue}"/> using their identifier or value.
/// </summary>
/// <typeparam name="TEnumeration">The smart enumeration type.</typeparam>
/// <typeparam name="TValue">The smart enumeration value type.</typeparam>
public class EnumerationConverter<TEnumeration, TValue> : EnumerationConverter<TEnumeration, int, TValue>
    where TEnumeration : class, IEnumeration<TValue>;

/// <summary>
/// Converts smart enumerations based on <see cref="IEnumeration{TId, TValue}"/> using their identifier or value.
/// </summary>
/// <typeparam name="TEnumeration">The smart enumeration type.</typeparam>
/// <typeparam name="TId">The smart enumeration identifier type.</typeparam>
/// <typeparam name="TValue">The smart enumeration value type.</typeparam>
public class EnumerationConverter<TEnumeration, TId, TValue> : IValueConverter<TEnumeration>
    where TEnumeration : class, IEnumeration<TId, TValue>
    where TId : IComparable
{
    /// <summary>
    /// Gets the mode to use during export.
    /// </summary>
    public EnumerationConverterMode ExportMode { get; init; } = EnumerationConverterMode.Value;

    /// <summary>
    /// Gets the mode to use during import.
    /// </summary>
    public EnumerationConverterMode ImportMode { get; init; } = EnumerationConverterMode.Value;

    /// <summary>
    /// Gets the culture to use for formatting and parsing.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets a value indicating whether string comparisons should ignore casing.
    /// </summary>
    public bool IgnoreCase { get; init; } = true;

    /// <inheritdoc/>
    public object ConvertToExport(TEnumeration value, ValueConversionContext context)
    {
        if (value is null)
        {
            return null;
        }

        object selectedValue = this.ExportMode switch
        {
            EnumerationConverterMode.Id => value.Id,
            _ => value.Value
        };

        var format = context.Format;
        var culture = this.Culture ?? context.Culture;

        return !string.IsNullOrWhiteSpace(format) && selectedValue is IFormattable formattable
            ? formattable.ToString(format, culture)
            : selectedValue;
    }

    /// <inheritdoc/>
    public TEnumeration ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is TEnumeration enumeration)
        {
            return enumeration;
        }

        return this.ImportMode switch
        {
            EnumerationConverterMode.Id => this.FindById(value, context),
            _ => this.FindByValue(value, context)
        };
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value as TEnumeration, context);
    }

    /// <inheritdoc/>
    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }

    private TEnumeration FindById(object value, ValueConversionContext context)
    {
        if (!this.TryConvert(value, this.Culture ?? context.Culture, out TId convertedValue))
        {
            return default;
        }

        return BridgingIT.DevKit.Common.Enumeration<TId, TValue>.GetAll<TEnumeration>()
            .FirstOrDefault(e => this.AreEqual(e.Id, convertedValue));
    }

    private TEnumeration FindByValue(object value, ValueConversionContext context)
    {
        if (!this.TryConvert(value, this.Culture ?? context.Culture, out TValue convertedValue))
        {
            return default;
        }

        return BridgingIT.DevKit.Common.Enumeration<TId, TValue>.GetAll<TEnumeration>()
            .FirstOrDefault(e => this.AreEqual(e.Value, convertedValue));
    }

    private bool AreEqual<T>(T left, T right)
    {
        if (typeof(T) == typeof(string) && this.IgnoreCase)
        {
            return string.Equals(left as string, right as string, StringComparison.OrdinalIgnoreCase);
        }

        return EqualityComparer<T>.Default.Equals(left, right);
    }

    private bool TryConvert<T>(object value, CultureInfo culture, out T result)
    {
        if (value is T typedValue)
        {
            result = typedValue;
            return true;
        }

        if (value is null)
        {
            result = default;
            return false;
        }

        var targetType = typeof(T);
        var stringValue = value.ToString();

        if (targetType == typeof(string))
        {
            result = (T)(object)stringValue;
            return true;
        }

        if (targetType == typeof(Guid) && Guid.TryParse(stringValue, out var guidValue))
        {
            result = (T)(object)guidValue;
            return true;
        }

        if (targetType.IsEnum && Enum.TryParse(targetType, stringValue, this.IgnoreCase, out var enumValue))
        {
            result = (T)enumValue;
            return true;
        }

        try
        {
            result = (T)Convert.ChangeType(value, targetType, culture);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
