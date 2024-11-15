// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converts Result objects to and from JSON format.
/// </summary>
public sealed class ResultJsonConverter : JsonConverter<Result>
{
    /// <summary>
    /// Reads and converts the JSON to the specified type.
    /// </summary>
    /// <param name="reader">The JSON reader to read from.</param>
    /// <param name="typeToConvert">The type of object to convert.</param>
    /// <param name="options">An object that contains settings to be used during deserialization.</param>
    /// <returns>The converted object.</returns>
    /// <exception cref="NotSupportedException">Thrown when deserialization is not supported.</exception>
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization is not supported.");
    }

    /// <summary>
    /// Writes the JSON representation of a Result object.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The Result object to write.</param>
    /// <param name="options">The serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteBoolean("isSuccess", value.IsSuccess);

        writer.WriteStartArray("messages");
        foreach (var message in value.Messages)
        {
            writer.WriteStringValue(message);
        }
        writer.WriteEndArray();

        writer.WriteStartArray("errors");
        foreach (var error in value.Errors)
        {
            JsonSerializer.Serialize(writer, error, error.GetType(), options);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}