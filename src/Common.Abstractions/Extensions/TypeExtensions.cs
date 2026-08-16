// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections;
using System.Diagnostics;
using System.Reflection;

/// <summary>
/// Provides runtime type classification, readable type names, hierarchy-safe member lookup, and interface checks.
/// </summary>
public static class TypeExtensions
{
    /// <summary>Determines whether an object's runtime type exactly equals a target type.</summary>
    /// <param name="source">The object to inspect.</param>
    /// <param name="targetType">The exact runtime type to match.</param>
    /// <returns><see langword="true"/> for an exact match; otherwise, <see langword="false"/>, including for null input.</returns>
    [DebuggerStepThrough]
    public static bool IsOfType(this object source, Type targetType)
    {
        if (source is null)
        {
            return false;
        }

        return source.GetType() == targetType;
    }

    /// <summary>Determines whether a non-null object's runtime type differs from a target type.</summary>
    /// <param name="source">The object to inspect.</param>
    /// <param name="targetType">The exact runtime type used for comparison.</param>
    /// <returns><see langword="true"/> when the non-null object's type differs; null input returns <see langword="false"/>.</returns>
    [DebuggerStepThrough]
    public static bool IsNotOfType(this object source, Type targetType)
    {
        if (source is null)
        {
            return false;
        }

        return source.GetType() != targetType;
    }

