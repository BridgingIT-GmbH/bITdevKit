// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Supplies explicit migration-time initialization of durable audit store identity.</summary>
/// <example><code>migrationBuilder.InitializeAuditingStore();</code></example>
public static class AuditingTestMigrationBuilderExtensions
{
    /// <summary>Seeds exactly one technical row with database-generated unique store/epoch identities.</summary>
    /// <remarks>Call once in the reviewed creation migration after table creation. Runtime reads never initialize or replace this row.</remarks>
    /// <example><code>migrationBuilder.InitializeAuditingStore();</code></example>
    public static MigrationBuilder InitializeAuditingStore(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var sqlServer = migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
        var postgres = migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL";
        var sqlite = migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite";
        if (!sqlServer && !postgres && !sqlite) throw new InvalidOperationException("AuditProviderUnsupported");
        string Quote(string value) => sqlServer ? "[" + value + "]" : "\"" + value + "\"";
        var table = Quote("__Auditing_StoreState");
        var columns = new[] { "Singleton", "StoreId", "StoreEpoch", "PublishedPosition", "LifecycleGeneration", "RemovedThroughPosition", "RejectCompletedBeforeUtcTicks", "ArchiveCompletedBeforeUtcTicks" };
        var identity = sqlServer ? "NEWID()" : postgres ? "gen_random_uuid()" : "lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)))";
        migrationBuilder.Sql("INSERT INTO " + table + " (" + string.Join(",", columns.Select(Quote)) + ") VALUES (1," + identity + "," + identity + ",0,0,0,0,0)");
        return migrationBuilder;
    }
}
