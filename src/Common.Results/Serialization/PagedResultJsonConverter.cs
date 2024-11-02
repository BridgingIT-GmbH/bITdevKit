// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converter for serializing PagedResult{T} objects to JSON format.
/// Inherits from JsonConverter{PagedResult{T}}.
/// Serialization only; deserialization is not supported.
/// </summary>
/// <typeparam name="T">Type of elements within the paged result.</typeparam>
public sealed class PagedResultJsonConverter<T> : JsonConverter<PagedResult<T>>
{
    /// <summary>
    /// Reads and converts the JSON to an object of type PagedResult{T}.
    /// </summary>
    /// <param name="reader">The reader that will read the JSON data.</param>
    /// <param name="typeToConvert">The type of the object to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <returns>The converted PagedResult object.</returns>
    public override PagedResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization is not supported.");
    }

    /// <summary>
    /// Writes a PagedResult{T} object to JSON format.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write the JSON content.</param>
    /// <param name="value">The PagedResult{T} object containing the data to write.</param>
    /// <param name="options">Options to control the behavior during serialization.</param>
    public override void Write(Utf8JsonWriter writer, PagedResult<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Base Result properties
        writer.WriteBoolean("isSuccess", value.IsSuccess);

        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Value, typeof(IEnumerable<T>), options);

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

        // PagedResult specific properties
        writer.WriteNumber("currentPage", value.CurrentPage);
        writer.WriteNumber("totalPages", value.TotalPages);
        writer.WriteNumber("totalCount", value.TotalCount);
        writer.WriteNumber("pageSize", value.PageSize);
        writer.WriteBoolean("hasNextPage", value.HasNextPage);
        writer.WriteBoolean("hasPreviousPage", value.HasPreviousPage);

        writer.WriteEndObject();
    }
}