// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BridgingIT.DevKit.Common;

/// <summary>Shared JSON handling for value-object cells, editors, diffs and the JSON export.</summary>
internal static class EntitiesDashboardJson
{
    /// <summary>Gets the shared serializer options (camelCase, string enums, indented, cycles ignored, depth 32).</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AllowNonPublicMembers } }
    };

    // value objects commonly use private constructors and private setters, which System.Text.Json skips by default
    private static void AllowNonPublicMembers(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Type.IsAbstract || typeInfo.Type.IsInterface)
        {
            return;
        }

        if (typeInfo.CreateObject == null && typeInfo.Type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null)
        {
            var type = typeInfo.Type;
            typeInfo.CreateObject = () => Activator.CreateInstance(type, true);
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.Get is { } getter)
            {
                // computed getters (e.g. DateTimeRange.Duration) can throw for valid states, so such properties are skipped
                var propertyType = property.PropertyType;
                var existing = property.ShouldSerialize;
                property.Get = target =>
                {
                    try
                    {
                        return getter(target);
                    }
                    catch (Exception)
                    {
                        return propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
                    }
                };
                property.ShouldSerialize = (target, value) =>
                {
                    if (existing != null && !existing(target, value))
                    {
                        return false;
                    }

                    try
                    {
                        getter(target);

                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                };
            }

            if (property.Set == null &&
                property.AttributeProvider is PropertyInfo propertyInfo &&
                propertyInfo.GetSetMethod(true) is { } setter)
            {
                property.Set = (target, value) => setter.Invoke(target, [value]);
            }
        }
    }

    /// <summary>Serializes a value to indented JSON, or null for null.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(object value) =>
        value == null ? null : JsonSerializer.Serialize(value, value.GetType(), Options);

    /// <summary>Deserializes JSON text into a value of the given type.</summary>
    /// <param name="type">The target type.</param>
    /// <param name="json">The JSON text.</param>
    /// <param name="propertyName">The property name used in failure messages.</param>
    /// <returns>The value (null for the JSON literal <c>null</c>) or a failed result carrying the parser message.</returns>
    public static Result<object> Deserialize(Type type, string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<object>.Success(null);
        }

        try
        {
            return Result<object>.Success(JsonSerializer.Deserialize(json, type, Options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            var message = ex is JsonException ? $"The JSON is not valid: {ex.Message}" : "The JSON cannot be converted to this property.";

            return Result<object>.Failure(message, new ValidationError(message, propertyName, null));
        }
    }

    /// <summary>Parses JSON text into a node, or returns null when the text is not valid JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The element or null.</returns>
    public static JsonElement? TryParse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
