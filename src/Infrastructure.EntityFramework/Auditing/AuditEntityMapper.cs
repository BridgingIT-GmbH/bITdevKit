// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Buffers;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Splits and reassembles original versioned JSON without replacing retained facts with current DTO serialization.</summary>
/// <example><code>var entity = AuditEntityMapper.ToEntity(original);</code></example>
public static class AuditEntityMapper
{
    private static readonly JsonSerializerOptions actorOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    /// <summary>Maps one frozen logical graph into its three owned row types and normalized search copies.</summary>
    /// <example><code>var rows = AuditEntityMapper.ToEntity(record);</code></example>
    public static Result<AuditRecordEntity> ToEntity(AuditRecord record)
    {
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditRecordEntity>.Failure(frozen);
        record = frozen.Value;
        try
        {
            if (record.Actions.Any(action => action.Id == Guid.Empty) || record.EntityChanges.Any(change => change.Id == Guid.Empty) ||
                record.Actions.Select(action => action.Id).Distinct().Count() != record.Actions.Length || record.EntityChanges.Select(change => change.Id).Distinct().Count() != record.EntityChanges.Length)
                return Result<AuditRecordEntity>.Failure(new AuditError("AuditDocumentInvalid"));
            using var source = JsonDocument.Parse(AuditRecordCodec.Encode(record));
            var document = source.RootElement;
            var root = Header(record, document);
            root.RootJson = Fragment(document);
            root.IntegrityJson = document.TryGetProperty("integrityProtection", out var protection) && protection.ValueKind != JsonValueKind.Null ? protection.GetRawText() : null;
            root.SearchJson = AuditSearchProjection.Build(record, "Record");
            var actions = document.TryGetProperty("actions", out var actionArray) ? actionArray.EnumerateArray().ToArray() : [];
            var changes = document.TryGetProperty("entityChanges", out var changeArray) ? changeArray.EnumerateArray().ToArray() : [];
            if (actions.Length != record.Actions.Length || changes.Length != record.EntityChanges.Length) return Result<AuditRecordEntity>.Failure(new AuditError("AuditDocumentInvalid"));
            for (var index = 0; index < actions.Length; index++)
            {
                var action = record.Actions[index];
                root.Actions.Add(new()
                {
                    Id = action.Id, AuditId = record.AuditId, ParentActionId = action.ParentActionId, Ordinal = index, Sequence = action.Sequence,
                    Key = action.Key, Outcome = action.Outcome.ToString(), StartedUtcTicks = action.StartedUtc.UtcTicks, CompletedUtcTicks = action.CompletedUtc.UtcTicks,
                    DocumentJson = actions[index].GetRawText(), SearchJson = AuditSearchProjection.Build(action, "Action")
                });
            }

            for (var index = 0; index < changes.Length; index++)
            {
                var change = record.EntityChanges[index];
                root.EntityChanges.Add(new()
                {
                    Id = change.Id, AuditId = record.AuditId, ActionId = change.ActionId, Ordinal = index, TimestampUtcTicks = change.Timestamp.UtcTicks,
                    EntityType = change.EntityType, EntityId = change.EntityId, PersistenceState = change.PersistenceState,
                    DocumentJson = changes[index].GetRawText(), SearchJson = AuditSearchProjection.Build(change, "Change")
                });
            }

            return Result<AuditRecordEntity>.Success(root);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return Result<AuditRecordEntity>.Failure(new AuditError("AuditDocumentInvalid"));
        }
    }

