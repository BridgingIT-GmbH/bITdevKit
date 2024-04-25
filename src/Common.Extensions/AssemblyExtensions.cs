// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Linq;
using System.Reflection;

public static class AssemblyExtensions
{
    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly)
    {
        if (assembly == null)
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

    public static IEnumerable<Type> SafeGetTypes(this Assembly assembly, Type @interface)
    {
        if (assembly == null)
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

    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null)
        {
            return[];
        }

        return assemblies.SelectMany(t => SafeGetTypes(t));
    }

    public static IEnumerable<Type> SafeGetTypes(this IEnumerable<Assembly> assemblies, Type @interface)
    {
        if (assemblies == null)
        {
            return[];
        }

        return assemblies.SelectMany(t => SafeGetTypes(t, @interface));
    }
}