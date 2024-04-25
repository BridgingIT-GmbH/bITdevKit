// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

public static class ReflectionHelper
{
    public static void SetProperties(object instance, IDictionary<string, object> propertyValues)
    {
        if (instance is null || propertyValues.IsNullOrEmpty())
        {
            return;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        foreach (var propertyInfo in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).SafeNull())
        {
            foreach (var propertyValue in propertyValues.SafeNull())
            {
                if (propertyValue.Key.SafeEquals(propertyInfo.Name) /*&& propertyValue.Value is not null*/ && propertyInfo.CanWrite)
                {
                    var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    propertyInfo.SetValue(instance, propertyValue.Value.To(propertyType), null);
                }
            }
        }
    }

    public static void SetProperty(object instance, string propertyName, object propertyValue)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            if (propertyName.SafeEquals(propertyInfo.Name) /*&& propertyValue is not null*/ && propertyInfo.CanWrite)
            {
                var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                propertyInfo.SetValue(instance, propertyValue.To(propertyType), null);
            }
        }
    }

    public static object GetProperty(object instance, string propertyName)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return default;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            if (propertyName.SafeEquals(propertyInfo.Name) /*&& propertyValue is not null*/ && propertyInfo.CanRead)
            {
                return propertyInfo.GetValue(instance);
            }
        }

        return default;
    }

    public static TType GetProperty<TType>(object instance, string propertyName)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return default;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            if (propertyName.SafeEquals(propertyInfo.Name) /*&& propertyValue is not null*/ && propertyInfo.CanRead)
            {
                var propertyValue = propertyInfo.GetValue(instance);
                return propertyValue.To<TType>();
            }
        }

        return default;
    }

    public static Func<TParam, TReturn> CreateGetter<TParam, TReturn>(FieldInfo field)
    {
        var methodName = $"{field.ReflectedType.FullName}.get_{field.Name}";
        var method = new DynamicMethod(methodName, typeof(TReturn), new[] { typeof(TParam) }, typeof(TParam), true);
        var ilGen = method.GetILGenerator();
        ilGen.Emit(OpCodes.Ldarg_0);
        ilGen.Emit(OpCodes.Ldfld, field);
        ilGen.Emit(OpCodes.Ret);

        return (Func<TParam, TReturn>)method.CreateDelegate(typeof(Func<TParam, TReturn>));
    }

    public static IEnumerable<Type> FindTypes(Func<Type, bool> predicate, params Assembly[] assemblies)
    {
        if (predicate is null)
        {
            yield return null;
        }

        if (assemblies.IsNullOrEmpty())
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        foreach (var assembly in assemblies.SafeNull())
        {
            if (!assembly.IsDynamic)
            {
                Type[] types = null;
                try
                {
                    types = assembly.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch (TypeLoadException)
                {
                    // skip
                }

                foreach (var type in types.SafeNull())
                {
                    if (predicate(type))
                    {
                        yield return type;
                    }
                }
            }
        }
    }
}