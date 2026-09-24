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
    /// <param name="source">The object to inspect.</param>
    extension(object source)
    {
        /// <summary>Determines whether an object's runtime type exactly equals a target type.</summary>
        /// <param name="targetType">The exact runtime type to match.</param>
        /// <returns><see langword="true"/> for an exact match; otherwise, <see langword="false"/>, including for null input.</returns>
        [DebuggerStepThrough]
        public bool IsOfType(Type targetType)
        {
            if (source is null)
            {
                return false;
            }

            return source.GetType() == targetType;
        }

        /// <summary>Determines whether a non-null object's runtime type differs from a target type.</summary>
        /// <param name="targetType">The exact runtime type used for comparison.</param>
        /// <returns><see langword="true"/> when the non-null object's type differs; null input returns <see langword="false"/>.</returns>
        [DebuggerStepThrough]
        public bool IsNotOfType(Type targetType)
        {
            if (source is null)
            {
                return false;
            }

            return source.GetType() != targetType;
        }
    }

    /// <param name="source">The type to inspect.</param>
    extension(Type source)
    {
        /// <summary>Determines whether a type is a constructed <see cref="Nullable{T}"/> value type.</summary>
        /// <returns><see langword="true"/> only for a constructed nullable value type.</returns>
        public bool IsNullableType()
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
        /// <returns>
        ///     <c>true</c> when the type represents a primitive, enum, common framework scalar, or smart enumeration;
        ///     otherwise, <c>false</c>.
        /// </returns>
        [DebuggerStepThrough]
        public bool IsSimpleType()
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
        /// <returns>
        ///     <c>true</c> when the type implements <see cref="IEnumerable"/> and is not treated as a scalar value such as
        ///     <see cref="string"/> or <see cref="byte"/>[]; otherwise, <c>false</c>.
        /// </returns>
        [DebuggerStepThrough]
        public bool IsCollectionType()
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
        /// <returns>
        ///     <c>true</c> when the type is not a simple scalar and represents either a collection or a non-<see cref="object"/>
        ///     reference type; otherwise, <c>false</c>.
        /// </returns>
        [DebuggerStepThrough]
        public bool SupportsStructuredValue()
        {
            if (source is null)
            {
                return false;
            }

            source = Nullable.GetUnderlyingType(source) ?? source;

            return !source.IsSimpleType() && (source.IsCollectionType() || (source.IsClass && source != typeof(object)));
        }

        /// <summary>Formats a type name without its namespace and recursively expands generic arguments.</summary>
        /// <param name="useAngleBrackets">Whether generic arguments use angle brackets instead of square brackets.</param>
        /// <returns>The readable type name, or an empty string for null input.</returns>
        [DebuggerStepThrough]
        public string PrettyName(bool useAngleBrackets = true)
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
        /// <param name="useAngleBrackets">Whether generic arguments use angle brackets instead of square brackets.</param>
        /// <returns>The readable fully qualified name, or an empty string for null input.</returns>
        [DebuggerStepThrough]
        public string FullPrettyName(bool useAngleBrackets = true)
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
        /// <returns>The shortened name, or an empty string when no assembly-qualified name is available.</returns>
        [DebuggerStepThrough]
        public string AssemblyQualifiedNameShort()
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

        /// <summary>Determines whether a non-array type represents a built-in integral, floating-point, or decimal number.</summary>
        /// <returns><see langword="true"/> for a supported numeric type.</returns>
        [DebuggerStepThrough]
        public bool IsNumeric()
        {
            if (source.IsArray)
            {
                return false;
            }

            if (source == typeof(byte) ||
                source == typeof(decimal) ||
                source == typeof(double) ||
                source == typeof(short) ||
                source == typeof(int) ||
                source == typeof(long) ||
                source == typeof(sbyte) ||
                source == typeof(float) ||
                source == typeof(ushort) ||
                source == typeof(uint) ||
                source == typeof(ulong))
            {
                return true;
            }

            switch (Type.GetTypeCode(source))
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
        /// <param name="name">The field name.</param>
        /// <param name="flags">The binding flags applied at each hierarchy level; <see cref="BindingFlags.DeclaredOnly"/> is added.</param>
        /// <returns>The first matching field, or <see langword="null"/>.</returns>
        [DebuggerStepThrough]
        public FieldInfo GetFieldUnambiguous(
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
        /// <param name="name">The property name.</param>
        /// <param name="flags">The binding flags applied at each hierarchy level; <see cref="BindingFlags.DeclaredOnly"/> is added.</param>
        /// <returns>The first matching property, or <see langword="null"/>.</returns>
        [DebuggerStepThrough]
        public PropertyInfo GetPropertyUnambiguous(
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
        [DebuggerStepThrough]
        public bool ImplementsInterface<T>()
        {
            return source.ImplementsInterface(typeof(T));
        }

        /// <summary>
        ///     Determine if a type implements a specific (open) generic interface type
        /// </summary>
        /// <param name="interface">the interface to implement</param>
        [DebuggerStepThrough]
        public bool ImplementsInterface(Type @interface)
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
        /// <param name="interfaces">The interface contracts that must all be implemented.</param>
        /// <returns><see langword="true"/> when every contract is implemented, including for an empty contract set.</returns>
        public bool ImplementsAllInterfaces(params Type[] interfaces)
        {
            return interfaces.All(source.ImplementsInterface);
        }

        /// <summary>Determines whether a type satisfies at least one supplied interface check.</summary>
        /// <param name="interfaces">The interface contracts to test.</param>
        /// <returns><see langword="true"/> when any contract is implemented.</returns>
        public bool ImplementsAnyInterface(params Type[] interfaces)
        {
            return interfaces.Any(source.ImplementsInterface);
        }

        /// <summary>
        ///     Determines whether a type, like IList&lt;int&gt;, implements an open generic interface, like
        ///     IEnumerable&lt;&gt;. Note that this only checks against *interfaces*.
        /// </summary>
        /// <param name="interface">The open generic type which it may impelement</param>
        [DebuggerStepThrough]
        public bool ImplementsOpenGenericInterface(Type @interface)
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

    //public static string AssemblyQualifiedNameShort(this Type source)
    //{
    //    // ommits the assembly version and culture
    //    var assemblyQualifiedName = source.AssemblyQualifiedName;

    //    return $"{assemblyQualifiedName.Split(',')[0]}, {assemblyQualifiedName.Split(',')[1]}".Replace("  ", " ");
    //}
}
