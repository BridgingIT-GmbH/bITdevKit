// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

/// <summary>
///     The default Cosmos JSON.NET serializer, replicated here to allow for custom settings.
/// </summary>
public class
    CosmosJsonNetSerializer(
        JsonSerializerSettings jsonSerializerSettings =
            null) : CosmosSerializer // source: https://raw.githubusercontent.com/Azure/azure-cosmos-dotnet-v3/master/Microsoft.Azure.Cosmos.Encryption/src/CosmosJsonDotNetSerializer.cs
{
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);

    private readonly JsonSerializerSettings serializerSettings = jsonSerializerSettings;

    /// <summary>
    ///     Convert a Stream to the passed in type.
    /// </summary>
    /// <typeparam name="T">The type of object that should be deserialized</typeparam>
    /// <param name="stream">An open stream that is readable that contains JSON</param>
    /// <returns>The object representing the deserialized stream</returns>
    public override T FromStream<T>(Stream stream)
    {
        EnsureArg.IsNotNull(stream, nameof(stream));

        if (typeof(Stream).IsAssignableFrom(typeof(T)))
        {
            return (T)(object)stream;
        }

        using var sr = new StreamReader(stream);
        using var jsonTextReader = new JsonTextReader(sr);
        var jsonSerializer = this.GetSerializer();

        return jsonSerializer.Deserialize<T>(jsonTextReader);
    }

    /// <summary>
    ///     Converts an object to a open readable stream.
    /// </summary>
    /// <typeparam name="T">The type of object being serialized</typeparam>
    /// <param name="input">The object to be serialized</param>
    /// <returns>An open readable stream containing the JSON of the serialized object</returns>
    public override Stream ToStream<T>(T input)
    {
        var streamPayload = new MemoryStream();
        using (var streamWriter = new StreamWriter(streamPayload, DefaultEncoding, 1024, true))
        using (JsonWriter writer = new JsonTextWriter(streamWriter))
        {
            writer.Formatting = Formatting.None;
            var jsonSerializer = this.GetSerializer();
            jsonSerializer.Serialize(writer, input);
            writer.Flush();
            streamWriter.Flush();
        }

        streamPayload.Position = 0;

        return streamPayload;
    }

    /// <summary>
    ///     JsonSerializer has hit a race conditions with custom settings that cause null reference exception.
    ///     To avoid the race condition a new JsonSerializer is created for each call
    /// </summary>
    private JsonSerializer GetSerializer()
    {
        return JsonSerializer.Create(this.serializerSettings);
    }
}