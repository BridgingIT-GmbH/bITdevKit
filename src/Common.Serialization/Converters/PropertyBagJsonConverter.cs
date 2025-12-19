// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter for PropertyBag to handle serialization/deserialization as a dictionary.
/// </summary>
public class PropertyBagJsonConverter : JsonConverter<PropertyBag>
{
    public override PropertyBag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        var bag = new PropertyBag();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return bag;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();

            reader.Read();

            var value = ReadValue(ref reader, options);
            bag.Set(propertyName, value);
        }

        throw new JsonException("Expected end of object");
    }

    public override void Write(Utf8JsonWriter writer, PropertyBag value, JsonSerializerOptions options)
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
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    private static object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt32(out var intValue) ? intValue :
                                   reader.TryGetInt64(out var longValue) ? longValue :
                                   reader.TryGetDecimal(out var decimalValue) ? decimalValue :
                                   reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => JsonSerializer.Deserialize<PropertyBag>(ref reader, options),
            JsonTokenType.StartArray => JsonSerializer.Deserialize<List<object>>(ref reader, options),
            _ => throw new JsonException($"Unexpected token type: {reader.TokenType}")
        };
    }
}
