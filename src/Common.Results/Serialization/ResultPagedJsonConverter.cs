// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes a paged result's values and pagination metadata to JSON.
/// </summary>
/// <typeparam name="T">Type of elements within the paged result.</typeparam>
public sealed class ResultPagedJsonConverter<T> : JsonConverter<ResultPaged<T>>
{
    /// <summary>
    /// Deserialization is unsupported.
    /// </summary>
    /// <param name="reader">The reader that will read the JSON data.</param>
    /// <param name="typeToConvert">The type of the object to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override ResultPaged<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization is not supported.");
    }

    /// <summary>
    /// Writes a ResultPaged{T} object to JSON format.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write the JSON content.</param>
    /// <param name="value">The ResultPaged{T} object containing the data to write.</param>
    /// <param name="options">Options to control the behavior during serialization.</param>
    public override void Write(Utf8JsonWriter writer, ResultPaged<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Base Result properties
        writer.WriteBoolean("isSuccess", value.IsSuccess);

        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Value, typeof(IEnumerable<T>), options);

        ResultJsonWriter.WriteMessages(writer, value.Messages);
        ResultJsonWriter.WriteErrors(writer, value.Errors, options);

        // ResultPaged specific properties
        writer.WriteNumber("currentPage", value.CurrentPage);
        writer.WriteNumber("totalPages", value.TotalPages);
        writer.WriteNumber("totalCount", value.TotalCount);
        writer.WriteNumber("pageSize", value.PageSize);
        writer.WriteBoolean("hasNextPage", value.HasNextPage);
        writer.WriteBoolean("hasPreviousPage", value.HasPreviousPage);

        writer.WriteEndObject();
    }
}