    /// <summary>Determines whether a type is a constructed <see cref="Nullable{T}"/> value type.</summary>
    /// <param name="source">The type to inspect.</param>
    /// <returns><see langword="true"/> only for a constructed nullable value type.</returns>
    public static bool IsNullableType(this Type source)
    {
        if (source is null)
        {
            return false;
        }

        return source.IsGenericType && source.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    ///     Determines whether the specified type should be treated as a simple scalar value.
    /// </summary>
    /// <param name="source">The type to inspect.</param>
    /// <returns>
    ///     <c>true</c> when the type represents a primitive, enum, common framework scalar, or smart enumeration;
    ///     otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool IsSimpleType(this Type source)
    {
        if (source is null)
        {
            return false;
        }

        source = Nullable.GetUnderlyingType(source) ?? source;

        return source.IsPrimitive
            || source.IsEnum
            || source == typeof(string)
            || source == typeof(decimal)
            || source == typeof(DateTime)
            || source == typeof(DateTimeOffset)
            || source == typeof(DateOnly)
            || source == typeof(TimeOnly)
            || source == typeof(TimeSpan)
            || source == typeof(Guid)
            || source == typeof(Uri)
            || typeof(IEnumeration).IsAssignableFrom(source);
    }

    /// <summary>
    ///     Determines whether the specified type represents a collection.
    /// </summary>
    /// <param name="source">The type to inspect.</param>
    /// <returns>
    ///     <c>true</c> when the type implements <see cref="IEnumerable"/> and is not treated as a scalar value such as
    ///     <see cref="string"/> or <see cref="byte"/>[]; otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool IsCollectionType(this Type source)
    {
        if (source is null)
        {
            return false;
        }

        if (source == typeof(string))
        {
            return false;
        }

        return typeof(IEnumerable).IsAssignableFrom(source) && source != typeof(byte[]);
    }

    /// <summary>
    ///     Determines whether the specified type can be represented as a structured value such as a nested object or collection.
    /// </summary>
    /// <param name="source">The type to inspect.</param>
    /// <returns>
    ///     <c>true</c> when the type is not a simple scalar and represents either a collection or a non-<see cref="object"/>
    ///     reference type; otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool SupportsStructuredValue(this Type source)
    {
        if (source is null)
        {
            return false;
        }

        source = Nullable.GetUnderlyingType(source) ?? source;

        return !source.IsSimpleType() && (source.IsCollectionType() || (source.IsClass && source != typeof(object)));
    }

    /// <summary>Formats a type name without its namespace and recursively expands generic arguments.</summary>
    /// <param name="source">The type to format.</param>
    /// <param name="useAngleBrackets">Whether generic arguments use angle brackets instead of square brackets.</param>
    /// <returns>The readable type name, or an empty string for null input.</returns>
    [DebuggerStepThrough]
    public static string PrettyName(this Type source, bool useAngleBrackets = true)
    {
        if (source is null)
        {
            return string.Empty;
        }

        if (source.IsGenericType)
        {
            var genericOpen = useAngleBrackets ? "<" : "[";
            var genericClose = useAngleBrackets ? ">" : "]";
            var name = source.Name.Substring(0, source.Name.IndexOf('`'));
            var types = string.Join(",", source.GetGenericArguments().Select(t => t.PrettyName(useAngleBrackets)));

            return $"{name}{genericOpen}{types}{genericClose}";
        }

        return source.Name;
    }

    /// <summary>Formats a namespace-qualified type name and recursively expands generic arguments.</summary>
    /// <param name="source">The type to format.</param>
    /// <param name="useAngleBrackets">Whether generic arguments use angle brackets instead of square brackets.</param>
    /// <returns>The readable fully qualified name, or an empty string for null input.</returns>
    [DebuggerStepThrough]
    public static string FullPrettyName(this Type source, bool useAngleBrackets = true)
    {
        if (source is null)
        {
            return string.Empty;
        }

        if (source.IsGenericType)
        {
            var genericOpen = useAngleBrackets ? "<" : "[";
            var genericClose = useAngleBrackets ? ">" : "]";
            var name = source.FullName.Substring(0, source.FullName.IndexOf('`'));
            var types = string.Join(",", source.GetGenericArguments().Select(t => t.FullPrettyName(useAngleBrackets)));

            return $"{name}{genericOpen}{types}{genericClose}";
        }

        return source.FullName;
    }

    /// <summary>Removes assembly version, culture, and public-key-token components from an assembly-qualified type name.</summary>
    /// <param name="source">The type whose assembly-qualified name should be shortened.</param>
    /// <returns>The shortened name, or an empty string when no assembly-qualified name is available.</returns>
    [DebuggerStepThrough]
    public static string AssemblyQualifiedNameShort(this Type source)
    {
        var aqn = source.AssemblyQualifiedName; // Remove version, culture, and public key token info but preserve structure
        if (string.IsNullOrEmpty(aqn))
        {
            return string.Empty;
        }

        var regex = new System.Text.RegularExpressions.Regex(
            @", Version=\d+\.\d+\.\d+\.\d+, Culture=\w+, PublicKeyToken=\w+");

        return regex.Replace(aqn, "").Replace("  ", " ");
    }
    //public static string AssemblyQualifiedNameShort(this Type source)
    //{
    //    // ommits the assembly version and culture
    //    var assemblyQualifiedName = source.AssemblyQualifiedName;

    //    return $"{assemblyQualifiedName.Split(',')[0]}, {assemblyQualifiedName.Split(',')[1]}".Replace("  ", " ");
    //}

    /// <summary>Determines whether a non-array type represents a built-in integral, floating-point, or decimal number.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for a supported numeric type.</returns>
    [DebuggerStepThrough]
    public static bool IsNumeric(this Type type)
    {
        if (type.IsArray)
        {
            return false;
        }

        if (type == typeof(byte) ||
            type == typeof(decimal) ||
            type == typeof(double) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(sbyte) ||
            type == typeof(float) ||
            type == typeof(ushort) ||
            type == typeof(uint) ||
            type == typeof(ulong))
        {
            return true;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.SByte:
            case TypeCode.Single:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
        }

        return false;
    }

    /// <summary>Finds a field by walking from a type through its base types without triggering ambiguous reflection matches.</summary>
    /// <param name="source">The most-derived type to inspect.</param>
    /// <param name="name">The field name.</param>
    /// <param name="flags">The binding flags applied at each hierarchy level; <see cref="BindingFlags.DeclaredOnly"/> is added.</param>
    /// <returns>The first matching field, or <see langword="null"/>.</returns>
    [DebuggerStepThrough]
    public static FieldInfo GetFieldUnambiguous(
        this Type source,
        string name,
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(name);

        flags |= BindingFlags.DeclaredOnly;

        while (source is not null)
        {
            var field = source.GetField(name, flags);

            if (field is not null)
            {
                return field;
            }

            source = source.BaseType;
        }

        return null;
    }

    /// <summary>Finds a property by walking from a type through its base types without triggering ambiguous reflection matches.</summary>
    /// <param name="source">The most-derived type to inspect.</param>
    /// <param name="name">The property name.</param>
    /// <param name="flags">The binding flags applied at each hierarchy level; <see cref="BindingFlags.DeclaredOnly"/> is added.</param>
    /// <returns>The first matching property, or <see langword="null"/>.</returns>
    [DebuggerStepThrough]
    public static PropertyInfo GetPropertyUnambiguous(
        this Type source,
        string name,
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(name);

        flags |= BindingFlags.DeclaredOnly;

        while (source is not null)
        {
            var property = source.GetProperty(name, flags);

            if (property is not null)
            {
                return property;
            }

            source = source.BaseType;
        }

        return null;
    }

    /// <summary>
    ///     Determine if a type implements a specific (open) generic interface type
    /// </summary>
    /// <param name="source">the instance to check</param>
    [DebuggerStepThrough]
    public static bool ImplementsInterface<T>(this Type source)
    {
        return source.ImplementsInterface(typeof(T));
    }

    /// <summary>
    ///     Determine if a type implements a specific (open) generic interface type
    /// </summary>
    /// <param name="source">the instance to check</param>
    /// <param name="interface">the interface to implement</param>
    [DebuggerStepThrough]
    public static bool ImplementsInterface(this Type source, Type @interface)
    {
        //EnsureArg.IsTrue(@interface?.IsInterface == true);

        if (source is null || @interface is null)
        {
            return false;
        }

        return @interface.GenericTypeArguments.Length > 0
            ? @interface.IsAssignableFrom(source)
            : source.GetInterfaces().Any(c => c.Name == @interface.Name);
    }

    /// <summary>Determines whether a type satisfies every supplied interface check.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaces">The interface contracts that must all be implemented.</param>
    /// <returns><see langword="true"/> when every contract is implemented, including for an empty contract set.</returns>
    public static bool ImplementsAllInterfaces(this Type type, params Type[] interfaces)
    {
        return interfaces.All(type.ImplementsInterface);
    }

    /// <summary>Determines whether a type satisfies at least one supplied interface check.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaces">The interface contracts to test.</param>
    /// <returns><see langword="true"/> when any contract is implemented.</returns>
    public static bool ImplementsAnyInterface(this Type type, params Type[] interfaces)
    {
        return interfaces.Any(type.ImplementsInterface);
    }

    /// <summary>
    ///     Determines whether a type, like IList&lt;int&gt;, implements an open generic interface, like
    ///     IEnumerable&lt;&gt;. Note that this only checks against *interfaces*.
    /// </summary>
    /// <param name="source">The type to check.</param>
    /// <param name="interface">The open generic type which it may impelement</param>
    [DebuggerStepThrough]
    public static bool ImplementsOpenGenericInterface(this Type source, Type @interface)
    {
        //EnsureArg.IsTrue(@interface?.IsInterface == true);

        if (source is null || @interface is null)
        {
            return false;
        }

        return source.Equals(@interface) ||
            (source.IsGenericType && source.GetGenericTypeDefinition().Equals(@interface)) ||
            source.GetInterfaces().Any(i => i.IsGenericType && i.ImplementsOpenGenericInterface(@interface));
    }
}
