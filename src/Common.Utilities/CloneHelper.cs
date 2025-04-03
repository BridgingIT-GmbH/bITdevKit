// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

/// <summary>
///     Provides functionality to create deep clones of objects.
/// </summary>
public static class CloneHelper
{
    // TODO: preferably use the SystemTextJsonSerializer instead https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to?pivots=dotnet-6-0
    //       however that is not feasable at the moment because of the following issues: https://cosmin-vladutu.medium.com/system-text-json-in-net-7-and-deserialization-edb2b65d0a9
    //       solution? JsonObjectCreationHandling.Populate > https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-8/#populate-read-only-members
    // see CloneHelperNew

    /// <summary>
    ///     Provides a serializer for cloning objects using JSON serialization.
    /// </summary>
    /// <remarks>
    ///     Utilizes the JsonNetSerializer with customized JsonSerializerSettings.
    /// </remarks>
    private static readonly JsonNetSerializer Serializer = new(new JsonSerializerSettings
    {
        ContractResolver = new JsonNetPrivateResolver(),
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        TypeNameHandling = TypeNameHandling.All
    });

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

        var bytes = Serializer.SerializeToBytes(source);
        if (bytes.IsNullOrEmpty())
        {
            return default;
        }

        return Serializer.Deserialize<T>(bytes);
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

        var bytes = Serializer.SerializeToBytes(source);

        return bytes.IsNullOrEmpty() ? default : Serializer.Deserialize(bytes, type);
    }
}