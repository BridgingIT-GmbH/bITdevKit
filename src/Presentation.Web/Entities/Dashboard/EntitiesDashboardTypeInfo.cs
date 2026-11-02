// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Collections;
using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Shared reflection helpers that classify entity property types for the Entities dashboard.</summary>
internal static class EntitiesDashboardTypeInfo
{
    /// <summary>Returns the type without its <see cref="Nullable{T}"/> wrapper.</summary>
    public static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>Determines whether the type derives from <c>EntityId&lt;T&gt;</c> and returns the primitive value type.</summary>
    public static bool IsTypedId(Type type, out Type primitiveType)
    {
        for (var current = Unwrap(type); current != null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityId<>))
            {
                primitiveType = current.GetGenericArguments()[0];

                return true;
            }
        }

        primitiveType = null;

        return false;
    }

    /// <summary>Determines whether the type is an <c>EnumerationValueObject</c> or an <c>Enumeration</c> class.</summary>
    public static bool IsEnumeration(Type type)
    {
        for (var current = Unwrap(type); current != null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType
                && (current.GetGenericTypeDefinition() == typeof(EnumerationValueObject<,>)
                    || current.GetGenericTypeDefinition() == typeof(EnumerationValueObject<>)
                    || current.GetGenericTypeDefinition() == typeof(Enumeration<,>)
                    || current.GetGenericTypeDefinition() == typeof(Enumeration<>)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the type is a CLR primitive-like scalar.</summary>
    public static bool IsScalar(Type type)
    {
        type = Unwrap(type);

        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly);
    }

    /// <summary>Determines whether the type is a collection (strings and byte arrays are not).</summary>
    public static bool IsCollection(Type type)
    {
        type = Unwrap(type);

        return type != typeof(string) && type != typeof(byte[]) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <summary>Gets the element type of a collection type, or null when it cannot be determined.</summary>
    public static Type GetElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0];
    }

    /// <summary>Gets the public readable instance properties, resolving shadowed members to the most derived declaration.</summary>
    public static IReadOnlyList<PropertyInfo> GetPublicProperties(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod?.IsPublic == true)
            .ToList();

        var result = new List<PropertyInfo>(properties.Count);
        foreach (var group in properties.GroupBy(p => p.Name, StringComparer.Ordinal))
        {
            result.Add(group.OrderByDescending(p => Depth(p.DeclaringType)).First());
        }

        return result
            .OrderBy(p => properties.IndexOf(p))
            .ToList();
    }

    /// <summary>Finds a public readable instance property by exact name.</summary>
    public static PropertyInfo FindProperty(Type type, string name) =>
        GetPublicProperties(type).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var current = type; current?.BaseType != null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }
}
