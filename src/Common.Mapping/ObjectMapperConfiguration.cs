// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
///     Configures object mapping between source and target types.
/// </summary>
/// <typeparam name="TSource">The source type to map from.</typeparam>
/// <typeparam name="TTarget">The target type to map to.</typeparam>
public class ObjectMapperConfiguration<TSource, TTarget>
{
    /// <summary>
    ///     The <see cref="ObjectMapper" /> instance responsible for managing object mappings and transformations
    ///     between different types within the application.
    /// </summary>
    /// <remarks>
    ///     Offers functionalities to define, configure, and apply mappings between source and target types.
    ///     This variable is utilized to execute mapping operations as per the configured rules.
    /// </remarks>
    private readonly ObjectMapper objectMapper;

    /// <summary>
    ///     Represents a collection of property mappings between a source and a target type.
    /// </summary>
    private readonly List<PropertyMapping<TSource, TTarget>> propertyMappings = [];

    /// <summary>
    ///     Provides configuration options for mapping properties between source type <typeparamref name="TSource" /> and
    ///     target type <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">Source type for the mapping configuration.</typeparam>
    /// <typeparam name="TTarget">Target type for the mapping configuration.</typeparam>
    internal ObjectMapperConfiguration(ObjectMapper objectMapper)
    {
        this.objectMapper = objectMapper;
    }

    /// <summary>
    ///     Maps a source property to a target property.
    /// </summary>
    /// <param name="sourceProperty">An expression representing the source property to map from.</param>
    /// <param name="targetProperty">An expression representing the target property to map to.</param>
    /// <returns>An instance of <see cref="ObjectMapperConfiguration{TSource, TTarget}" /> to allow for method chaining.</returns>
    public ObjectMapperConfiguration<TSource, TTarget> Map(
        Expression<Func<TSource, object>> sourceProperty,
        Expression<Func<TTarget, object>> targetProperty)
    {
        var sourceMemberInfo = this.GetMemberInfo(sourceProperty);
        var targetMemberInfo = this.GetMemberInfo(targetProperty);

        this.propertyMappings.Add(new PropertyMapping<TSource, TTarget>(sourceMemberInfo, targetMemberInfo));

        return this;
    }

    /// <summary>
    ///     Configures a custom mapping between the source and target properties.
    /// </summary>
    /// <param name="sourceExpression">A function that specifies the custom source property or value.</param>
    /// <param name="targetProperty">An expression that specifies the target property.</param>
    /// <returns>
    ///     The updated <see cref="ObjectMapperConfiguration{TSource, TTarget}" /> instance with the custom mapping
    ///     applied.
    /// </returns>
    public ObjectMapperConfiguration<TSource, TTarget> MapCustom(
        Func<TSource, object> sourceExpression,
        Expression<Func<TTarget, object>> targetProperty)
    {
        var targetMemberInfo = this.GetMemberInfo(targetProperty);

        this.propertyMappings.Add(new PropertyMapping<TSource, TTarget>(sourceExpression, targetMemberInfo));

        return this;
    }

    /// <summary>
    ///     Applies the current mapping configuration and registers the mappings with the ObjectMapper.
    /// </summary>
    /// <returns>
    ///     The configured ObjectMapper with the applied mappings.
    /// </returns>
    public ObjectMapper Apply()
    {
        this.objectMapper.AddMapping<TSource, TTarget>(this.CreateMapping);
        this.objectMapper.AddMapping<TSource, TTarget>(this.UpdateMapping);

        return this.objectMapper;
    }

