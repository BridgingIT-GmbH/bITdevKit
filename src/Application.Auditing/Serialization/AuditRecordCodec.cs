// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using System.Buffers;
using System.Globalization;

/// <summary>Versioned frozen record encoding. Original additive JSON survives historical reads.</summary>
/// <example><code>var frozen = AuditRecordCodec.Freeze(record);</code></example>
public static class AuditRecordCodec
{
    private static readonly JsonSerializerOptions options = CreateOptions();

    /// <summary>Validates content identity and freezes a version-one record.</summary>
    /// <example><code>var result = AuditRecordCodec.Freeze(record);</code></example>
    public static Result<AuditRecord> Freeze(AuditRecord record)
    {
        if (record is null || record.SchemaVersion != 1 || record.AuditId == Guid.Empty || string.IsNullOrWhiteSpace(record.Key))
            return Result<AuditRecord>.Failure(new AuditError("AuditDocumentInvalid"));
        try
        {
            record = record with
            {
                Resources = record.Resources.Select(SnapshotResource).ToImmutableArray(),
                Actions = record.Actions.Select(action => action with { Resources = action.Resources.Select(SnapshotResource).ToImmutableArray() }).ToImmutableArray()
            };
            var hash = AuditCanonicalizerV1.Hash(CanonicalDocument(record));
            if (record.ContentHash is not null && record.ContentHash != hash) return Result<AuditRecord>.Failure(new AuditError("AuditContentHashInvalid"));
            return Result<AuditRecord>.Success(record with { ContentHash = hash });
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return Result<AuditRecord>.Failure(new AuditError("AuditDocumentInvalid"));
        }
    }

    /// <summary>Gets original complete immutable content, excluding only root hash/protection fields.</summary>
    /// <example><code>var bytes = AuditRecordCodec.CanonicalDocument(record);</code></example>
    public static byte[] CanonicalDocument(AuditRecord record)
    {
        var projection = Body(JsonSerializer.SerializeToElement(record, options));
        var bytes = AuditCanonicalizerV1.Encode(projection);
        return record.OriginalDocument.ValueKind == JsonValueKind.Object && record.OriginalProjectionHash == AuditCanonicalizerV1.Hash(bytes)
            ? AuditCanonicalizerV1.Encode(Body(record.OriginalDocument)) : bytes;
    }

    /// <summary>Encodes a complete document and optional envelope without discarding unknown additions.</summary>
    /// <example><code>var bytes = AuditRecordCodec.Encode(record);</code></example>
    public static byte[] Encode(AuditRecord record)
    {
        using var body = JsonDocument.Parse(CanonicalDocument(record));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in body.RootElement.EnumerateObject()) property.WriteTo(writer);
            writer.WriteString("contentHash", record.ContentHash);
            writer.WritePropertyName("integrityProtection");
            JsonSerializer.Serialize(writer, record.IntegrityProtection, options);
            writer.WriteEndObject();
        }

        using var complete = JsonDocument.Parse(buffer.WrittenMemory);
        return AuditCanonicalizerV1.Encode(complete.RootElement);
    }

    /// <summary>Reads a supported original record and retains all additive JSON, including nested additions.</summary>
    /// <example><code>var result = AuditRecordCodec.Decode(bytes);</code></example>
    public static Result<AuditRecord> Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 16_777_216) return Result<AuditRecord>.Failure(new AuditError("AuditRecordTooLarge"));
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            if (!document.RootElement.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1)
                return Result<AuditRecord>.Failure(new AuditError("AuditSchemaUnsupported"));
            // Also rejects duplicate names, at any depth, before a lossy typed reader sees them.
            _ = AuditCanonicalizerV1.Encode(document.RootElement);
            var record = document.RootElement.Deserialize<AuditRecord>(options);
            if (record is null) return Result<AuditRecord>.Failure(new AuditError("AuditDocumentInvalid"));
            var projectionHash = AuditCanonicalizerV1.Hash(CanonicalDocument(record));
            return Freeze(record with { OriginalDocument = document.RootElement.Clone(), OriginalProjectionHash = projectionHash });
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException)
        {
            return Result<AuditRecord>.Failure(new AuditError("AuditDocumentInvalid"));
        }
    }

    private static AuditResourceReference SnapshotResource(AuditResourceReference resource) => new(resource.Type, resource.Id, resource.DisplayName);
    private static JsonElement Body(JsonElement record)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in record.EnumerateObject())
                if (property.Name is not ("contentHash" or "integrityProtection")) property.WriteTo(writer);
            writer.WriteEndObject();
        }

        using var body = JsonDocument.Parse(buffer.WrittenMemory);
        return body.RootElement.Clone();
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var result = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 64 };
        result.Converters.Add(new JsonStringEnumConverter());
        result.Converters.Add(new UtcTimestampConverter());
        return result;
    }
    private sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions serializerOptions) => DateTimeOffset.Parse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions serializerOptions) => writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
    }
}