    /// <summary>Reconstructs the full original and verifies all stored scalar/projection copies before returning it.</summary>
    /// <remarks>Cryptographic verification follows through the Application read policy before any business exposure.</remarks>
    /// <example><code>var original = AuditEntityMapper.ToRecord(headerWithOwnedRows);</code></example>
    public static Result<AuditRecord> ToRecord(AuditRecordEntity entity)
    {
        try
        {
            var actions = entity.Actions.OrderBy(action => action.Ordinal).ToArray();
            var changes = entity.EntityChanges.OrderBy(change => change.Ordinal).ToArray();
            if (actions.Where((action, index) => action.Ordinal != index || action.AuditId != entity.AuditId).Any() || changes.Where((change, index) => change.Ordinal != index || change.AuditId != entity.AuditId).Any())
                return Invalid();
            using var root = JsonDocument.Parse(entity.RootJson);
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var property in root.RootElement.EnumerateObject())
                {
                    if (property.Name == "actions") WriteArray(writer, property.Name, actions.Select(action => action.DocumentJson));
                    else if (property.Name == "entityChanges") WriteArray(writer, property.Name, changes.Select(change => change.DocumentJson));
                    else property.WriteTo(writer);
                }

                if (actions.Length > 0 && !root.RootElement.TryGetProperty("actions", out _) || changes.Length > 0 && !root.RootElement.TryGetProperty("entityChanges", out _)) return Invalid();
                writer.WriteString("contentHash", entity.ContentHash);
                writer.WritePropertyName("integrityProtection");
                if (entity.IntegrityJson is null) writer.WriteNullValue();
                else
                {
                    using var protection = JsonDocument.Parse(entity.IntegrityJson);
                    protection.RootElement.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            var decoded = AuditRecordCodec.Decode(buffer.WrittenMemory);
            if (decoded.IsFailure) return Invalid();
            var record = decoded.Value;
            var expected = Header(record, root.RootElement);
            if (!CopiesAgree(entity, expected) || !ProjectionAgrees(entity.SearchJson, entity.SearchProjectionHash, record, "Record")) return Invalid();
            for (var index = 0; index < actions.Length; index++)
            {
                var row = actions[index];
                var original = record.Actions[index];
                if (row.Id != original.Id || row.ParentActionId != original.ParentActionId || row.Sequence != original.Sequence || row.Key != original.Key || row.Outcome != original.Outcome.ToString() || row.StartedUtcTicks != original.StartedUtc.UtcTicks || row.CompletedUtcTicks != original.CompletedUtc.UtcTicks ||
                    !ProjectionAgrees(row.SearchJson, row.SearchProjectionHash, original, "Action")) return Invalid();
            }

            for (var index = 0; index < changes.Length; index++)
            {
                var row = changes[index];
                var original = record.EntityChanges[index];
                if (row.Id != original.Id || row.ActionId != original.ActionId || row.EntityType != original.EntityType || row.EntityId != original.EntityId || row.TimestampUtcTicks != original.Timestamp.UtcTicks || row.PersistenceState != original.PersistenceState ||
                    !ProjectionAgrees(row.SearchJson, row.SearchProjectionHash, original, "Change")) return Invalid();
            }

            return decoded;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException) { return Invalid(); }
    }

    /// <summary>Creates a payload-free, unverified business summary from bounded scalar/actor copies.</summary>
    /// <example><code>var summary = AuditEntityMapper.Summary(header, asOfUtc);</code></example>
    public static AuditRecordSummary Summary(AuditRecordEntity entity, DateTimeOffset asOfUtc) => new()
    {
        AuditId = entity.AuditId, Key = entity.Key, DisplayName = entity.DisplayName, Kind = entity.Kind,
        StartedUtc = new(entity.StartedUtcTicks, TimeSpan.Zero), CompletedUtc = new(entity.CompletedUtcTicks, TimeSpan.Zero),
        Outcome = Enum.Parse<AuditOutcome>(entity.Outcome), Completeness = Enum.Parse<AuditCompleteness>(entity.Completeness),
        Actor = JsonSerializer.Deserialize<AuditActor>(entity.ActorJson, actorOptions), InitiatingActor = entity.InitiatingActorJson is null ? null : JsonSerializer.Deserialize<AuditActor>(entity.InitiatingActorJson, actorOptions),
        ModuleName = entity.ModuleName, CorrelationId = entity.CorrelationId, OriginAuditId = entity.OriginAuditId, CausationAuditId = entity.CausationAuditId,
        StorageState = entity.StorageState.ToString(), ActionCount = entity.TotalActionCount, RetainedActionCount = entity.RetainedActionCount, ChangeCount = entity.ChangeCount, HasFailedActions = entity.HasFailedActions,
        IntegrityStatus = "NotChecked", AsOfUtc = asOfUtc
    };

