// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Data;
using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Auditing;
using Microsoft.EntityFrameworkCore;

/// <summary>Reads bounded original graphs while hashing expanded normalized copies through small streaming buffers.</summary>
/// <example><code>var original = await AuditGraphReader.ReadAsync(session, auditId, maximumBytes, cancellationToken);</code></example>
public static class AuditGraphReader
{
    /// <summary>Checks authoritative payload sizes before transfer, preserves owned order, and verifies all copied facts.</summary>
    /// <remarks>No database context or live reader is retained in the returned original.</remarks>
    /// <example><code>var record = await AuditGraphReader.ReadAsync(session, id, 1_048_576, cancellationToken);</code></example>
    public static async Task<Result<AuditRecord>> ReadAsync(AuditStoreSession session, Guid auditId, int maximumBytes, CancellationToken cancellationToken = default)
    {
        if (maximumBytes is < 128 or > 16_777_216) return Result<AuditRecord>.Failure(new AuditError("AuditReadLimitInvalid"));
        var size = await SizeAsync(session, auditId, cancellationToken).ConfigureAwait(false);
        if (size is null) return Result<AuditRecord>.Failure(new AuditError("AuditNotFound"));
        if (size.Value.Bytes > maximumBytes + 512L || size.Value.Actions > 10_000 || size.Value.Changes > 10_000 || size.Value.ActorBytes > 65_536 || size.Value.InitiatingActorBytes > 65_536)
            return Result<AuditRecord>.Failure(new AuditError("AuditRecordTooLarge"));
        var context = session.Context;
        var entity = await context.Set<AuditRecordEntity>().IgnoreQueryFilters().AsNoTracking().Where(row => row.AuditId == auditId).Select(row => new AuditRecordEntity
        {
            AuditId = row.AuditId, SchemaVersion = row.SchemaVersion, ContentHash = row.ContentHash, RootJson = row.RootJson, IntegrityJson = row.IntegrityJson,
            Position = row.Position, StorageState = row.StorageState, ArchivedUtcTicks = row.ArchivedUtcTicks,
            StartedUtcTicks = row.StartedUtcTicks, CompletedUtcTicks = row.CompletedUtcTicks, Key = row.Key, DisplayName = row.DisplayName,
            Kind = row.Kind, ModuleName = row.ModuleName, Outcome = row.Outcome, Completeness = row.Completeness, OriginAuditId = row.OriginAuditId,
            CausationAuditId = row.CausationAuditId, CorrelationId = row.CorrelationId, ActorUserId = row.ActorUserId, InitiatingUserId = row.InitiatingUserId,
            ActorJson = row.ActorJson, InitiatingActorJson = row.InitiatingActorJson, TotalActionCount = row.TotalActionCount, RetainedActionCount = row.RetainedActionCount,
            ChangeCount = row.ChangeCount, HasFailedActions = row.HasFailedActions
        }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null) return Result<AuditRecord>.Failure(new AuditError("AuditNotFound"));
        if (!Enum.IsDefined(entity.StorageState)) return Result<AuditRecord>.Failure(new AuditError("AuditStorageStateInvalid"));
        if (entity.SchemaVersion != 1) return Result<AuditRecord>.Failure(new AuditError("AuditSchemaUnsupported"));
        entity.Actions = await context.Set<AuditActionEntity>().IgnoreQueryFilters().AsNoTracking().Where(row => row.AuditId == auditId).OrderBy(row => row.Ordinal).Select(row => new AuditActionEntity
        {
            Id = row.Id, AuditId = row.AuditId, ParentActionId = row.ParentActionId, Ordinal = row.Ordinal, Sequence = row.Sequence,
            Key = row.Key, Outcome = row.Outcome, StartedUtcTicks = row.StartedUtcTicks, CompletedUtcTicks = row.CompletedUtcTicks, DocumentJson = row.DocumentJson
        }).ToListAsync(cancellationToken).ConfigureAwait(false);
        entity.EntityChanges = await context.Set<AuditEntityChangeEntity>().IgnoreQueryFilters().AsNoTracking().Where(row => row.AuditId == auditId).OrderBy(row => row.Ordinal).Select(row => new AuditEntityChangeEntity
        {
            Id = row.Id, AuditId = row.AuditId, ActionId = row.ActionId, Ordinal = row.Ordinal, TimestampUtcTicks = row.TimestampUtcTicks,
            EntityType = row.EntityType, EntityId = row.EntityId, PersistenceState = row.PersistenceState, DocumentJson = row.DocumentJson
        }).ToListAsync(cancellationToken).ConfigureAwait(false);
        // Canonical source fragments produced by profile1 are ASCII JSON. Unknown additive facts remain in those fragments.
        if (!Ascii(entity.RootJson) || !Ascii(entity.IntegrityJson) || entity.Actions.Any(row => !Ascii(row.DocumentJson)) || entity.EntityChanges.Any(row => !Ascii(row.DocumentJson)))
            return Result<AuditRecord>.Failure(new AuditError("AuditStoredDocumentInvalid"));
        var maximumProjectionBytes = checked((long)maximumBytes * 32);
        await HashRootAsync(session, entity, maximumProjectionBytes, cancellationToken).ConfigureAwait(false);
        await HashActionsAsync(session, entity, maximumProjectionBytes, cancellationToken).ConfigureAwait(false);
        await HashChangesAsync(session, entity, maximumProjectionBytes, cancellationToken).ConfigureAwait(false);
        var record = AuditEntityMapper.ToRecord(entity);
        if (record.IsFailure) return record;
        return AuditRecordCodec.Encode(record.Value).Length <= maximumBytes ? record : Result<AuditRecord>.Failure(new AuditError("AuditRecordTooLarge"));
    }

