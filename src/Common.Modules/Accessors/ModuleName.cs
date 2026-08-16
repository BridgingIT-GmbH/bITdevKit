// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

/// <summary>
///     Reads a module name from an assembly-level <see cref="Attribute"/>.
/// </summary>
public static class ModuleName
{
    /// <summary>
    ///     Gets the module name attached to the assembly containing <typeparamref name="TType"/>.
    /// </summary>
    /// <typeparam name="TType">A type from the module assembly.</typeparam>
    /// <param name="throwIfNotFound">Whether to throw when the assembly has no module-name attribute.</param>
    /// <returns>The module name, or <see langword="null"/> when it is absent and throwing is disabled.</returns>
    /// <exception cref="Exception">No module name exists and <paramref name="throwIfNotFound"/> is <see langword="true"/>.</exception>
    public static string From<TType>(bool throwIfNotFound = true)
    {
        var value = typeof(TType).Assembly.GetCustomAttribute<Attribute>()
            ?.Value; // TODO: cache this value lookup for better perf?

        if (string.IsNullOrEmpty(value) && throwIfNotFound)
        {
            throw new Exception(
                $"ModuleName property not found on assembly {typeof(TType).Assembly.GetName()}. Please add the following property '<ModuleName>NAME</ModuleName>' inside a '<PropertyGroup>'.");

            // also add the following to propagate the property as an AssemblyAttribute
            //<ItemGroup>
            //  <AssemblyAttribute Include="BridgingIT.DevKit.Common.ModuleName.Attribute">
            //    <_Parameter1>"$(ModuleName)"</_Parameter1>
            //  </AssemblyAttribute>
            //</ItemGroup>
        }

        return value;
    }

    /// <summary>
    ///     Gets the module name attached to the assembly containing a specified type.
    /// </summary>
    /// <param name="type">A type from the module assembly.</param>
    /// <param name="throwIfNotFound">Whether to throw when the assembly has no module-name attribute.</param>
    /// <returns>The module name, or <see langword="null"/> when it is absent and throwing is disabled.</returns>
    /// <exception cref="Exception">No module name exists and <paramref name="throwIfNotFound"/> is <see langword="true"/>.</exception>
    public static string From(Type type, bool throwIfNotFound = true)
    {
        var value = type.Assembly.GetCustomAttribute<Attribute>()
            ?.Value; // TODO: cache this value lookup for better perf?

        if (string.IsNullOrEmpty(value) && throwIfNotFound)
        {
            throw new Exception(
                $"ModuleName property not found on assembly {type.Assembly.GetName()}. Please add the following property '<ModuleName>NAME</ModuleName>' inside a '<PropertyGroup>'.");

            // also add the following to propagate the property as an AssemblyAttribute
            //<ItemGroup>
            //  <AssemblyAttribute Include="BridgingIT.DevKit.Common.ModuleName.Attribute">
            //    <_Parameter1>"$(ModuleName)"</_Parameter1>
            //  </AssemblyAttribute>
            //</ItemGroup>
        }

        return value;
    }

    /// <summary>
    ///     Associates a module name with an assembly.
    /// </summary>
    /// <param name="value">The module name, optionally surrounded by quotation marks.</param>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class Attribute(string value) : System.Attribute
    {
        private readonly string value = value;

        /// <summary>
        ///     Gets the module name with surrounding quotation marks removed.
        /// </summary>
        public string Value => this.value.Trim('"');
    }
}
