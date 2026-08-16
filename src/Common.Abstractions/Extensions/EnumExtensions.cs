// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;

/// <summary>
/// Provides enum validation and attribute metadata access helpers.
/// </summary>
public static class EnumExtensions
{
    /// <summary>Gets the <see cref="DescriptionAttribute.Description"/> assigned to an enum member.</summary>
    /// <param name="enum">The enum value whose member metadata should be inspected.</param>
    /// <returns>The member's description.</returns>
    /// <exception cref="ArgumentException">Thrown when exactly one description attribute cannot be found.</exception>
    public static string ToDescription(this Enum @enum)
    {
        var attribute = GetText<DescriptionAttribute>(@enum);

        return attribute.Description;
    }

    /// <summary>Gets the single attribute of a requested type assigned to an enum member.</summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="enum">The enum value whose member metadata should be inspected.</param>
    /// <returns>The assigned attribute.</returns>
    /// <exception cref="ArgumentException">Thrown when the enum member or exactly one matching attribute cannot be found.</exception>
    public static T GetText<T>(Enum @enum)
        where T : Attribute
    {
        var type = @enum.GetType();

        var memberInfo = type.GetMember(@enum.ToString());

        if (memberInfo is not null && !memberInfo.Any())
        {
            throw new ArgumentException($"No public members for the argument '{@enum}'.");
        }

        var attributes = memberInfo[0].GetCustomAttributes(typeof(T), false);
        if (attributes is null || attributes.Length != 1)
        {
            throw new ArgumentException(
                $"Can't find an attribute matching '{typeof(T).Name}' for the argument '{@enum}'");
        }

        return attributes.Single() as T;
    }

    /// <summary>
    ///     Tries and parses an enum and it's default type.
    /// </summary>
    /// <returns>True if the enum value is defined.</returns>
    public static bool TryEnumIsDefined(Type type, object value)
    {
        if (type is null || value is null || !type.GetTypeInfo().IsEnum)
        {
            return false;
        }

        // Return true if the value is an enum and is a matching type.
        if (type == value.GetType())
        {
            return true;
        }

        if (TryEnumIsDefined<int>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<string>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<byte>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<short>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<long>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<sbyte>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<ushort>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<uint>(type, value))
        {
            return true;
        }

        if (TryEnumIsDefined<ulong>(type, value))
        {
            return true;
        }

        return false;
    }

    /// <summary>Safely tests whether a value is defined by an enum type, returning false for invalid inputs or conversions.</summary>
    /// <typeparam name="T">A compatibility type parameter retained for callers selecting an underlying representation.</typeparam>
    /// <param name="type">The enum type to inspect.</param>
    /// <param name="value">The name, underlying value, or enum value to test.</param>
    /// <returns><see langword="true"/> when <see cref="Enum.IsDefined(Type, object)"/> accepts the value; otherwise, <see langword="false"/>.</returns>
    public static bool TryEnumIsDefined<T>(Type type, object value)
    {
        // Catch any casting errors that can occur or if 0 is not defined as a default value.
        try
        {
            if (type is not null && value is not null && Enum.IsDefined(type, value))
            {
                return true;
            }
        }
        catch
        {
            // ignore, return false;
        }

        return false;
    }

    /// <summary>Projects a value from an attribute assigned to an enum member.</summary>
    /// <typeparam name="T">The member attribute type.</typeparam>
    /// <typeparam name="TE">The projected value type.</typeparam>
    /// <param name="enumeration">The enum value whose member should be inspected.</param>
    /// <param name="expression">The projection applied to the attribute.</param>
    /// <returns>The projected value, or <see langword="default"/> when the attribute is absent.</returns>
    public static TE GetAttributeValue<T, TE>(this Enum enumeration, Func<T, TE> expression)
        where T : Attribute
    {
        var attribute = enumeration.GetType()
            .GetMember(enumeration.ToString())
            .Where(member => member.MemberType == MemberTypes.Field)
            .FirstOrDefault()
            .GetCustomAttributes(typeof(T), false)
            .Cast<T>()
            .SingleOrDefault();

        if (attribute is null)
        {
            return default;
        }

        return expression(attribute);
    }

    /// <summary>Projects a value from an attribute assigned to a type, including inherited attributes.</summary>
    /// <typeparam name="TAttribute">The type attribute to locate.</typeparam>
    /// <typeparam name="TValue">The projected value type.</typeparam>
    /// <param name="type">The type whose attributes should be inspected.</param>
    /// <param name="valueSelector">The projection applied to the first matching attribute.</param>
    /// <returns>The projected value, or <see langword="default"/> when the attribute is absent.</returns>
    public static TValue GetAttributeValue<TAttribute, TValue>(this Type type, Func<TAttribute, TValue> valueSelector)
        where TAttribute : Attribute
    {
        var att = type.GetCustomAttributes(typeof(TAttribute), true).FirstOrDefault() as TAttribute;
        if (att is not null)
        {
            return valueSelector(att);
        }

        return default;
    }

    /// <summary>Enumerates serialized names for every value of an enum type.</summary>
    /// <typeparam name="T">The enum type to inspect.</typeparam>
    /// <returns>Each <see cref="EnumMemberAttribute.Value"/> when present, otherwise the enum member name.</returns>
    public static IEnumerable<string> GetEnumMemberValues<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T))
                   .Cast<T>()
                   .Select(e => e.GetEnumMemberValue() ?? e.ToString())
                   .Where(v => v != null)!;
    }

    /// <summary>Gets the serialization name assigned to an enum member.</summary>
    /// <param name="enumValue">The enum value whose field metadata should be inspected.</param>
    /// <returns>The <see cref="EnumMemberAttribute.Value"/>, or <see langword="null"/> when the attribute is absent.</returns>
    public static string GetEnumMemberValue(this Enum enumValue)
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        var attribute = field?.GetCustomAttribute<EnumMemberAttribute>();
        return attribute?.Value;
    }
}
