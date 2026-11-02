// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using BridgingIT.DevKit.Common;

/// <summary>Converts entity ids between their typed or primitive form and invariant text.</summary>
internal static class EntitiesDashboardIdConverter
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> Creators = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ValueReaders = new();

    /// <summary>Gets the primitive type behind an id type (typed ids are unwrapped through their <c>Value</c> property).</summary>
    /// <param name="idType">The id type.</param>
    /// <returns>The primitive id type.</returns>
    public static Type GetPrimitiveType(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        var type = EntitiesDashboardTypeInfo.Unwrap(idType);

        return EntitiesDashboardTypeInfo.IsTypedId(type, out var primitive)
            ? EntitiesDashboardTypeInfo.Unwrap(primitive)
            : type;
    }

    /// <summary>Parses id text into an instance of the id type.</summary>
    /// <param name="idType">The id type (primitive or typed id).</param>
    /// <param name="text">The id text.</param>
    /// <returns>The parsed id, or a failed result with a validation message.</returns>
    public static Result<object> Parse(Type idType, string text)
    {
        ArgumentNullException.ThrowIfNull(idType);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("An id is required.", text);
        }

        text = text.Trim();
        var primitiveType = GetPrimitiveType(idType);

        object primitive;
        try
        {
            primitive = ParsePrimitive(primitiveType, text);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            primitive = null;
        }

        if (primitive == null)
        {
            return Invalid($"'{text}' is not a valid {primitiveType.Name} id.", text);
        }

        var type = EntitiesDashboardTypeInfo.Unwrap(idType);
        if (!EntitiesDashboardTypeInfo.IsTypedId(type, out _))
        {
            return Result<object>.Success(primitive);
        }

        var creator = Creators.GetOrAdd(type, BuildCreator);
        if (creator == null)
        {
            return Invalid($"The id type {type.Name} cannot be created from text.", text);
        }

        try
        {
            return Result<object>.Success(creator(primitive));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            return Invalid($"'{text}' is not a valid {type.Name}.", text);
        }
    }

    /// <summary>Formats an id as invariant primitive text.</summary>
    /// <param name="id">The id (primitive or typed id).</param>
    /// <returns>The invariant text, or an empty string for null.</returns>
    public static string Format(object id)
    {
        if (id == null)
        {
            return string.Empty;
        }

        var value = id;
        var type = id.GetType();
        if (EntitiesDashboardTypeInfo.IsTypedId(type, out _))
        {
            var reader = ValueReaders.GetOrAdd(type, BuildValueReader);
            value = reader(id);
        }

        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    /// <summary>Creates a new <see cref="Guid"/> based id when the primitive id type is a <see cref="Guid"/>.</summary>
    /// <param name="idType">The id type.</param>
    /// <returns>A new id of the id type, or null when the primitive type is not a <see cref="Guid"/>.</returns>
    public static object NewGuidIfEmpty(Type idType)
    {
        if (GetPrimitiveType(idType) != typeof(Guid))
        {
            return null;
        }

        var guid = Guid.NewGuid();
        var type = EntitiesDashboardTypeInfo.Unwrap(idType);
        if (!EntitiesDashboardTypeInfo.IsTypedId(type, out _))
        {
            return guid;
        }

        return Creators.GetOrAdd(type, BuildCreator)?.Invoke(guid);
    }

    private static object ParsePrimitive(Type primitiveType, string text)
    {
        if (primitiveType == typeof(Guid))
        {
            return Guid.TryParse(text, out var guid) ? guid : null;
        }

        if (primitiveType == typeof(string))
        {
            return text;
        }

        if (primitiveType.IsEnum)
        {
            return Enum.TryParse(primitiveType, text, true, out var value) ? value : null;
        }

        return Convert.ChangeType(text, primitiveType, CultureInfo.InvariantCulture);
    }

    private static Func<object, object> BuildCreator(Type typedIdType)
    {
        EntitiesDashboardTypeInfo.IsTypedId(typedIdType, out var primitive);
        primitive = EntitiesDashboardTypeInfo.Unwrap(primitive);
        var parameter = Expression.Parameter(typeof(object), "value");

        var factory = typedIdType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Create"
                && m.ReturnType != typeof(void)
                && typedIdType.IsAssignableFrom(m.ReturnType)
                && m.GetParameters() is [var p]
                && p.ParameterType == primitive);

        Expression body = null;
        if (factory != null)
        {
            body = Expression.Call(factory, Expression.Convert(parameter, primitive));
        }
        else
        {
            var constructor = typedIdType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters() is [var p] && p.ParameterType == primitive);
            if (constructor != null)
            {
                body = Expression.New(constructor, Expression.Convert(parameter, primitive));
            }
        }

        return body == null
            ? null
            : Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), parameter).Compile();
    }

    private static Func<object, object> BuildValueReader(Type typedIdType)
    {
        var property = typedIdType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        var parameter = Expression.Parameter(typeof(object), "id");
        var body = Expression.Convert(
            Expression.Property(Expression.Convert(parameter, typedIdType), property),
            typeof(object));

        return Expression.Lambda<Func<object, object>>(body, parameter).Compile();
    }

    private static Result<object> Invalid(string message, string attemptedValue) =>
        Result<object>.Failure(message, new ValidationError(message, "Id", attemptedValue));
}