    private static AuditRecordEntity Header(AuditRecord record, JsonElement document)
    {
        return new AuditRecordEntity
            {
                AuditId = record.AuditId, SchemaVersion = record.SchemaVersion, ContentHash = record.ContentHash,
                StartedUtcTicks = record.StartedUtc.UtcTicks, CompletedUtcTicks = record.CompletedUtc.UtcTicks,
                Key = record.Key, DisplayName = record.DisplayName, Kind = record.Kind, ModuleName = record.ModuleName, Outcome = record.Outcome.ToString(), Completeness = record.Completeness.ToString(),
                OriginAuditId = record.OriginAuditId, CausationAuditId = record.CausationAuditId, CorrelationId = record.CorrelationId,
                ActorUserId = record.Actor?.UserId, InitiatingUserId = record.InitiatingActor?.UserId,
                ActorJson = Actor(document, "actor", record.Actor), InitiatingActorJson = Actor(document, "initiatingActor", record.InitiatingActor),
                TotalActionCount = record.TotalActionCount, RetainedActionCount = record.Actions.Length, ChangeCount = record.EntityChanges.Length, HasFailedActions = record.HasFailedActions
            };
    }
    private static bool ProjectionAgrees(string json, string streamedHash, object original, string domain) => streamedHash is not null
        ? streamedHash == AuditSearchProjection.ComputeHash(original, domain) || streamedHash == AuditSearchProjection.ComputeLegacyHash(original, domain)
        : json is not null && AuditSearchProjection.IsConsistent(json, original, domain);
    private static bool CopiesAgree(AuditRecordEntity actual, AuditRecordEntity expected) =>
        actual.AuditId == expected.AuditId && actual.SchemaVersion == expected.SchemaVersion && actual.ContentHash == expected.ContentHash &&
        actual.StartedUtcTicks == expected.StartedUtcTicks && actual.CompletedUtcTicks == expected.CompletedUtcTicks && actual.Key == expected.Key && actual.DisplayName == expected.DisplayName && actual.Kind == expected.Kind &&
        actual.ModuleName == expected.ModuleName && actual.Outcome == expected.Outcome && actual.Completeness == expected.Completeness && actual.OriginAuditId == expected.OriginAuditId && actual.CausationAuditId == expected.CausationAuditId &&
        actual.CorrelationId == expected.CorrelationId && actual.ActorUserId == expected.ActorUserId && actual.InitiatingUserId == expected.InitiatingUserId && actual.ActorJson == expected.ActorJson && actual.InitiatingActorJson == expected.InitiatingActorJson &&
        actual.TotalActionCount == expected.TotalActionCount && actual.RetainedActionCount == expected.RetainedActionCount && actual.ChangeCount == expected.ChangeCount && actual.HasFailedActions == expected.HasFailedActions;
    private static string Actor(JsonElement document, string name, AuditActor fallback) => fallback is null ? null : Encoding.UTF8.GetString(AuditCanonicalizerV1.Encode(JsonSerializer.SerializeToElement(fallback, actorOptions)));
    private static void WriteArray(Utf8JsonWriter writer, string name, IEnumerable<string> fragments)
    {
        writer.WriteStartArray(name);
        foreach (var fragment in fragments)
        {
            using var document = JsonDocument.Parse(fragment);
            document.RootElement.WriteTo(writer);
        }

        writer.WriteEndArray();
    }
    private static string Fragment(JsonElement original)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in original.EnumerateObject())
            {
                if (property.Name is "contentHash" or "integrityProtection") continue;
                if (property.Name is "actions" or "entityChanges") { writer.WriteStartArray(property.Name); writer.WriteEndArray(); }
                else property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
    private static Result<AuditRecord> Invalid() => Result<AuditRecord>.Failure(new AuditError("AuditStoredDocumentInvalid"));
}
