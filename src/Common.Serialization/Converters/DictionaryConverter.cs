// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class DictionaryConverter : JsonConverter<IDictionary<string, object>>
{
    public override IDictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var dictionary = new Dictionary<string, object>();
        var originalDepth = reader.CurrentDepth;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == originalDepth)
            {
                return dictionary;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var key = reader.GetString()!;
                reader.Read(); // Move to value

                // Peek ahead to determine the value type
                var value = this.DeserializeValue(ref reader, options);
                dictionary[key] = value ?? new object(); // Default to new object if null
            }
        }
        throw new JsonException("Unexpected end of JSON");
    }

    private object DeserializeValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var intValue) ? intValue : reader.GetDouble();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
            case JsonTokenType.StartArray:
                return JsonSerializer.Deserialize<List<object>>(ref reader, options);
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value, kvp.Value?.GetType() ?? typeof(object), options);
        }
        writer.WriteEndObject();
    }
}