    private static async Task<(long Bytes, long Actions, long Changes, long ActorBytes, long InitiatingActorBytes)?> SizeAsync(AuditStoreSession session, Guid id, CancellationToken token)
    {
        var sql = session.Sql;
        var rootId = sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.AuditId));
        var actionOwner = sql.Column<AuditActionEntity>("a", nameof(AuditActionEntity.AuditId));
        var changeOwner = sql.Column<AuditEntityChangeEntity>("c", nameof(AuditEntityChangeEntity.AuditId));
        var actionRows = " FROM " + sql.Table<AuditActionEntity>() + " a WHERE " + actionOwner + "=" + rootId;
        var changeRows = " FROM " + sql.Table<AuditEntityChangeEntity>() + " c WHERE " + changeOwner + "=" + rootId;
        var rootBytes = Length(sql, sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.RootJson)));
        var integrityBytes = Length(sql, sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.IntegrityJson)));
        var actionBytes = Length(sql, sql.Column<AuditActionEntity>("a", nameof(AuditActionEntity.DocumentJson)));
        var changeBytes = Length(sql, sql.Column<AuditEntityChangeEntity>("c", nameof(AuditEntityChangeEntity.DocumentJson)));
        var total = rootBytes + "+" + integrityBytes + "+COALESCE((SELECT SUM(" + actionBytes + "+1)" + actionRows + "),0)+COALESCE((SELECT SUM(" + changeBytes + "+1)" + changeRows + "),0)";
        var count = sql.Backend == "SqlServer" ? "COUNT_BIG(*)" : "COUNT(*)";
        var query = "SELECT " + total + ", (SELECT " + count + actionRows + "), (SELECT " + count + changeRows + ")," +
            Length(sql, sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.ActorJson))) + "," + Length(sql, sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.InitiatingActorJson))) +
            " FROM " + sql.Table<AuditRecordEntity>() + " r WHERE " + rootId + "=@p0";
        await using var command = sql.Command(query, [id]);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
        return (Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture));
    }
    private static string Length(AuditSqlDialect sql, string column) => sql.Backend switch
    {
        "SqlServer" => "COALESCE(CONVERT(bigint,DATALENGTH(" + column + "))/2,0)",
        "Postgres" => "COALESCE(octet_length(" + column + "),0)::bigint",
        _ => "COALESCE(length(CAST(" + column + " AS BLOB)),0)"
    };
    private static async Task HashRootAsync(AuditStoreSession session, AuditRecordEntity root, long maximumBytes, CancellationToken token)
    {
        var sql = session.Sql;
        await using var command = sql.Command("SELECT " + sql.Column<AuditRecordEntity>(null, nameof(AuditRecordEntity.SearchJson)) + " FROM " + sql.Table<AuditRecordEntity>() + " WHERE " + sql.Column<AuditRecordEntity>(null, nameof(AuditRecordEntity.AuditId)) + "=@p0", [root.AuditId]);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new ArgumentException("AuditStoredDocumentInvalid");
        using var text = reader.GetTextReader(0);
        root.SearchProjectionHash = await AuditSearchProjection.HashStoredAsync(text, maximumBytes, token).ConfigureAwait(false);
    }
    private static async Task HashActionsAsync(AuditStoreSession session, AuditRecordEntity root, long maximumBytes, CancellationToken token)
    {
        var sql = session.Sql;
        var rows = root.Actions.ToDictionary(row => row.Id);
        await using var command = sql.Command("SELECT " + sql.Column<AuditActionEntity>(null, nameof(AuditActionEntity.Id)) + "," + sql.Column<AuditActionEntity>(null, nameof(AuditActionEntity.SearchJson)) +
            " FROM " + sql.Table<AuditActionEntity>() + " WHERE " + sql.Column<AuditActionEntity>(null, nameof(AuditActionEntity.AuditId)) + "=@p0", [root.AuditId]);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var id = reader.GetGuid(0);
            if (!rows.TryGetValue(id, out var row)) throw new ArgumentException("AuditStoredDocumentInvalid");
            using var text = reader.GetTextReader(1);
            row.SearchProjectionHash = await AuditSearchProjection.HashStoredAsync(text, maximumBytes, token).ConfigureAwait(false);
        }
    }
    private static async Task HashChangesAsync(AuditStoreSession session, AuditRecordEntity root, long maximumBytes, CancellationToken token)
    {
        var sql = session.Sql;
        var rows = root.EntityChanges.ToDictionary(row => row.Id);
        await using var command = sql.Command("SELECT " + sql.Column<AuditEntityChangeEntity>(null, nameof(AuditEntityChangeEntity.Id)) + "," + sql.Column<AuditEntityChangeEntity>(null, nameof(AuditEntityChangeEntity.SearchJson)) +
            " FROM " + sql.Table<AuditEntityChangeEntity>() + " WHERE " + sql.Column<AuditEntityChangeEntity>(null, nameof(AuditEntityChangeEntity.AuditId)) + "=@p0", [root.AuditId]);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var id = reader.GetGuid(0);
            if (!rows.TryGetValue(id, out var row)) throw new ArgumentException("AuditStoredDocumentInvalid");
            using var text = reader.GetTextReader(1);
            row.SearchProjectionHash = await AuditSearchProjection.HashStoredAsync(text, maximumBytes, token).ConfigureAwait(false);
        }
    }
    private static bool Ascii(string text) => text is null || text.All(character => character <= 127);
}
