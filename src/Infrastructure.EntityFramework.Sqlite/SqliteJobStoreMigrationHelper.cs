// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Migrations;

public static class SqliteJobStoreMigrationHelper
{
    public static void CreateQuartzTables(MigrationBuilder migrationBuilder, SqliteJobStoreMigrationOptions options = null)
    {
        options ??= new SqliteJobStoreMigrationOptions();
        var tablePrefix = options.TablePrefix;

        // Create QRTZ_CALENDARS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}CALENDARS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                CALENDAR_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR_NAME", "TEXT"), nullable: false),
                CALENDAR = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR", "BLOB"), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_CALENDARS", x => new { x.SCHED_NAME, x.CALENDAR_NAME });
            });

        // Create QRTZ_JOB_DETAILS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}JOB_DETAILS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                JOB_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_NAME", "TEXT"), nullable: false),
                JOB_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_GROUP", "TEXT"), nullable: false),
                DESCRIPTION = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("DESCRIPTION", "TEXT"), nullable: true),
                JOB_CLASS_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_CLASS_NAME", "TEXT"), nullable: false),
                IS_DURABLE = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_DURABLE", "INTEGER"), nullable: false),
                IS_NONCONCURRENT = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_NONCONCURRENT", "INTEGER"), nullable: false),
                IS_UPDATE_DATA = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_UPDATE_DATA", "INTEGER"), nullable: false),
                REQUESTS_RECOVERY = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("REQUESTS_RECOVERY", "INTEGER"), nullable: false),
                JOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA", "BLOB"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_JOB_DETAILS", x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP });
            });

        // Create QRTZ_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                JOB_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_NAME", "TEXT"), nullable: false),
                JOB_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_GROUP", "TEXT"), nullable: false),
                DESCRIPTION = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("DESCRIPTION", "TEXT"), nullable: true),
                NEXT_FIRE_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("NEXT_FIRE_TIME", "INTEGER"), nullable: true),
                PREV_FIRE_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("PREV_FIRE_TIME", "INTEGER"), nullable: true),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: true),
                TRIGGER_STATE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_STATE", "TEXT"), nullable: false),
                TRIGGER_TYPE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_TYPE", "TEXT"), nullable: false),
                START_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("START_TIME", "INTEGER"), nullable: false),
                END_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("END_TIME", "INTEGER"), nullable: true),
                CALENDAR_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR_NAME", "TEXT"), nullable: true),
                MISFIRE_INSTR = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("MISFIRE_INSTR", "INTEGER"), nullable: true),
                JOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA", "BLOB"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_TRIGGERS_JOB_DETAILS",
                    columns: x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP },
                    principalTable: $"{tablePrefix}JOB_DETAILS",
                    principalColumns: ["SCHED_NAME", "JOB_NAME", "JOB_GROUP"]);
            });

        // Create QRTZ_CRON_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}CRON_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                CRON_EXPRESSION = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CRON_EXPRESSION", "TEXT"), nullable: false),
                TIME_ZONE_ID = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TIME_ZONE_ID", "TEXT"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_CRON_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_CRON_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    onDelete: ReferentialAction.Cascade);
            });

        // Create QRTZ_SIMPLE_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}SIMPLE_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                REPEAT_COUNT = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("REPEAT_COUNT", "INTEGER"), nullable: false),
                REPEAT_INTERVAL = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("REPEAT_INTERVAL", "INTEGER"), nullable: false),
                TIMES_TRIGGERED = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("TIMES_TRIGGERED", "INTEGER"), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_SIMPLE_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_SIMPLE_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    onDelete: ReferentialAction.Cascade);
            });

        // Create QRTZ_SIMPROP_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}SIMPROP_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                STR_PROP_1 = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STR_PROP_1", "TEXT"), nullable: true),
                STR_PROP_2 = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STR_PROP_2", "TEXT"), nullable: true),
                STR_PROP_3 = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STR_PROP_3", "TEXT"), nullable: true),
                INT_PROP_1 = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("INT_PROP_1", "INTEGER"), nullable: true),
                INT_PROP_2 = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("INT_PROP_2", "INTEGER"), nullable: true),
                LONG_PROP_1 = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LONG_PROP_1", "INTEGER"), nullable: true),
                LONG_PROP_2 = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LONG_PROP_2", "INTEGER"), nullable: true),
                DEC_PROP_1 = table.Column<decimal>(type: options.ColumnTypeOverrides.GetValueOrDefault("DEC_PROP_1", "REAL"), nullable: true),
                DEC_PROP_2 = table.Column<decimal>(type: options.ColumnTypeOverrides.GetValueOrDefault("DEC_PROP_2", "REAL"), nullable: true),
                BOOL_PROP_1 = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("BOOL_PROP_1", "INTEGER"), nullable: true),
                BOOL_PROP_2 = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("BOOL_PROP_2", "INTEGER"), nullable: true),
                TIME_ZONE_ID = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TIME_ZONE_ID", "TEXT"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_SIMPROP_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_SIMPROP_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    onDelete: ReferentialAction.Cascade);
            });

        // Create QRTZ_BLOB_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}BLOB_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                BLOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("BLOB_DATA", "BLOB"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_BLOB_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_BLOB_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    onDelete: ReferentialAction.Cascade);
            });

        // Create QRTZ_FIRED_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}FIRED_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                ENTRY_ID = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("ENTRY_ID", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                INSTANCE_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("INSTANCE_NAME", "TEXT"), nullable: false),
                FIRED_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("FIRED_TIME", "INTEGER"), nullable: false),
                SCHED_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_TIME", "INTEGER"), nullable: false),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: false),
                STATE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STATE", "TEXT"), nullable: false),
                JOB_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_NAME", "TEXT"), nullable: true),
                JOB_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_GROUP", "TEXT"), nullable: true),
                IS_NONCONCURRENT = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_NONCONCURRENT", "INTEGER"), nullable: true),
                REQUESTS_RECOVERY = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("REQUESTS_RECOVERY", "INTEGER"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_FIRED_TRIGGERS", x => new { x.SCHED_NAME, x.ENTRY_ID });
            });

        // Create QRTZ_PAUSED_TRIGGER_GRPS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}PAUSED_TRIGGER_GRPS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_PAUSED_TRIGGER_GRPS", x => new { x.SCHED_NAME, x.TRIGGER_GROUP });
            });

        // Create QRTZ_SCHEDULER_STATE table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}SCHEDULER_STATE",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                INSTANCE_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("INSTANCE_NAME", "TEXT"), nullable: false),
                LAST_CHECKIN_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LAST_CHECKIN_TIME", "INTEGER"), nullable: false),
                CHECKIN_INTERVAL = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("CHECKIN_INTERVAL", "INTEGER"), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_SCHEDULER_STATE", x => new { x.SCHED_NAME, x.INSTANCE_NAME });
            });

        // Create QRTZ_LOCKS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}LOCKS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                LOCK_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("LOCK_NAME", "TEXT"), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_LOCKS", x => new { x.SCHED_NAME, x.LOCK_NAME });
            });

        // Create QRTZ_JOURNAL_TRIGGERS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}JOURNAL_TRIGGERS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                ENTRY_ID = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("ENTRY_ID", "TEXT"), nullable: false),
                TRIGGER_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_NAME", "TEXT"), nullable: false),
                TRIGGER_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_GROUP", "TEXT"), nullable: false),
                JOB_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_NAME", "TEXT"), nullable: false),
                JOB_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_GROUP", "TEXT"), nullable: false),
                DESCRIPTION = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("DESCRIPTION", "TEXT"), nullable: true),
                START_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("START_TIME", "TEXT"), nullable: false),
                END_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("END_TIME", "TEXT"), nullable: true),
                SCHEDULED_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHEDULED_TIME", "TEXT"), nullable: false),
                DURATION_MS = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("DURATION_MS", "INTEGER"), nullable: true),
                STATUS = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STATUS", "TEXT"), nullable: false),
                ERROR_MESSAGE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("ERROR_MESSAGE", "TEXT"), nullable: true),
                JOB_DATA_JSON = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA_JSON", "TEXT"), nullable: true),
                INSTANCE_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("INSTANCE_NAME", "TEXT"), nullable: true),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: true),
                RESULT = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("RESULT", "TEXT"), nullable: true),
                RETRY_COUNT = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("RETRY_COUNT", "INTEGER"), nullable: false, defaultValue: 0),
                CATEGORY = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CATEGORY", "TEXT"), nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_JOURNAL_TRIGGERS", x => new { x.SCHED_NAME, x.ENTRY_ID });
            });

        if (options.CreateIndexes)
        {
            // Create indexes for QRTZ_JOURNAL_TRIGGERS
            migrationBuilder.CreateIndex(
                name: $"IX_{tablePrefix.TrimEnd('_')}JT_GJ",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "JOB_GROUP", "JOB_NAME"]);

            migrationBuilder.CreateIndex(
                name: $"IX_{tablePrefix.TrimEnd('_')}JT_ST",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "START_TIME"],
                descending: [false, true]); // START_TIME DESC

            migrationBuilder.CreateIndex(
                name: $"IX_{tablePrefix.TrimEnd('_')}JT_S",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "STATUS"]);

            migrationBuilder.CreateIndex(
                name: $"IX_{tablePrefix.TrimEnd('_')}JT_IN",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "INSTANCE_NAME"]);
        }
    }

    public static void DropQuartzTables(MigrationBuilder migrationBuilder, SqliteJobStoreMigrationOptions options = null)
    {
        options ??= new SqliteJobStoreMigrationOptions();
        var tablePrefix = options.TablePrefix;

        // Drop tables in reverse order to respect foreign key constraints
        migrationBuilder.DropTable(name: $"{tablePrefix}JOURNAL_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}CRON_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}SIMPLE_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}SIMPROP_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}BLOB_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}JOB_DETAILS");
        migrationBuilder.DropTable(name: $"{tablePrefix}CALENDARS");
        migrationBuilder.DropTable(name: $"{tablePrefix}FIRED_TRIGGERS");
        migrationBuilder.DropTable(name: $"{tablePrefix}PAUSED_TRIGGER_GRPS");
        migrationBuilder.DropTable(name: $"{tablePrefix}SCHEDULER_STATE");
        migrationBuilder.DropTable(name: $"{tablePrefix}LOCKS");
    }
}

public class SqliteJobStoreMigrationOptions
{
    public string Schema { get; set; } = null; // Ignored in SQLite, included for consistency
    public string TablePrefix { get; set; } = "QRTZ_";
    public bool CreateIndexes { get; set; } = true;
    public Dictionary<string, string> ColumnTypeOverrides { get; set; } = [];
}