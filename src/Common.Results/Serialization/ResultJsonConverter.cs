// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A custom JSON converter for <see cref="Result"/> that writes a compact and
/// client-safe JSON representation.
/// 
/// Behavior:
/// - Writes the following top-level properties:
///   - <c>isSuccess</c> (boolean)
///   - <c>messages</c> (array of strings)
///   - <c>errors</c> (array of error objects)
/// - For general errors, delegates to <see cref="JsonSerializer"/> using the
///   error's type and the provided options.
/// - For <see cref="ExceptionError"/>, emits a safe, flattened object and
///   deliberately excludes the raw <see cref="Exception"/> instance to avoid
///   unsupported types (e.g., <c>MethodBase</c>) and oversized/PII payloads.
/// 
/// Notes:
/// - Deserialization is not supported and will throw <see cref="NotSupportedException"/>.
/// - This converter is intended for outbound API responses or logging, not for
///   round-tripping back into domain models.
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
        WriteMessages(writer, value);
        WriteErrors(writer, value, options);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the <c>messages</c> array for the result.
    /// </summary>
    private static void WriteMessages(Utf8JsonWriter writer, Result value)
    {
        writer.WriteStartArray("messages");
        foreach (var message in value.Messages.SafeNull())
        {
            writer.WriteStringValue(message);
        }
        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes the <c>errors</c> array for the result. This method
    /// special-cases <see cref="ExceptionError"/> to avoid serializing the raw
    /// <see cref="Exception"/> instance (which can contain unsupported members
    /// like <c>MethodBase</c> and potentially sensitive data).
    /// </summary>
    /// <remarks>
    /// Why special-case ExceptionError?
    /// - System.Text.Json cannot serialize certain members of <see cref="Exception"/>
    ///   (e.g., <c>TargetSite</c>/<see cref="System.Reflection.MethodBase"/>), which
    ///   throws <see cref="NotSupportedException"/>.
    /// - Serializing full exceptions can bloat payloads and leak PII/implementation details.
    /// 
    /// Output shape for ExceptionError:
    /// {
    ///   "message": "…",
    ///   "exceptionType": "Namespace.ExceptionType",
    ///   "stackTrace": "…" // optional, omitted if null/empty
    /// }
    /// </remarks>
    private static void WriteErrors(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartArray("errors");

        foreach (var error in value.Errors.SafeNull())
        {
            if (error is ExceptionError exErr)
            {
                WriteExceptionError(writer, exErr);
                continue;
            }

            // Default path for non-exception errors.
            JsonSerializer.Serialize(writer, error, error.GetType(), options);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes a safe, flattened representation of <see cref="ExceptionError"/>.
    /// The raw <see cref="ExceptionError.Exception"/> is NOT serialized.
    /// </summary>
    private static void WriteExceptionError(Utf8JsonWriter writer, ExceptionError value)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(value.Message))
        {
            writer.WriteString("message", value.Message);
        }

        var typeName = value.ExceptionType;
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            writer.WriteString("type", typeName);
        }

        var stack = value.StackTrace;
        if (!string.IsNullOrWhiteSpace(stack))
        {
            writer.WriteString("stackTrace", stack);
        }

        writer.WriteEndObject();
    }
}