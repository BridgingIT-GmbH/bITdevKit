// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// A custom <see cref="DefaultJsonTypeInfoResolver"/> implementation that provides universal support for deserializing
/// types with public or private constructors and properties with public or private setters, including inherited properties.
/// This resolver dynamically configures object creation and property setters using reflection to handle various accessibility
/// scenarios, making it suitable for immutable or complex object graphs without requiring attributes like [JsonInclude]
/// or [JsonConstructor].
/// </summary>
/// <remarks>
/// <para>This resolver is designed to work with <see cref="System.Text.Json"/> serialization and deserialization processes.
/// It ensures that types can be instantiated using their most accessible parameterless constructor (public preferred,
/// falling back to non-public) and that all writable properties, including those with private setters, are populated
/// from JSON data.</para>
/// <para>Key features include:</para>
/// <list type="bullet">
/// <item>Support for public and private constructors, prioritizing parameterless constructors.</item>
/// <item>Configuration of property setters for both public and private setters using reflection.</item>
/// <item>Inclusion of inherited properties from base classes using <see cref="BindingFlags.FlattenHierarchy"/>.</item>
/// <item>Case-insensitive property name matching to align JSON keys with .NET property names.</item>
/// <item>Optional support for <see cref="JsonPropertyNameAttribute"/> to map custom JSON property names.</item>
/// </list>
/// <para>Limitations:</para>
/// <list type="bullet">
/// <item>Relies on reflection, which may impact performance and is not AOT-friendly without source generation.</item>
/// <item>Currently supports only parameterless constructors; parameterized constructors require additional customization.</item>
/// <item>Does not validate required properties; manual validation may be needed for critical data.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var options = new JsonSerializerOptions
/// {
///     TypeInfoResolver = new UniversalContractResolver(),
///     PropertyNameCaseInsensitive = true
/// };
/// var json = @"{""id"": 1, ""name"": ""Test""}";
/// var result = JsonSerializer.Deserialize&lt;TestClass&gt;(json, options);
/// </code>
/// <para>Where <c>TestClass</c> might be:</para>
/// <code>
/// public class TestClass
/// {
///     public int Id { get; private set; }
///     public string Name { get; private set; }
///     private TestClass() { }
/// }
/// </code>
/// </example>
public class UniversalContractResolver : DefaultJsonTypeInfoResolver
{
    /// <summary>
    /// Overrides the base <see cref="DefaultJsonTypeInfoResolver.GetTypeInfo"/> method to customize the
    /// <see cref="JsonTypeInfo"/> for the specified type.
    /// </summary>
    /// <param name="type">The type for which to generate or modify the <see cref="JsonTypeInfo"/>.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> used for serialization and deserialization.</param>
    /// <returns>A configured <see cref="JsonTypeInfo"/> instance for the specified type.</returns>
    /// <remarks>
    /// This method checks if the type is an object and configures its creation and property settings.
    /// It prioritizes a public parameterless constructor, falls back to a non-public one, and sets up
    /// property setters for all writable properties, including those with private access.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no accessible constructor is found for the specified type.
    /// </exception>
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);
        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            // Handle constructor (public or private, preferring parameterless)
            if (jsonTypeInfo.CreateObject is null && !type.IsInterface)
            {
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (constructors.Length > 0)
                {
                    // Prefer public parameterless
                    var parameterlessCtor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0 && c.IsPublic);
                    if (parameterlessCtor != null)
                    {
                        jsonTypeInfo.CreateObject = () => Activator.CreateInstance(type);
                    }
                    else
                    {
                        // Fallback to non-public parameterless
                        var nonPublicParameterlessCtor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0 && !c.IsPublic);
                        if (nonPublicParameterlessCtor != null)
                        {
                            jsonTypeInfo.CreateObject = () => Activator.CreateInstance(type, true);
                        }
                        else
                        {
                            // If no parameterless, use the first constructor (may require params mapping if needed)
                            var firstCtor = constructors[0];
                            jsonTypeInfo.CreateObject = () => Activator.CreateInstance(type, true);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"No accessible constructor found for {type.Name}");
                }
            }

            // Handle property setters (public or private) with case-insensitive matching, including inherited properties
            var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var property in jsonTypeInfo.Properties)
            {
                // Check for JsonPropertyName attribute to map to the correct .NET property
                var jsonPropertyName = property.AttributeProvider?.GetCustomAttributes(false)
                    .OfType<JsonPropertyNameAttribute>()
                    .Select(a => a.Name).FirstOrDefault() ?? property.Name;

                if (allProperties.TryGetValue(jsonPropertyName, out var propInfo) && propInfo.CanWrite)
                {
                    property.Set = (obj, value) =>
                    {
                        propInfo.SetValue(obj, value, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null, null, null);
                    };
                }
            }
        }
        return jsonTypeInfo;
    }
}