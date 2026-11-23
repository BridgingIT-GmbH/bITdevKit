// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes a value result to JSON, including its payload on success.
/// </summary>
public sealed class ResultValueJsonConverter<T> : JsonConverter<Result<T>>
{
    /// <summary>
    /// Deserialization is unsupported.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="typeToConvert">The type of the object to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization is not supported.");
    }

    /// <summary>
    /// Writes the JSON representation of the specified Result{T} object.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="result">The Result{T} object value to convert.</param>
    /// <param name="options">An object that specifies options to control the behavior during writing.</param>
    public override void Write(Utf8JsonWriter writer, Result<T> result, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteBoolean("isSuccess", result.IsSuccess);

        if (result.IsSuccess)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, result.Value, typeof(T), options);
        }

        ResultJsonWriter.WriteMessages(writer, result.Messages);
        ResultJsonWriter.WriteErrors(writer, result.Errors, options);

        writer.WriteEndObject();
    }
}
