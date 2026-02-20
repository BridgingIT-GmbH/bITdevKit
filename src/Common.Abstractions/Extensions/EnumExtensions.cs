// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;

public static class EnumExtensions
{
    public static string ToDescription(this Enum @enum)
    {
        var attribute = GetText<DescriptionAttribute>(@enum);

        return attribute.Description;
    }

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

    public static IEnumerable<string> GetEnumMemberValues<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T))
                   .Cast<T>()
                   .Select(e => e.GetEnumMemberValue() ?? e.ToString())
                   .Where(v => v != null)!;
    }

    public static string GetEnumMemberValue(this Enum enumValue)
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        var attribute = field?.GetCustomAttribute<EnumMemberAttribute>();
        return attribute?.Value;
    }
}