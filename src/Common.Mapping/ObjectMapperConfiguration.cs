// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Linq.Expressions;
using System.Reflection;

public class ObjectMapperConfiguration<TSource, TTarget>
{
    private readonly ObjectMapper objectMapper;
    private readonly List<PropertyMapping<TSource, TTarget>> propertyMappings = new List<PropertyMapping<TSource, TTarget>>();

    internal ObjectMapperConfiguration(ObjectMapper objectMapper)
    {
        this.objectMapper = objectMapper;
    }

    public ObjectMapperConfiguration<TSource, TTarget> Map(
        Expression<Func<TSource, object>> sourceProperty,
        Expression<Func<TTarget, object>> targetProperty)
    {
        var sourceMemberInfo = this.GetMemberInfo(sourceProperty);
        var targetMemberInfo = this.GetMemberInfo(targetProperty);

        this.propertyMappings.Add(new PropertyMapping<TSource, TTarget>(
            sourceMemberInfo,
            targetMemberInfo));

        return this;
    }

    public ObjectMapperConfiguration<TSource, TTarget> MapCustom(
        Func<TSource, object> sourceExpression,
        Expression<Func<TTarget, object>> targetProperty)
    {
        var targetMemberInfo = this.GetMemberInfo(targetProperty);

        this.propertyMappings.Add(new PropertyMapping<TSource, TTarget>(
            sourceExpression,
            targetMemberInfo));

        return this;
    }

    public ObjectMapper Apply()
    {
        this.objectMapper.AddMapping<TSource, TTarget>(this.CreateMapping);

        return this.objectMapper;
    }

    private MemberInfo GetMemberInfo(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambdaExpression => this.GetMemberInfo(lambdaExpression.Body),
            MemberExpression memberExpression => memberExpression.Member,
            UnaryExpression unaryExpression when unaryExpression.Operand is MemberExpression operand => operand.Member,
            _ => throw new ArgumentException("Invalid expression"),
        };
    }

    private TTarget CreateMapping(TSource source)
    {
        var target = Activator.CreateInstance<TTarget>();
        if (target is not null)
        {
            foreach (var propertyMapping in this.propertyMappings)
            {
                if (propertyMapping.SourceExpression is not null)
                {
                    var sourceValue = propertyMapping.SourceExpression(source);
                    this.SetPropertyValue(target, propertyMapping.TargetMemberInfo.Name, sourceValue);
                }
                else
                {
                    var sourceValue = this.GetPropertyValue(source, propertyMapping.SourceMemberInfo);
                    this.SetPropertyValue(target, propertyMapping.TargetMemberInfo.Name, sourceValue);
                }
            }
        }

        return target;
    }

    private object GetPropertyValue(object obj, MemberInfo memberInfo)
    {
        if (memberInfo is PropertyInfo propertyInfo)
        {
            return propertyInfo.GetValue(obj);
        }
        else if (memberInfo is FieldInfo fieldInfo)
        {
            return fieldInfo.GetValue(obj);
        }
        else
        {
            throw new ArgumentException("Invalid member type");
        }
    }

    private void SetPropertyValue(object obj, string propertyName, object value)
    {
        var propertyInfo = typeof(TTarget).GetProperty(propertyName);
        if (propertyInfo is not null)
        {
            propertyInfo.SetValue(obj, value);
        }
        else
        {
            throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(TTarget)}'");
        }
    }
}

internal class PropertyMapping<TSource, TTarget>
{
    public PropertyMapping(MemberInfo sourceMemberInfo, MemberInfo targetMemberInfo)
    {
        this.SourceMemberInfo = sourceMemberInfo;
        this.TargetMemberInfo = targetMemberInfo;
    }

    public PropertyMapping(Func<TSource, object> sourceExpression, MemberInfo targetMemberInfo)
    {
        this.SourceExpression = sourceExpression;
        this.TargetMemberInfo = targetMemberInfo;
    }

    public MemberInfo SourceMemberInfo { get; }

    public Func<TSource, object> SourceExpression { get; }

    public MemberInfo TargetMemberInfo { get; }
}