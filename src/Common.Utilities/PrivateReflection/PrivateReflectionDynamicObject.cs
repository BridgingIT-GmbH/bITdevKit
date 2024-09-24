// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.PrivateReflection;

using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;

public class PrivateReflectionDynamicObject : DynamicObject
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly IDictionary<Type, IDictionary<string, IProperty>> PropertiesOnType =
        new ConcurrentDictionary<Type, IDictionary<string, IProperty>>();

    private object RealObject { get; set; }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        var prop = this.GetProperty(binder.Name);
        result = prop.GetValue(this.RealObject, null);
        result = WrapObjectIfNeeded(result);
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        var prop = this.GetProperty(binder.Name);
        prop.SetValue(this.RealObject, value, null);
        return true;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
    {
        var prop = this.GetIndexProperty();
        result = prop.GetValue(this.RealObject, indexes);
        result = WrapObjectIfNeeded(result);
        return true;
    }

    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
    {
        var prop = this.GetIndexProperty();
        prop.SetValue(this.RealObject, value, indexes);
        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
        result = InvokeMemberOnType(this.RealObject.GetType(), this.RealObject, binder.Name, args);
        result = WrapObjectIfNeeded(result);
        return true;
    }

    public override bool TryConvert(ConvertBinder binder, out object result)
    {
        result = Convert.ChangeType(this.RealObject, binder.Type);
        return true;
    }

    public override string ToString()
    {
        return this.RealObject.ToString();
    }

    internal static object WrapObjectIfNeeded(object o)
    {
        if (o is null || o.GetType().IsPrimitive || o is string)
        {
            return o;
        }

        return new PrivateReflectionDynamicObject { RealObject = o };
    }

    private static IDictionary<string, IProperty> GetTypeProperties(Type type)
    {
        if (PropertiesOnType.TryGetValue(type, out var typeProperties))
        {
            return typeProperties;
        }

        typeProperties = new ConcurrentDictionary<string, IProperty>();

        foreach (var prop in type.GetProperties(Flags).Where(p => p.DeclaringType == type))
        {
            typeProperties[prop.Name] = new Property { PropertyInfo = prop };
        }

        foreach (var field in type.GetFields(Flags).Where(p => p.DeclaringType == type))
        {
            typeProperties[field.Name] = new Field { FieldInfo = field };
        }

        if (type.BaseType is not null)
        {
            foreach (var prop in GetTypeProperties(type.BaseType).Values)
            {
                typeProperties[prop.Name] = prop;
            }
        }

        PropertiesOnType[type] = typeProperties;

        return typeProperties;
    }

    private static object InvokeMemberOnType(Type type, object target, string name, object[] args)
    {
        try
        {
            return type.InvokeMember(name, BindingFlags.InvokeMethod | Flags, null, target, args);
        }
        catch (MissingMethodException)
        {
            if (type.BaseType is not null)
            {
                return InvokeMemberOnType(type.BaseType, target, name, args);
            }

            throw new PrivateReflectionMethodNotFoundException(
                $"Method {name} with parameters '{string.Join(",", args)}' not found at instance of type {target.GetType().FullName}");
        }
    }

    private IProperty GetIndexProperty()
    {
        return this.GetProperty("Item");
    }

    private IProperty GetProperty(string propertyName)
    {
        var typeProperties = GetTypeProperties(this.RealObject.GetType());
        if (typeProperties.TryGetValue(propertyName, out var property))
        {
            return property;
        }

        var propNames = typeProperties.Keys.Where(name => name[0] != '<').OrderBy(name => name);
        throw new ArgumentException(
            $"The property {propertyName} doesn't exist on type {this.RealObject.GetType()}. Supported properties are: {string.Join(", ", propNames)}");
    }
}