    /// <summary>
    ///     Extracts the <see cref="MemberInfo" /> from the given expression.
    /// </summary>
    /// <param name="expression">The expression to retrieve member information from.</param>
    /// <returns>The <see cref="MemberInfo" /> extracted from the expression.</returns>
    /// <exception cref="ArgumentException">Thrown when the expression is not valid for extracting member information.</exception>
    private MemberInfo GetMemberInfo(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambdaExpression => this.GetMemberInfo(lambdaExpression.Body),
            MemberExpression memberExpression => memberExpression.Member,
            UnaryExpression unaryExpression when unaryExpression.Operand is MemberExpression operand => operand.Member,
            _ => throw new ArgumentException("Invalid expression")
        };
    }

    /// <summary>
    ///     Creates a new instance of TTarget and populates it with values from the specified source object.
    /// </summary>
    /// <param name="source">The source object from which to map values.</param>
    /// <returns>A new instance of TTarget with mapped values.</returns>
    private TTarget CreateMapping(TSource source)
    {
        var target = Activator.CreateInstance<TTarget>();
        this.UpdateMapping(source, target);
        return target;
    }

    /// <summary>
    ///     Updates the target object with values from the source object based on the configured property mappings.
    /// </summary>
    /// <param name="source">The source object from which values are obtained.</param>
    /// <param name="target">The target object on which values are set.</param>
    private void UpdateMapping(TSource source, TTarget target)
    {
        if (target is null)
        {
            return;
        }

        foreach (var propertyMapping in this.propertyMappings)
        {
            if (propertyMapping.SourceExpression is not null)
            {
                var sourceValue = propertyMapping.SourceExpression(source);
                this.SetPropertyValue(target, propertyMapping.Target.Name, sourceValue);
            }
            else
            {
                var sourceValue = this.GetPropertyValue(source, propertyMapping.Source);
                this.SetPropertyValue(target, propertyMapping.Target.Name, sourceValue);
            }
        }
    }

    /// <summary>
    ///     Retrieves the value of a specified property or field from an object.
    /// </summary>
    /// <param name="obj">The object from which to retrieve the value.</param>
    /// <param name="memberInfo">The metadata information of the property or field to retrieve.</param>
    /// <returns>The value of the specified property or field.</returns>
    /// <exception cref="ArgumentException">Thrown when the memberInfo is not of type PropertyInfo or FieldInfo.</exception>
    private object GetPropertyValue(object obj, MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo propertyInfo => propertyInfo.GetValue(obj),
            FieldInfo fieldInfo => fieldInfo.GetValue(obj),
            _ => throw new ArgumentException("Invalid member type")
        };
    }

    /// <summary>
    ///     Sets the value of a specified property on the target object.
    /// </summary>
    /// <param name="obj">The target object on which the property value will be set.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set on the specified property.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when the property with the specified name is not found on the target
    ///     object's type.
    /// </exception>
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

/// <summary>
///     Represents a mapping between properties of source and target objects in the object mapping configuration.
/// </summary>
/// <typeparam name="TSource">The type of the source object.</typeparam>
/// <typeparam name="TTarget">The type of the target object.</typeparam>
internal class PropertyMapping<TSource, TTarget>(MemberInfo source, MemberInfo target)
{
    /// <summary>
    ///     Represents a mapping configuration between properties of the source and target types.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    public PropertyMapping(Func<TSource, object> sourceExpression, MemberInfo target)
        : this(default(MemberInfo), target)
    {
        this.SourceExpression = sourceExpression;
    }

    /// <summary>
    ///     Gets the source member information for the property mapping.
    /// </summary>
    /// <remarks>
    ///     This property holds metadata about the source property, such as its name,
    ///     type, and other reflection-related information, which is used in the object
    ///     mapping process to map values from the source object to the target object.
    /// </remarks>
    public MemberInfo Source { get; } = source;

    /// <summary>
    ///     Gets the function used to convert the source object to the required format for mapping to the target object.
    ///     This expression is utilized when a custom mapping logic is specified for the source property.
    /// </summary>
    public Func<TSource, object> SourceExpression { get; }

    /// <summary>
    ///     Gets the target member information that represents the property or field
    ///     in the target object to which the source object's property or field will
    ///     be mapped.
    /// </summary>
    public MemberInfo Target { get; } = target;
}