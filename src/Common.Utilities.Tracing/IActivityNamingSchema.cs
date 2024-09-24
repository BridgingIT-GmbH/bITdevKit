// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

public interface IActivityNamingSchema
{
    public string GetName(Type type, MethodInfo method);
}

public class MethodFullNameSchema : IActivityNamingSchema
{
    public string GetName(Type type, MethodInfo method)
    {
        return $"{type.FullName}.{method.Name}";
    }
}

public class ClassAndMethodNameSchema : IActivityNamingSchema
{
    public string GetName(Type type, MethodInfo method)
    {
        return $"{type.Name}.{method.Name}";
    }
}