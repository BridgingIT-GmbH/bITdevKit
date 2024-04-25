// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Reflection;

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
        var methodActivityAttributes = method.GetCustomAttribute<ActivityAttributesAttribute>(inherit: false);
        var classActivityAttributes = innerType.GetCustomAttribute<ActivityAttributesAttribute>(inherit: false);

        if (methodActivityAttributes != null)
        {
            foreach (var key in classActivityAttributes.Attributes.Keys)
            {
                activity.AddTag(key, methodActivityAttributes.Attributes[key]);
            }
        }

        if (classActivityAttributes != null)
        {
            foreach (var key in classActivityAttributes.Attributes.Keys)
            {
                activity.AddTag(key, methodActivityAttributes.Attributes[key]);
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