// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

public static class AssemblyExtensions
{
    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies)
    {
        if (assemblies is null)
        {
            return[];
        }

        return assemblies.SelectMany(SafeGetTypes);
    }

    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly)
    {
        if (assembly is null)
        {
            return[];
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

    public static IEnumerable<Type> SafeGetTypes<TInterface>(this IEnumerable<Assembly> assemblies)
    {
        return SafeGetTypes(assemblies, typeof(TInterface));
    }

    public static IEnumerable<Type> SafeGetTypes<TInterface>(this Assembly assembly)
    {
        return SafeGetTypes(assembly, typeof(TInterface));
    }

    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies, Type @interface)
    {
        if (assemblies is null || @interface is null)
        {
            return[];
        }

        return assemblies.SelectMany(t => SafeGetTypes(t, @interface));
    }

    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly, Type @interface)
    {
        if (assembly is null || @interface is null)
        {
            return[];
        }

        try
        {
            return assembly.GetTypes().Where(t => t != null && t.ImplementsInterface(@interface));
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null && t.ImplementsInterface(@interface));
        }
    }

    public static DateTime GetBuildDate(this Assembly assembly)
    {
        // origin: https://www.meziantou.net/2018/09/24/getting-the-date-of-build-of-a-net-assembly-at-runtime
        // note: project file needs to contain:
        //       <PropertyGroup><SourceRevisionId>build$([System.DateTime]::UtcNow.ToString("yyyyMMddHHmmss"))</SourceRevisionId></PropertyGroup>
        const string BuildVersionMetadataPrefix1 = "+build";
        const string BuildVersionMetadataPrefix2 = ".build"; // TODO: make this an array of allowable prefixes
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute?.InformationalVersion is not null)
        {
            var value = attribute.InformationalVersion;
            var prefix = BuildVersionMetadataPrefix1;
            var index = value.IndexOf(BuildVersionMetadataPrefix1, StringComparison.OrdinalIgnoreCase);
            // fallback for '.build' prefix
            if (index == -1)
            {
                prefix = BuildVersionMetadataPrefix2;
                index = value.IndexOf(BuildVersionMetadataPrefix2, StringComparison.OrdinalIgnoreCase);
            }

            if (index > 0)
            {
                value = value[(index + prefix.Length)..];
                if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }
        }

        return default;
    }
}