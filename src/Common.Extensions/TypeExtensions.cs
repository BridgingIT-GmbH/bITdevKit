// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

public static class TypeExtensions
{
    [DebuggerStepThrough]

    public static bool IsOfType(this object source, Type targetType)
    {
        if (source is null)
        {
            return false;
        }

        return source.GetType() == targetType;
    }

    [DebuggerStepThrough]
    public static bool IsNotOfType(this object source, Type targetType)
    {
        if (source is null)
        {
            return false;
        }

        return source.GetType() != targetType;
    }

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

    [DebuggerStepThrough]
    public static string AssemblyQualifiedNameShort(this Type source)
    {
        // ommits the assembly version and culture
        var assemblyQualifiedName = source.AssemblyQualifiedName;
        return $"{assemblyQualifiedName.Split(',')[0]}, {assemblyQualifiedName.Split(',')[1]}".Replace("  ", " ");
    }

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

    [DebuggerStepThrough]
    public static FieldInfo GetFieldUnambiguous(this Type source, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
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

    [DebuggerStepThrough]
    public static PropertyInfo GetPropertyUnambiguous(this Type source, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
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
    /// Determine if a type implements a specific (open) generic interface type
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

    /// <summary>Determines whether a type, like IList&lt;int&gt;, implements an open generic interface, like
    /// IEnumerable&lt;&gt;. Note that this only checks against *interfaces*.</summary>
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

        return
            source.Equals(@interface) ||
            (source.IsGenericType && source.GetGenericTypeDefinition().Equals(@interface)) ||
            source.GetInterfaces().Any(i => i.IsGenericType && i.ImplementsOpenGenericInterface(@interface));
    }
}
