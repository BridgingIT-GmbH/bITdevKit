// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Fixes structural field names while preserving application data converters.</summary>
/// <example><code>options.Converters.Add(new NdjsonEnvelopeJsonConverter());</code></example>
public sealed class NdjsonEnvelopeJsonConverter : JsonConverterFactory
{
    /// <summary>Identifies shared envelope types.</summary>
    /// <example><code>var supported = converter.CanConvert(typeof(NdjsonEnvelope&lt;Weather&gt;));</code></example>
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(NdjsonEnvelope<>);

    /// <summary>Creates the typed structural converter.</summary>
    /// <example><code>var typed = converter.CreateConverter(typeof(NdjsonEnvelope&lt;Weather&gt;), options);</code></example>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(EnvelopeConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()));

    private sealed class EnvelopeConverter<T> : JsonConverter<NdjsonEnvelope<T>>
    {
        public EnvelopeConverter() { }

        public override NdjsonEnvelope<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            RequireObject(root);
            CheckDuplicates(root, ["version", "kind", "data", "recordCount", "error"]);
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            {
                throw Invalid();
            }

            if (number != 1)
            {
                throw new NdjsonStreamException("stream_unsupported_envelope");
            }

            var kind = RequiredString(root, "kind") switch
            {
                "data" => NdjsonEnvelopeKind.Data,
                "heartbeat" => NdjsonEnvelopeKind.Heartbeat,
                "completed" => NdjsonEnvelopeKind.Completed,
                "error" => NdjsonEnvelopeKind.Error,
                _ => throw new NdjsonStreamException("stream_unsupported_envelope")
            };
            var expectedField = kind switch
            {
                NdjsonEnvelopeKind.Data => "data",
                NdjsonEnvelopeKind.Completed => "recordCount",
                NdjsonEnvelopeKind.Error => "error",
                _ => null
            };
            foreach (var field in new[] { "data", "recordCount", "error" })
            {
                if (field != expectedField && root.TryGetProperty(field, out _)) { throw Invalid(); }
            }

            var value = default(T);
            long? count = null;
            NdjsonError error = null;
            if (kind == NdjsonEnvelopeKind.Data)
            {
                if (!root.TryGetProperty("data", out var data))
                {
                    throw Invalid();
                }

                value = data.Deserialize<T>(options);
            }
            else if (kind == NdjsonEnvelopeKind.Completed)
            {
                if (!root.TryGetProperty("recordCount", out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out var total) || total < 0)
                {
                    throw Invalid();
                }

                count = total;
            }
            else if (kind == NdjsonEnvelopeKind.Error)
            {
                if (!root.TryGetProperty("error", out var property))
                {
                    throw Invalid();
                }

                RequireObject(property);
                CheckDuplicates(property, ["code", "message", "traceId"]);
                var trace = property.TryGetProperty("traceId", out var traceProperty) ? RequiredString(property, "traceId") : null;
                error = new NdjsonError(RequiredString(property, "code"), RequiredString(property, "message"), trace);
            }

            return new NdjsonEnvelope<T> { Version = number, Kind = kind, Data = value, RecordCount = count, Error = error };
        }

        public override void Write(Utf8JsonWriter writer, NdjsonEnvelope<T> value, JsonSerializerOptions options)
        {
            if (value is null || value.Version != 1 || !Enum.IsDefined(value.Kind))
            {
                throw Invalid();
            }

            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("kind", value.Kind.ToString().ToLowerInvariant());
            switch (value.Kind)
            {
                case NdjsonEnvelopeKind.Data:
                    writer.WritePropertyName("data");
                    JsonSerializer.Serialize(writer, value.Data, options);
                    break;
                case NdjsonEnvelopeKind.Completed:
                    if (value.RecordCount is not >= 0)
                    {
                        throw Invalid();
                    }

                    writer.WriteNumber("recordCount", value.RecordCount.Value);
                    break;
                case NdjsonEnvelopeKind.Error:
                    if (string.IsNullOrEmpty(value.Error?.Code) || string.IsNullOrEmpty(value.Error.Message))
                    {
                        throw Invalid();
                    }

                    writer.WriteStartObject("error");
                    writer.WriteString("code", value.Error.Code);
                    writer.WriteString("message", value.Error.Message);
                    if (value.Error.TraceId is not null)
                    {
                        writer.WriteString("traceId", value.Error.TraceId);
                    }

                    writer.WriteEndObject();
                    break;
            }

            writer.WriteEndObject();
        }

        private static NdjsonStreamException Invalid() => new("stream_invalid_record");

        private static void RequireObject(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid();
            }
        }

        private static string RequiredString(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(property.GetString()))
            {
                throw Invalid();
            }

            return property.GetString();
        }

        private static void CheckDuplicates(JsonElement value, HashSet<string> reserved)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (reserved.Contains(property.Name) && !seen.Add(property.Name))
                {
                    throw Invalid();
                }
            }
        }
    }
}
