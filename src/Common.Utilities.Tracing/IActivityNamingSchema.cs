// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

/// <summary>
///     Defines how traced method activities are named.
/// </summary>
public interface IActivityNamingSchema
{
    /// <summary>
    ///     Creates an activity name for a method invoked on a runtime type.
    /// </summary>
    /// <param name="type">The runtime implementation type.</param>
    /// <param name="method">The invoked interface method.</param>
    /// <returns>The activity name.</returns>
    public string GetName(Type type, MethodInfo method);
}

/// <summary>
///     Names activities with the runtime type's full name followed by the method name.
/// </summary>
public class MethodFullNameSchema : IActivityNamingSchema
{
    /// <inheritdoc/>
    public string GetName(Type type, MethodInfo method)
    {
        return $"{type.FullName}.{method.Name}";
    }
}

/// <summary>
///     Names activities with the runtime type's short name followed by the method name.
/// </summary>
public class ClassAndMethodNameSchema : IActivityNamingSchema
{
    /// <inheritdoc/>
    public string GetName(Type type, MethodInfo method)
    {
        return $"{type.Name}.{method.Name}";
    }
}
