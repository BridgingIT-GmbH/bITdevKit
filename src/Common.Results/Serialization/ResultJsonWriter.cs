// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

internal static class ResultJsonWriter
{
    internal static void WriteMessages(Utf8JsonWriter writer, IReadOnlyList<string> messages)
    {
        writer.WriteStartArray("messages");
        foreach (var message in messages)
        {
            writer.WriteStringValue(message);
        }

        writer.WriteEndArray();
    }

    internal static void WriteErrors(
        Utf8JsonWriter writer,
        IReadOnlyList<IResultError> errors,
        JsonSerializerOptions options,
        bool useCompactExceptionError = false)
    {
        writer.WriteStartArray("errors");
        foreach (var error in errors)
        {
            if (useCompactExceptionError && error is ExceptionError exceptionError)
            {
                WriteCompactExceptionError(writer, exceptionError);
            }
            else
            {
                JsonSerializer.Serialize(writer, error, error.GetType(), options);
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteCompactExceptionError(Utf8JsonWriter writer, ExceptionError error)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(error.Message))
        {
            writer.WriteString("message", error.Message);
        }

        var typeName = error.ExceptionType;
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            writer.WriteString("type", typeName);
        }

        var stackTrace = error.StackTrace;
        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            writer.WriteString("stackTrace", stackTrace);
        }

        writer.WriteEndObject();
    }
}
