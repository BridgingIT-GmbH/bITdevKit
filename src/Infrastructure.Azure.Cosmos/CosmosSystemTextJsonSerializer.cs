// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.IO;
using System.Text.Json;
using global::Azure.Core.Serialization;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Uses <see cref="Azure.Core.Serialization.JsonObjectSerializer"/> which leverages System.Text.Json
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer // TODO: systemtextjson still has issues deserializing types with no public or multiple constructors, that is an issue for ValueObjects.
{
    private readonly JsonObjectSerializer serializer;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
    {
        this.serializer = new JsonObjectSerializer(jsonSerializerOptions);
    }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                return default;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return (T)this.serializer.Deserialize(stream, typeof(T), default);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        this.serializer.Serialize(stream, input, input.GetType(), default);
        stream.Position = 0;

        return stream;
    }
}