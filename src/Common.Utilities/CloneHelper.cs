// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

public static class CloneHelper
{
    // TODO: preferably use the SystemTextJsonSerializer instead https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to?pivots=dotnet-6-0
    //       however that is not feasable at the moment because of the following issues: https://cosmin-vladutu.medium.com/system-text-json-in-net-7-and-deserialization-edb2b65d0a9
    //       solution? JsonObjectCreationHandling.Populate > https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-8/#populate-read-only-members
    private static readonly JsonNetSerializer Serializer = new(
            new JsonSerializerSettings
            {
                ContractResolver = new JsonNetPrivateResolver(),
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TypeNameHandling = TypeNameHandling.All
            });

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

    public static object Clone<T>(T source, Type type)
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

        return Serializer.Deserialize(bytes, type);
    }
}