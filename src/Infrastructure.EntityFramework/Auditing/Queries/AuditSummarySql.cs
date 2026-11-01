// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Data.Common;
using System.Globalization;
using BridgingIT.DevKit.Application.Auditing;

/// <summary>Limits ordinary search projections to bounded scalar/actor copies, excluding source and search JSON payloads.</summary>
/// <example><code>var columns = AuditSummarySql.Columns(dialect);</code></example>
public static class AuditSummarySql
{
    private static readonly string[] properties = [nameof(AuditRecordEntity.AuditId), nameof(AuditRecordEntity.Key), nameof(AuditRecordEntity.DisplayName), nameof(AuditRecordEntity.Kind),
        nameof(AuditRecordEntity.StartedUtcTicks), nameof(AuditRecordEntity.CompletedUtcTicks), nameof(AuditRecordEntity.Outcome), nameof(AuditRecordEntity.Completeness),
        nameof(AuditRecordEntity.ActorJson), nameof(AuditRecordEntity.InitiatingActorJson), nameof(AuditRecordEntity.ModuleName), nameof(AuditRecordEntity.CorrelationId),
        nameof(AuditRecordEntity.OriginAuditId), nameof(AuditRecordEntity.CausationAuditId), nameof(AuditRecordEntity.StorageState), nameof(AuditRecordEntity.TotalActionCount),
        nameof(AuditRecordEntity.RetainedActionCount), nameof(AuditRecordEntity.ChangeCount), nameof(AuditRecordEntity.HasFailedActions)];

    /// <summary>Returns only the scalar projection required by the public summary contract.</summary>
    /// <example><code>var select = "SELECT " + AuditSummarySql.Columns(dialect);</code></example>
    public static string Columns(AuditSqlDialect sql, string alias = "r") => string.Join(",", properties.Select(property =>
    {
        var column = sql.Column<AuditRecordEntity>(alias, property);
        if (property is nameof(AuditRecordEntity.ActorJson) or nameof(AuditRecordEntity.InitiatingActorJson))
        {
            var length = sql.Backend switch { "SqlServer" => "DATALENGTH(" + column + ")", "Postgres" => "octet_length(" + column + ")", _ => "length(CAST(" + column + " AS BLOB))" };
            column = "CASE WHEN " + length + "<=65536 THEN " + column + " ELSE NULL END";
        }

        return column + " AS " + sql.Quote(property);
    }));

    /// <summary>Reads one selected row without child graphs or a computed Verified assertion.</summary>
    /// <example><code>var summary = AuditSummarySql.Read(reader, asOfUtc);</code></example>
    public static AuditRecordSummary Read(DbDataReader reader, DateTimeOffset asOfUtc)
    {
        string Text(string property) { var ordinal = reader.GetOrdinal(property); return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal); }

        long Number(string property) => Convert.ToInt64(reader.GetValue(reader.GetOrdinal(property)), CultureInfo.InvariantCulture);
        var actor = Text(nameof(AuditRecordEntity.ActorJson));
        var state = (AuditStorageState)reader.GetInt32(reader.GetOrdinal(nameof(AuditRecordEntity.StorageState)));
        if (actor is null || !Enum.IsDefined(state)) throw new ArgumentException("AuditStoredDocumentInvalid");
        var cause = reader.GetOrdinal(nameof(AuditRecordEntity.CausationAuditId));
        return AuditEntityMapper.Summary(new()
        {
            AuditId = reader.GetGuid(reader.GetOrdinal(nameof(AuditRecordEntity.AuditId))), Key = Text(nameof(AuditRecordEntity.Key)), DisplayName = Text(nameof(AuditRecordEntity.DisplayName)), Kind = Text(nameof(AuditRecordEntity.Kind)),
            StartedUtcTicks = Number(nameof(AuditRecordEntity.StartedUtcTicks)), CompletedUtcTicks = Number(nameof(AuditRecordEntity.CompletedUtcTicks)), Outcome = Text(nameof(AuditRecordEntity.Outcome)), Completeness = Text(nameof(AuditRecordEntity.Completeness)),
            ActorJson = actor, InitiatingActorJson = Text(nameof(AuditRecordEntity.InitiatingActorJson)), ModuleName = Text(nameof(AuditRecordEntity.ModuleName)), CorrelationId = Text(nameof(AuditRecordEntity.CorrelationId)),
            OriginAuditId = reader.GetGuid(reader.GetOrdinal(nameof(AuditRecordEntity.OriginAuditId))), CausationAuditId = reader.IsDBNull(cause) ? null : reader.GetGuid(cause), StorageState = state,
            TotalActionCount = Number(nameof(AuditRecordEntity.TotalActionCount)), RetainedActionCount = Number(nameof(AuditRecordEntity.RetainedActionCount)), ChangeCount = Number(nameof(AuditRecordEntity.ChangeCount)),
            HasFailedActions = reader.GetBoolean(reader.GetOrdinal(nameof(AuditRecordEntity.HasFailedActions)))
        }, asOfUtc);
    }
}
