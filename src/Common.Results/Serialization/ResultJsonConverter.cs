// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// <para>
/// A custom JSON converter for <see cref="Result"/> that writes a compact and
/// client-safe JSON representation.
/// </para>
/// <para>
/// Behavior:
/// - Writes the following top-level properties:
///   - <c>isSuccess</c> (boolean)
///   - <c>messages</c> (array of strings)
///   - <c>errors</c> (array of error objects)
/// - For general errors, delegates to <see cref="JsonSerializer"/> using the
///   error's type and the provided options.
/// - For <see cref="ExceptionError"/>, emits a safe, flattened object and
///   uses <c>type</c> for the exception type. It excludes the raw <see cref="Exception"/> instance to avoid
///   unsupported types (e.g., <c>MethodBase</c>) and oversized/PII payloads.
/// </para>
/// <para>
/// Notes:
/// - Deserialization is not supported and will throw <see cref="NotSupportedException"/>.
/// - This converter is intended for outbound API responses or logging, not for
///   round-tripping back into domain models.
/// </para>
/// </summary>
public sealed class ResultJsonConverter : JsonConverter<Result>
{
    /// <summary>
    /// Deserialization is not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Deserialization is not supported.");

    /// <summary>
    /// Writes the JSON representation of a <see cref="Result"/>.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="Result"/> to serialize.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> in effect.</param>
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteBoolean("isSuccess", value.IsSuccess);
        ResultJsonWriter.WriteMessages(writer, value.Messages);
        ResultJsonWriter.WriteErrors(writer, value.Errors, options, useCompactExceptionError: true);

        writer.WriteEndObject();
    }
}
