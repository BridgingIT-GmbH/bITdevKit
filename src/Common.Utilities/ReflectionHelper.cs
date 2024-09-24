// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using System.Reflection.Emit;

/// <summary>
///     Provides helper methods for reflecting and manipulating object properties at runtime.
/// </summary>
public static class ReflectionHelper
{
    /// <summary>
    ///     Sets the properties of the specified instance with the values provided in the propertyValues dictionary.
    /// </summary>
    /// <param name="instance">The object instance whose properties need to be set.</param>
    /// <param name="propertyValues">A dictionary containing property names and their corresponding values.</param>
    public static void SetProperties(object instance, IDictionary<string, object> propertyValues)
    {
        if (instance is null || propertyValues.IsNullOrEmpty())
        {
            return;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        foreach (var propertyInfo in instance.GetType()
                     .GetProperties(BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Instance |
                         BindingFlags.IgnoreCase)
                     .SafeNull())
        {
            foreach (var propertyValue in propertyValues.SafeNull())
            {
                if (propertyValue.Key.SafeEquals(propertyInfo.Name) /*&& propertyValue.Value is not null*/ &&
                    propertyInfo.CanWrite)
                {
                    var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ??
                        propertyInfo.PropertyType;
                    propertyInfo.SetValue(instance, propertyValue.Value.To(propertyType), null);
                }
            }
        }
    }

    /// <summary>
    ///     Sets the value of a specified property on an instance of a given object. The property can be private, public,
    ///     static, or instance-based.
    /// </summary>
    /// <param name="instance">The object instance on which the property value will be set.</param>
    /// <param name="propertyName">The name of the property to set. Case is ignored.</param>
    /// <param name="propertyValue">The value to assign to the property.</param>
    public static void SetProperty(object instance, string propertyName, object propertyValue)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType()
            .GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            if (propertyName.SafeEquals(propertyInfo.Name) /*&& propertyValue is not null*/ && propertyInfo.CanWrite)
            {
                var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                propertyInfo.SetValue(instance, propertyValue.To(propertyType), null);
            }
        }
    }

    //public static void SetProperty2(object instance, string propertyName, object propertyValue)
    //{
    //    if (instance is null || string.IsNullOrEmpty(propertyName))
    //    {
    //        return;
    //    }

    //    var type = instance.GetType();
    //    while (type != null)
    //    {
    //        var propertyInfo = type.GetProperty(propertyName,
    //            BindingFlags.Public | BindingFlags.NonPublic |
    //            BindingFlags.Instance | BindingFlags.DeclaredOnly);

    //        if (propertyInfo != null)
    //        {
    //            var setMethod = type.GetMethod("set_" + propertyName,
    //                BindingFlags.Public | BindingFlags.NonPublic |
    //                BindingFlags.Instance | BindingFlags.DeclaredOnly);

    //            if (setMethod != null)
    //            {
    //                var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
    //                var convertedValue = Convert.ChangeType(propertyValue, propertyType);
    //                setMethod.Invoke(instance, new[] { convertedValue });
    //                return;
    //            }
    //        }

    //        type = type.BaseType;
    //    }
    //}

    /// <summary>
    ///     Retrieves the value of a specified property from a given instance.
    /// </summary>
    /// <param name="instance">The object instance from which the property value is to be retrieved.</param>
    /// <param name="propertyName">The name of the property whose value is to be retrieved.</param>
    /// <returns>The value of the property if found; otherwise, the default value for the property type.</returns>
    public static object GetProperty(object instance, string propertyName)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return default;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType()
            .GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is not null)
        {
            if (propertyName.SafeEquals(propertyInfo.Name) /*&& propertyValue is not null*/ && propertyInfo.CanRead)
            {
                return propertyInfo.GetValue(instance);
            }
        }

        return default;
    }

    /// <summary>
    ///     Retrieves the property value of the specified property name from the given instance.
    /// </summary>
    /// <typeparam name="TType">The type of the property value to be returned.</typeparam>
    /// <param name="instance">The instance from which the property value is retrieved.</param>
    /// <param name="propertyName">The name of the property whose value is to be retrieved.</param>
    /// <returns>The value of the property if found; otherwise, the default value of the specified type.</returns>
    public static TType GetProperty<TType>(object instance, string propertyName)
    {
        if (instance is null || propertyName.IsNullOrEmpty())
        {
            return default;
        }

        // or use https://github.com/ekonbenefits/dynamitey/wiki/UsageReallyLateBinding DLR dynamic InvokeSetAll(object target, ...) =CASESENSITIVE
        var propertyInfo = instance.GetType()
            .GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

    /// <summary>
    ///     Creates a getter function for accessing the value of a specified field.
    /// </summary>
    /// <typeparam name="TParam">The type of the object containing the field.</typeparam>
    /// <typeparam name="TReturn">The type of the field value.</typeparam>
    /// <param name="field">The FieldInfo object representing the field for which the getter function is created.</param>
    /// <returns>A function that takes an instance of type TParam and returns the value of the field of type TReturn.</returns>
    public static Func<TParam, TReturn> CreateGetter<TParam, TReturn>(FieldInfo field)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(field.ReflectedType);

        var methodName = $"{field.ReflectedType.FullName}.get_{field.Name}";
        var method = new DynamicMethod(methodName, typeof(TReturn), [typeof(TParam)], typeof(TParam), true);
        var ilGen = method.GetILGenerator();
        ilGen.Emit(OpCodes.Ldarg_0);
        ilGen.Emit(OpCodes.Ldfld, field);
        ilGen.Emit(OpCodes.Ret);

        return (Func<TParam, TReturn>)method.CreateDelegate(typeof(Func<TParam, TReturn>));
    }

    /// <summary>
    ///     Finds and returns all types from the specified assemblies that match the provided predicate.
    /// </summary>
    /// <param name="predicate">A function to test each type for a condition.</param>
    /// <param name="assemblies">
    ///     An optional array of assemblies to search for types. If not specified, all assemblies in the
    ///     current AppDomain are searched.
    /// </param>
    /// <returns>An enumerable collection of types that match the specified predicate.</returns>
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
            if (assembly.IsDynamic)
            {
                continue;
            }

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
                if (predicate != null && predicate(type))
                {
                    yield return type;
                }
            }
        }
    }
}