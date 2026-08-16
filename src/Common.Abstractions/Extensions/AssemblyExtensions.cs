// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Reflection;

/// <summary>
/// Provides resilient assembly type discovery and build metadata helpers.
/// </summary>
public static class AssemblyExtensions
{
    /// <summary>
    /// Enumerates loadable types from each supplied assembly, ignoring assemblies that are <see langword="null"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to inspect.</param>
    /// <returns>All types that can be loaded, or an empty sequence when <paramref name="assemblies"/> is <see langword="null"/>.</returns>
    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies)
    {
        return assemblies is null ? [] : assemblies.SelectMany(SafeGetTypes);
    }

    /// <summary>
    /// Enumerates the types that can be loaded from an assembly, retaining partial results when some types fail to load.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>The loadable types, or an empty sequence when <paramref name="assembly"/> is <see langword="null"/>.</returns>
    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly)
    {
        if (assembly is null)
        {
            return [];
        }

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }

    /// <summary>
    /// Enumerates loadable types from the supplied assemblies that implement <typeparamref name="TInterface"/>.
    /// </summary>
    /// <typeparam name="TInterface">The interface contract used to filter discovered types.</typeparam>
    /// <param name="assemblies">The assemblies to inspect.</param>
    /// <returns>Matching loadable types, or an empty sequence for a <see langword="null"/> source.</returns>
    public static IEnumerable<Type> SafeGetTypes<TInterface>(this IEnumerable<Assembly> assemblies)
    {
        return SafeGetTypes(assemblies, typeof(TInterface));
    }

    /// <summary>
    /// Enumerates loadable types from an assembly that implement <typeparamref name="TInterface"/>.
    /// </summary>
    /// <typeparam name="TInterface">The interface contract used to filter discovered types.</typeparam>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>Matching loadable types, or an empty sequence when <paramref name="assembly"/> is <see langword="null"/>.</returns>
    public static IEnumerable<Type> SafeGetTypes<TInterface>(this Assembly assembly)
    {
        return SafeGetTypes(assembly, typeof(TInterface));
    }

    /// <summary>
    /// Enumerates loadable types from the supplied assemblies that implement a specified interface.
    /// </summary>
    /// <param name="assemblies">The assemblies to inspect.</param>
    /// <param name="interface">The interface contract used to filter discovered types.</param>
    /// <returns>Matching loadable types, or an empty sequence when either argument is <see langword="null"/>.</returns>
    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies, Type @interface)
    {
        if (assemblies is null || @interface is null)
        {
            return [];
        }

        return assemblies.SelectMany(a => SafeGetTypes(a, @interface));
    }

    /// <summary>
    /// Enumerates loadable types from an assembly that implement a specified interface.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <param name="interface">The interface contract used to filter discovered types.</param>
    /// <returns>Matching loadable types, including partial results after a type-load failure.</returns>
    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly, Type @interface)
    {
        if (assembly is null || @interface is null)
        {
            return [];
        }

        try
        {
            return assembly.GetTypes().Where(t => t.ImplementsInterface(@interface));
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null && t.ImplementsInterface(@interface));
        }
    }

    /// <summary>
    /// Enumerates loadable types from the supplied assemblies that implement at least one specified interface.
    /// </summary>
    /// <param name="assemblies">The assemblies to inspect.</param>
    /// <param name="interfaces">The interface contracts used to filter discovered types.</param>
    /// <returns>Matching loadable types, or an empty sequence when no usable filter is supplied.</returns>
    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies, params Type[] interfaces)
    {
        if (assemblies is null || interfaces is null || interfaces.Length == 0)
        {
            return Array.Empty<Type>();
        }

        return assemblies.SelectMany(a => SafeGetTypes(a, interfaces));
    }

    /// <summary>
    /// Enumerates loadable types from an assembly that implement at least one specified interface.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <param name="interfaces">The interface contracts used to filter discovered types.</param>
    /// <returns>Matching loadable types, including partial results after a type-load failure.</returns>
    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly, params Type[] interfaces)
    {
        if (assembly is null || interfaces is null || interfaces.Length == 0)
        {
            return Array.Empty<Type>();
        }

        try
        {
            return assembly.GetTypes().Where(t => t.ImplementsAnyInterface(interfaces));
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null && t.ImplementsAnyInterface(interfaces));
        }
    }

    /// <summary>
    /// Reads a UTC-style build timestamp encoded in the assembly informational version metadata.
    /// </summary>
    /// <param name="assembly">The assembly whose informational version should be inspected.</param>
    /// <returns>
    /// The timestamp following a <c>+build</c> or <c>.build</c> suffix in <c>yyyyMMddHHmmss</c> format;
    /// otherwise, <see langword="default"/>.
    /// </returns>
    public static DateTime GetBuildDate(this Assembly assembly)
    {
        // origin: https://www.meziantou.net/2018/09/24/getting-the-date-of-build-of-a-net-assembly-at-runtime
        // note: project file needs to contain:
        //       <PropertyGroup><SourceRevisionId>build$([System.DateTime]::UtcNow.ToString("yyyyMMddHHmmss"))</SourceRevisionId></PropertyGroup>
        const string buildVersionMetadataPrefix1 = "+build";
        const string buildVersionMetadataPrefix2 = ".build"; // TODO: make this an array of allowable prefixes
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute?.InformationalVersion is not null)
        {
            var value = attribute.InformationalVersion;
            var prefix = buildVersionMetadataPrefix1;
            var index = value.IndexOf(buildVersionMetadataPrefix1, StringComparison.OrdinalIgnoreCase);
            // fallback for '.build' prefix
            if (index == -1)
            {
                prefix = buildVersionMetadataPrefix2;
                index = value.IndexOf(buildVersionMetadataPrefix2, StringComparison.OrdinalIgnoreCase);
            }

            if (index > 0)
            {
                value = value[(index + prefix.Length)..];
                if (DateTime.TryParseExact(value,
                        "yyyyMMddHHmmss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var result))
                {
                    return result;
                }
            }
        }

        return default;
    }
}
