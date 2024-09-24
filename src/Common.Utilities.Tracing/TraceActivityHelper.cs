// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Reflection;

/// <summary>
///     A helper class to manage and add diagnostic trace activity tags related to methods in a .NET application.
/// </summary>
/// <remarks>
///     This class provides methods to add tags that describe method characteristics to an Activity object,
///     enhancing the traceability and debuggability of methods executions.
/// </remarks>
public static class TraceActivityHelper
{
    public static void AddMethodTags(Activity activity, MethodInfo method)
    {
        activity?.AddTag("code.namespace", method.DeclaringType?.ToString());
        activity?.AddTag("code.function", method.Name);
        activity?.AddTag("code.function.parameters", method.ToParametersString());
    }

    public static void AddAttributeTags(Activity activity, MethodInfo method, Type innerType)
    {
        var methodActivityAttributes = method.GetCustomAttribute<ActivityAttributesAttribute>(false);
        var classActivityAttributes = innerType.GetCustomAttribute<ActivityAttributesAttribute>(false);

        if (methodActivityAttributes != null)
        {
            foreach (var key in methodActivityAttributes.Attributes.Keys)
            {
                activity.AddTag(key, methodActivityAttributes.Attributes[key]);
            }
        }

        if (classActivityAttributes != null)
        {
            foreach (var key in classActivityAttributes.Attributes.Keys)
            {
                activity.AddTag(key, classActivityAttributes.Attributes[key]);
            }
        }
    }

    private static string ToParametersString(this MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length < 1)
        {
            return string.Empty;
        }

        return string.Join('|', parameters.Select(p => p.ParameterType.Name));
    }
}