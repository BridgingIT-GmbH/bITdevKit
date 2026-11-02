// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using BridgingIT.DevKit.Common;

/// <summary>A selectable value of an enum or enumeration property.</summary>
/// <param name="Value">The stored value (enum name or enumeration key).</param>
/// <param name="Label">The display text.</param>
internal sealed record EntitiesDashboardOption(string Value, string Label);

/// <summary>Converts scalar property values between their CLR form and invariant text.</summary>
internal static class EntitiesDashboardValueConverter
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<EntitiesDashboardOption>> OptionCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object>> KeyReaders = new();

    /// <summary>Parses text into an instance of a scalar property type.</summary>
    /// <param name="type">The property type (nullable wrappers are removed).</param>
    /// <param name="text">The text to parse.</param>
    /// <param name="propertyName">The property name used in failure messages.</param>
    /// <returns>The parsed value, or a failed result with a validation message.</returns>
    public static Result<object> Parse(Type type, string text, string propertyName)
    {
        var under = EntitiesDashboardTypeInfo.Unwrap(type);
        text = text?.Trim();

        if (under == typeof(string))
        {
            return Result<object>.Success(text);
        }

        if (string.IsNullOrEmpty(text))
        {
            return Invalid(propertyName, "A value is required.", text);
        }

        try
        {
            if (EntitiesDashboardTypeInfo.IsTypedId(under, out _))
            {
                var id = EntitiesDashboardIdConverter.Parse(under, text);

                return id.IsSuccess
                    ? Result<object>.Success(id.Value)
                    : Invalid(propertyName, id.Messages.FirstOrDefault() ?? "The id is not valid.", text);
            }

            if (EntitiesDashboardTypeInfo.IsEnumeration(under))
            {
                var option = FindEnumeration(under, text);

                return option != null
                    ? Result<object>.Success(option)
                    : Invalid(propertyName, $"'{text}' is not a known {under.Name} value.", text);
            }

            object value = null;
            if (under == typeof(Guid))
            {
                value = Guid.TryParse(text, out var guid) ? guid : null;
            }
            else if (under == typeof(bool))
            {
                value = bool.TryParse(text, out var flag) ? flag : null;
            }
            else if (under == typeof(DateTimeOffset))
            {
                value = DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto) ? dto : null;
            }
            else if (under == typeof(DateTime))
            {
                value = DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
            }
            else if (under == typeof(DateOnly))
            {
                value = DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var date) ? date : null;
            }
            else if (under == typeof(TimeOnly))
            {
                value = TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var time) ? time : null;
            }
            else if (under == typeof(TimeSpan))
            {
                value = TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span) ? span : null;
            }
            else if (under == typeof(char))
            {
                value = text.Length == 1 ? text[0] : null;
            }
            else if (under.IsEnum)
            {
                value = Enum.TryParse(under, text, true, out var parsed) && IsDefined(under, parsed) ? parsed : null;
            }
            else if (under.IsPrimitive || under == typeof(decimal))
            {
                value = Convert.ChangeType(text, under, CultureInfo.InvariantCulture);
            }

            return value != null
                ? Result<object>.Success(value)
                : Invalid(propertyName, $"'{text}' is not a valid {GetTypeLabel(under)}.", text);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException or ArgumentException)
        {
            return Invalid(propertyName, $"'{text}' is not a valid {GetTypeLabel(under)}.", text);
        }
    }

    /// <summary>Formats a scalar value as invariant text, or null for null.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The invariant text.</returns>
    public static string Format(object value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return text;
            case bool flag:
                return flag ? "true" : "false";
            case DateTime dt:
                return dt.ToString("O", CultureInfo.InvariantCulture);
            case DateTimeOffset dto:
                return dto.ToString("O", CultureInfo.InvariantCulture);
            case DateOnly date:
                return date.ToString("O", CultureInfo.InvariantCulture);
            case TimeOnly time:
                return time.ToString("O", CultureInfo.InvariantCulture);
            case TimeSpan span:
                return span.ToString("c", CultureInfo.InvariantCulture);
        }

        var type = value.GetType();
        if (EntitiesDashboardTypeInfo.IsTypedId(type, out _))
        {
            return EntitiesDashboardIdConverter.Format(value);
        }

        if (EntitiesDashboardTypeInfo.IsEnumeration(type))
        {
            return FormatInvariant(KeyReaders.GetOrAdd(type, BuildKeyReader)(value));
        }

        return value is Enum ? value.ToString() : FormatInvariant(value);
    }

    /// <summary>Gets the display name of an enumeration value, falling back to its key.</summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The display text.</returns>
    public static string GetEnumerationName(object value)
    {
        if (value == null)
        {
            return null;
        }

        var type = value.GetType();

        return type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as string
            ?? type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)?.ToString()
            ?? Format(value);
    }

    /// <summary>Gets the selectable options of an enum or enumeration type.</summary>
    /// <param name="type">The property type.</param>
    /// <returns>The options, or an empty list for other types.</returns>
    public static IReadOnlyList<EntitiesDashboardOption> GetOptions(Type type)
    {
        var under = EntitiesDashboardTypeInfo.Unwrap(type);

        return OptionCache.GetOrAdd(under, t =>
        {
            if (t.IsEnum)
            {
                return Enum.GetNames(t).Select(n => new EntitiesDashboardOption(n, n)).ToList();
            }

            if (EntitiesDashboardTypeInfo.IsEnumeration(t))
            {
                return GetEnumerations(t)
                    .Select(e => new EntitiesDashboardOption(Format(e), GetEnumerationName(e)))
                    .ToList();
            }

            return [];
        });
    }

    private static object FindEnumeration(Type type, string text) =>
        GetEnumerations(type).FirstOrDefault(e => string.Equals(Format(e), text, StringComparison.OrdinalIgnoreCase))
        ?? GetEnumerations(type).FirstOrDefault(e => string.Equals(GetEnumerationName(e), text, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<object> GetEnumerations(Type type)
    {
        var field = type.GetField("All", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (field?.GetValue(null) is IEnumerable all)
        {
            return all.Cast<object>().ToList();
        }

        // Enumeration classes declare their members as public static fields
        return type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => type.IsAssignableFrom(f.FieldType))
            .Select(f => f.GetValue(null))
            .Where(v => v != null)
            .ToList();
    }

    private static bool IsDefined(Type enumType, object value) =>
        Enum.IsDefined(enumType, value) || enumType.GetCustomAttribute<FlagsAttribute>() != null;

    private static string GetTypeLabel(Type type) => type == typeof(decimal) || type.IsPrimitive ? "number" : type.Name;

    private static string FormatInvariant(object value) =>
        value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();

    private static Func<object, object> BuildKeyReader(Type type)
    {
        var property = type.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        return instance => property?.GetValue(instance);
    }

    private static Result<object> Invalid(string propertyName, string message, string attemptedValue) =>
        Result<object>.Failure(message, new ValidationError(message, propertyName, attemptedValue));
}
