// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public static class CloneHelperNew
{
    /// <summary>
    ///     Provides serializer options for cloning objects using System.Text.Json.
    /// </summary>
    /// <remarks>
    ///     Configured to handle polymorphic serialization, preserve references, and allow non-public constructors.
    /// </remarks>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.Preserve,
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { AddPolymorphicTypeHandling }
        }
    };

    /// <summary>
    ///     Creates a deep clone of the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone. Must be a class.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <returns>A deep clone of the specified object, or null if the source is null.</returns>
    public static T Clone<T>(T source)
        where T : class
    {
        if (EqualityComparer<T>.Default.Equals(source, default))
        {
            return default;
        }

        var bytes = SerializeToBytes(source);
        if (bytes == null || bytes.Length == 0)
        {
            return default;
        }

        return Deserialize<T>(bytes);
    }

    /// <summary>
    ///     Creates a deep copy of the given object using the specified type for deserialization.
    /// </summary>
    /// <typeparam name="T">The type of the source object.</typeparam>
    /// <param name="source">The object to be cloned.</param>
    /// <param name="type">The type used for deserialization of the clone.</param>
    /// <returns>A deep copied clone of the source object as the given type.</returns>
    public static object Clone<T>(T source, Type type)
        where T : class
    {
        if (EqualityComparer<T>.Default.Equals(source, default))
        {
            return default;
        }

        var bytes = SerializeToBytes(source);
        return bytes == null || bytes.Length == 0 ? default : Deserialize(bytes, type);
    }

    /// <summary>
    ///     Serializes an object to a byte array using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The serialized object as a byte array.</returns>
    private static byte[] SerializeToBytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
    }

    /// <summary>
    ///     Deserializes a byte array to an object of type T using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="bytes">The byte array to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    private static T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    /// <summary>
    ///     Deserializes a byte array to an object of the specified type using System.Text.Json.
    /// </summary>
    /// <param name="bytes">The byte array to deserialize.</param>
    /// <param name="type">The type of the object to deserialize to.</param>
    /// <returns>The deserialized object.</returns>
    private static object Deserialize(byte[] bytes, Type type)
    {
        return JsonSerializer.Deserialize(bytes, type, SerializerOptions);
    }

    /// <summary>
    ///     Adds polymorphic type handling to the type information.
    /// </summary>
    /// <param name="typeInfo">The JSON type information to modify.</param>
    private static void AddPolymorphicTypeHandling(JsonTypeInfo typeInfo)
    {
        // Skip non-POCO types or primitives
        if (typeInfo.Kind != JsonTypeInfoKind.Object ||
            typeInfo.Type.IsPrimitive ||
            typeInfo.Type == typeof(string))
        {
            return;
        }

        // Set discriminator property name for type discrimination
        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$type",
            IgnoreUnrecognizedTypeDiscriminators = true,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
        };

        // Recursively register derived types in the hierarchy
        RegisterDerivedTypes(typeInfo);
    }

    /// <summary>
    ///     Registers the derived types for polymorphic serialization.
    /// </summary>
    /// <param name="typeInfo">The base type information.</param>
    private static void RegisterDerivedTypes(JsonTypeInfo typeInfo)
    {
        // Only process types that can have derived types
        if (typeInfo.Type.IsSealed || typeInfo.Type.IsValueType)
        {
            return;
        }

        // Register itself as a derived type (necessary for references)
        typeInfo.PolymorphismOptions.DerivedTypes.Add(
            new JsonDerivedType(typeInfo.Type, typeInfo.Type.FullName));

        // Find derived types in loaded assemblies
        var derivedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Type.EmptyTypes;
                }
            })
            .Where(type => type != typeInfo.Type && !type.IsAbstract && !type.IsInterface &&
                           typeInfo.Type.IsAssignableFrom(type))
            .ToList();

        // Register each derived type
        foreach (var derivedType in derivedTypes)
        {
            typeInfo.PolymorphismOptions.DerivedTypes.Add(
                new JsonDerivedType(derivedType, derivedType.FullName));
        }
    }
}