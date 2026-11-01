// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Auditing;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Auditing;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Verifies lossless graph reconstruction and rejection of altered storage copies.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditEntityMapperTests</code></example>
[UnitTest("Infrastructure")]
public sealed class AuditEntityMapperTests
{
    /// <summary>Checks consumers own their DbContext and migrations while the feature supplies the four audit sets.</summary>
    /// <example><code>typeof(IAuditingDbContext).GetProperties().Length.ShouldBe(4);</code></example>
    [Fact]
    public void ContextContract_ExposesAuditSetsWithoutProvidingContextsOrMigrations()
    {
        var types = typeof(IAuditingDbContext).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(IAuditingDbContext).Namespace).ToArray();
        types.ShouldNotContain(type => typeof(DbContext).IsAssignableFrom(type));
        types.ShouldNotContain(type => typeof(Migration).IsAssignableFrom(type));
        types.ShouldNotContain(type => type.Name.Contains("MigrationBuilder") || type.Name.Contains("ModelBuilder"));
        typeof(IAuditingDbContext).GetProperties().Length.ShouldBe(4);
    }

    /// <summary>Checks unknown root, actor, action, resource and property members survive splitting.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(AuditEntityMapper.ToEntity(record).Value);</code></example>
    [Fact]
    public void ToRecord_UnknownNestedAdditiveFields_RoundtripsOriginalCanonicalBytes()
    {
        var original = WithAdditiveFields(Record());
        var expected = AuditRecordCodec.Encode(original);

        var mapped = AuditEntityMapper.ToEntity(original);
        mapped.IsSuccess.ShouldBeTrue();
        var restored = AuditEntityMapper.ToRecord(mapped.Value);

        restored.IsSuccess.ShouldBeTrue();
        restored.Value.ContentHash.ShouldBe(original.ContentHash);
        AuditRecordCodec.Encode(restored.Value).ShouldBe(expected);
        mapped.Value.RootJson.ShouldContain("futureRoot");
        mapped.Value.Actions.First().DocumentJson.ShouldContain("futureAction");
        mapped.Value.EntityChanges.Single().DocumentJson.ShouldContain("futureProperty");
    }

    /// <summary>Checks physical row order and action sequence cannot reorder the original array.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity);</code></example>
    [Fact]
    public void ToRecord_UnorderedRowsAndNonmonotonicSequence_RestoresOriginalArrayOrdinals()
    {
        var original = Record();
        var entity = Entity(original);
        entity.Actions.Select(action => action.Sequence).ShouldBe([20L, 3L]);
        entity.Actions = entity.Actions.Reverse().ToList();

        var restored = AuditEntityMapper.ToRecord(entity);

        restored.IsSuccess.ShouldBeTrue();
        restored.Value.Actions.Select(action => action.Id).ShouldBe(original.Actions.Select(action => action.Id));
        restored.Value.Actions.Select(action => action.Sequence).ShouldBe([20L, 3L]);
        AuditRecordCodec.Encode(restored.Value).ShouldBe(AuditRecordCodec.Encode(original));
    }

    /// <summary>Checks scalar copies cannot contradict the authenticated original graph.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity).IsFailure.ShouldBeTrue();</code></example>
    [Theory]
    [InlineData("Record.Key")]
    [InlineData("Record.CompletedUtcTicks")]
    [InlineData("Record.ActorUserId")]
    [InlineData("Record.ActorJson")]
    [InlineData("Record.RetainedActionCount")]
    [InlineData("Record.HasFailedActions")]
    [InlineData("Action.Key")]
    [InlineData("Action.Sequence")]
    [InlineData("Action.ParentActionId")]
    [InlineData("Change.EntityId")]
    [InlineData("Change.Timestamp")]
    [InlineData("Change.PersistenceState")]
    public void ToRecord_AlteredScalarCopy_RejectsStoredDocument(string copy)
    {
        var entity = Entity(Record());
        switch (copy)
        {
            case "Record.Key": entity.Key = "forged"; break;
            case "Record.CompletedUtcTicks": entity.CompletedUtcTicks++; break;
            case "Record.ActorUserId": entity.ActorUserId = "other-user"; break;
            case "Record.ActorJson": entity.ActorJson = "{}"; break;
            case "Record.RetainedActionCount": entity.RetainedActionCount++; break;
            case "Record.HasFailedActions": entity.HasFailedActions = !entity.HasFailedActions; break;
            case "Action.Key": entity.Actions.First().Key = "forged"; break;
            case "Action.Sequence": entity.Actions.First().Sequence++; break;
            case "Action.ParentActionId": entity.Actions.First().ParentActionId = Guid.NewGuid(); break;
            case "Change.EntityId": entity.EntityChanges.Single().EntityId = "other-entity"; break;
            case "Change.Timestamp": entity.EntityChanges.Single().TimestampUtcTicks++; break;
            case "Change.PersistenceState": entity.EntityChanges.Single().PersistenceState = "RolledBack"; break;
        }

        var result = AuditEntityMapper.ToRecord(entity);

        result.IsFailure.ShouldBeTrue(copy);
        result.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditStoredDocumentInvalid");
    }

    /// <summary>Checks both retained and streamed search copies against their source facts.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity);</code></example>
    [Theory]
    [InlineData("Record", false)]
    [InlineData("Action", false)]
    [InlineData("Change", false)]
    [InlineData("Record", true)]
    [InlineData("Action", true)]
    [InlineData("Change", true)]
    public async Task ToRecord_AlteredSearchCopy_RejectsStoredDocument(string domain, bool streamed)
    {
        var entity = Entity(Record());
        var json = JsonNode.Parse(SearchJson(entity, domain));
        var binding = domain == "Change" ? "EntityType" : "Key";
        json["fields"][AuditSearchEncoding.FieldKey(binding)]["String"]["exact"] = AuditSearchEncoding.Text("forged", false);
        SetSearchJson(entity, domain, json.ToJsonString());
        if (streamed)
        {
            await StreamSearchCopiesAsync(entity);
        }

        var result = AuditEntityMapper.ToRecord(entity);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditStoredDocumentInvalid");
    }

    /// <summary>Checks bounded reader digests allow search text to be omitted from the materialized graph.</summary>
    /// <example><code>entity.SearchProjectionHash = await AuditSearchProjection.HashStoredAsync(reader, limit);</code></example>
    [Fact]
    public async Task ToRecord_StreamedSearchHashesWithoutSearchJson_PreservesOriginalGraph()
    {
        var original = WithAdditiveFields(Record());
        var entity = Entity(original);

        await StreamSearchCopiesAsync(entity);
        var restored = AuditEntityMapper.ToRecord(entity);

        entity.SearchJson.ShouldBeNull();
        entity.Actions.All(action => action.SearchJson is null).ShouldBeTrue();
        entity.EntityChanges.All(change => change.SearchJson is null).ShouldBeTrue();
        restored.IsSuccess.ShouldBeTrue();
        AuditRecordCodec.Encode(restored.Value).ShouldBe(AuditRecordCodec.Encode(original));
    }

    /// <summary>Checks malformed or structurally incomplete ASCII cannot authenticate as a valid projection.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity).IsFailure.ShouldBeTrue();</code></example>
    [Theory]
    [InlineData("Record", "{")]
    [InlineData("Action", "not-json")]
    [InlineData("Change", "[]")]
    [InlineData("Record", "{\"version\":1}")]
    public async Task ToRecord_MalformedStreamedProjection_RejectsDigestMismatch(string domain, string malformed)
    {
        var entity = Entity(Record());
        SetSearchJson(entity, domain, malformed);
        await StreamSearchCopiesAsync(entity);

        var result = AuditEntityMapper.ToRecord(entity);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditStoredDocumentInvalid");
    }

    /// <summary>Checks malformed retained projections are returned as a controlled failure.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity);</code></example>
    [Theory]
    [InlineData("Record")]
    [InlineData("Action")]
    [InlineData("Change")]
    public void ToRecord_MalformedRetainedProjection_RejectsStoredDocument(string domain)
    {
        var entity = Entity(Record());
        SetSearchJson(entity, domain, "{");

        var result = AuditEntityMapper.ToRecord(entity);

        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Checks owned row identity and ordinal validation before reconstruction.</summary>
    /// <example><code>AuditEntityMapper.ToRecord(entity);</code></example>
    [Theory]
    [InlineData("Action.Ordinal")]
    [InlineData("Action.Owner")]
    [InlineData("Change.Ordinal")]
    [InlineData("Change.Owner")]
    public void ToRecord_InvalidOwnedRow_RejectsStoredDocument(string copy)
    {
        var entity = Entity(Record());
        switch (copy)
        {
            case "Action.Ordinal": entity.Actions.First().Ordinal = 4; break;
            case "Action.Owner": entity.Actions.First().AuditId = Guid.NewGuid(); break;
            case "Change.Ordinal": entity.EntityChanges.Single().Ordinal = 1; break;
            case "Change.Owner": entity.EntityChanges.Single().AuditId = Guid.NewGuid(); break;
        }

        var result = AuditEntityMapper.ToRecord(entity);

        result.IsFailure.ShouldBeTrue();
    }

    private static AuditRecordEntity Entity(AuditRecord record)
    {
        var mapped = AuditEntityMapper.ToEntity(record);
        mapped.IsSuccess.ShouldBeTrue();
        return mapped.Value;
    }

    private static string SearchJson(AuditRecordEntity entity, string domain) => domain switch
    {
        "Record" => entity.SearchJson,
        "Action" => entity.Actions.First().SearchJson,
        _ => entity.EntityChanges.Single().SearchJson
    };

    private static void SetSearchJson(AuditRecordEntity entity, string domain, string json)
    {
        switch (domain)
        {
            case "Record": entity.SearchJson = json; break;
            case "Action": entity.Actions.First().SearchJson = json; break;
            default: entity.EntityChanges.Single().SearchJson = json; break;
        }
    }

    [Fact]
    public async Task ToRecord_LegacyTruncationProjection_PreservesOriginalContent()
    {
        var original = Record() with { ContentHash = null, Actions = [], EntityChanges = [], Resources = [], Completeness = AuditCompleteness.Truncated, Notes = [new() { Code = "AuditValueTooLarge" }] };
        var entity = Entity(original);
        var legacy = JsonNode.Parse(entity.SearchJson);
        legacy["version"] = 1;
        var flag = legacy["fields"][AuditSearchEncoding.FieldKey("HasTruncation")]["Boolean"];
        flag["exact"] = "0";
        flag["folded"] = "0";
        entity.SearchJson = legacy.ToJsonString();
        using (var document = JsonDocument.Parse(entity.SearchJson)) entity.SearchJson = Encoding.UTF8.GetString(AuditCanonicalizerV1.Encode(document.RootElement));

        AuditEntityMapper.ToRecord(entity).IsSuccess.ShouldBeTrue();
        await StreamSearchCopiesAsync(entity);
        var restored = AuditEntityMapper.ToRecord(entity);

        restored.IsSuccess.ShouldBeTrue();
        restored.Value.ContentHash.ShouldBe(entity.ContentHash);
        restored.Value.Notes.Single().Code.ShouldBe("AuditValueTooLarge");
    }

    private static async Task StreamSearchCopiesAsync(AuditRecordEntity entity)
    {
        entity.SearchProjectionHash = await StreamHashAsync(entity.SearchJson);
        entity.SearchJson = null;
        foreach (var action in entity.Actions)
        {
            action.SearchProjectionHash = await StreamHashAsync(action.SearchJson);
            action.SearchJson = null;
        }

        foreach (var change in entity.EntityChanges)
        {
            change.SearchProjectionHash = await StreamHashAsync(change.SearchJson);
            change.SearchJson = null;
        }
    }

    private static async Task<string> StreamHashAsync(string json)
    {
        using var reader = new StringReader(json);
        return await AuditSearchProjection.HashStoredAsync(reader, json.Length);
    }

    private static AuditRecord WithAdditiveFields(AuditRecord record)
    {
        var json = JsonNode.Parse(AuditRecordCodec.Encode(record)).AsObject();
        json["futureRoot"] = new JsonObject { ["contentHash"] = "nested-retained", ["items"] = new JsonArray(3, 1, 2) };
        json["actor"]["futureActor"] = new JsonObject { ["labels"] = new JsonArray("one", "two") };
        json["resources"][0]["futureResource"] = "root-resource-retained";
        json["actions"][0]["futureAction"] = new JsonObject { ["enabled"] = true };
        json["actions"][0]["resources"][0]["futureResource"] = "action-resource-retained";
        json["entityChanges"][0]["futureChange"] = 42;
        json["entityChanges"][0]["properties"][0]["futureProperty"] = new JsonObject { ["order"] = new JsonArray(2, 1) };
        json["entityChanges"][0]["properties"][0]["oldValue"]["futureValue"] = "retained";
        json.Remove("contentHash");
        json.Remove("integrityProtection");
        using var body = JsonDocument.Parse(json.ToJsonString());
        json["contentHash"] = AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(body.RootElement));
        json["integrityProtection"] = null;
        var decoded = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString()));
        decoded.IsSuccess.ShouldBeTrue();
        return decoded.Value;
    }

    private static AuditRecord Record()
    {
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var firstAction = Guid.Parse("83ea92f8-8eb7-459b-aa15-b35e2a706faa");
        var record = new AuditRecord
        {
            AuditId = Guid.Parse("235283d5-43ac-4ed1-a7c8-e7251aa27d0b"), OriginAuditId = Guid.Parse("235283d5-43ac-4ed1-a7c8-e7251aa27d0b"),
            Key = "orders.approve", DisplayName = "Approve order", ModuleName = "Orders", StartedUtc = timestamp, CompletedUtc = timestamp.AddMinutes(1),
            Actor = new() { Kind = "User", UserId = "u-123", DisplayName = "Captured operator", IsAuthenticated = true },
            Resources = [new("Order", "O-123", "Captured order")],
            Details = ImmutableDictionary<string, AuditValue>.Empty.Add("amount", AuditValue.From(123.4500m)),
            TotalActionCount = 2,
            Actions =
            [
                new() { Id = firstAction, Key = "orders.validate", Sequence = 20, StartedUtc = timestamp, CompletedUtc = timestamp.AddSeconds(1), Resources = [new("Order", "O-123")] },
                new() { Id = Guid.Parse("2cc60bce-5c06-465f-a689-2f79e0eab60b"), ParentActionId = firstAction, Key = "orders.persist", Sequence = 3, StartedUtc = timestamp.AddSeconds(1), CompletedUtc = timestamp.AddSeconds(2) }
            ],
            EntityChanges =
            [
                new()
                {
                    Id = Guid.Parse("13057c14-cc93-4b9f-8d21-cf360773d822"), ActionId = firstAction, EntityType = "Order", EntityId = "O-123",
                    Timestamp = timestamp.AddSeconds(2), PersistenceState = "Committed",
                    Properties = [new() { Path = "Status", Operation = "Replace", OldValue = AuditValue.From("Pending"), NewValue = AuditValue.From("Approved") }]
                }
            ]
        };
        var frozen = AuditRecordCodec.Freeze(record);
        frozen.IsSuccess.ShouldBeTrue();
        return frozen.Value;
    }
}
