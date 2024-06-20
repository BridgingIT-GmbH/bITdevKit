// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

public static class ModuleName
{
    public static string From<TType>(bool throwIfNotFound = true)
    {
        var value = typeof(TType).Assembly.GetCustomAttribute<Attribute>()?.Value; // TODO: cache this value lookup for better perf?

        if (string.IsNullOrEmpty(value) && throwIfNotFound)
        {
            throw new Exception($"ModuleName property not found on assembly {typeof(TType).Assembly.GetName()}. Please add the following property '<ModuleName>NAME</ModuleName>' inside a '<PropertyGroup>'.");

            // also add the following to propagate the property as an AssemblyAttribute
            //<ItemGroup>
            //  <AssemblyAttribute Include="BridgingIT.DevKit.Common.ModuleName.Attribute">
            //    <_Parameter1>"$(ModuleName)"</_Parameter1>
            //  </AssemblyAttribute>
            //</ItemGroup>
        }

        return value;
    }

    public static string From(Type type, bool throwIfNotFound = true)
    {
        var value = type.Assembly.GetCustomAttribute<Attribute>()?.Value; // TODO: cache this value lookup for better perf?

        if (string.IsNullOrEmpty(value) && throwIfNotFound)
        {
            throw new Exception($"ModuleName property not found on assembly {type.Assembly.GetName()}. Please add the following property '<ModuleName>NAME</ModuleName>' inside a '<PropertyGroup>'.");

            // also add the following to propagate the property as an AssemblyAttribute
            //<ItemGroup>
            //  <AssemblyAttribute Include="BridgingIT.DevKit.Common.ModuleName.Attribute">
            //    <_Parameter1>"$(ModuleName)"</_Parameter1>
            //  </AssemblyAttribute>
            //</ItemGroup>
        }

        return value;
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public class Attribute(string value) : System.Attribute
    {
        private readonly string value = value;

        public string Value => this.value.Trim('"');
    }
}