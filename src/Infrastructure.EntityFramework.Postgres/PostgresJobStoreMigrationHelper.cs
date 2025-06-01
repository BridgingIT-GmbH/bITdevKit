// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Migrations;

public static class PostgresJobStoreMigrationHelper
{
    public static void CreateQuartzTables(MigrationBuilder migrationBuilder, PostgresJobStoreMigrationOptions options = null)
    {
        options ??= new PostgresJobStoreMigrationOptions();
        var schema = options.Schema;
        var tablePrefix = options.TablePrefix;

        // Create QRTZ_CALENDARS table
        migrationBuilder.CreateTable(
            name: $"{tablePrefix}CALENDARS",
            columns: table => new
            {
                SCHED_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_NAME", "TEXT"), nullable: false),
                CALENDAR_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR_NAME", "TEXT"), nullable: false),
                CALENDAR = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR", "BYTEA"), nullable: false)
            },
            schema: schema,
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
                IS_DURABLE = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_DURABLE", "BOOL"), nullable: false),
                IS_NONCONCURRENT = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_NONCONCURRENT", "BOOL"), nullable: false),
                IS_UPDATE_DATA = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_UPDATE_DATA", "BOOL"), nullable: false),
                REQUESTS_RECOVERY = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("REQUESTS_RECOVERY", "BOOL"), nullable: false),
                JOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA", "BYTEA"), nullable: true)
            },
            schema: schema,
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
                NEXT_FIRE_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("NEXT_FIRE_TIME", "BIGINT"), nullable: true),
                PREV_FIRE_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("PREV_FIRE_TIME", "BIGINT"), nullable: true),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: true),
                TRIGGER_STATE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_STATE", "TEXT"), nullable: false),
                TRIGGER_TYPE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TRIGGER_TYPE", "TEXT"), nullable: false),
                START_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("START_TIME", "BIGINT"), nullable: false),
                END_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("END_TIME", "BIGINT"), nullable: true),
                CALENDAR_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CALENDAR_NAME", "TEXT"), nullable: true),
                MISFIRE_INSTR = table.Column<short>(type: options.ColumnTypeOverrides.GetValueOrDefault("MISFIRE_INSTR", "SMALLINT"), nullable: true),
                JOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA", "BYTEA"), nullable: true)
            },
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_TRIGGERS_JOB_DETAILS",
                    columns: x => new { x.SCHED_NAME, x.JOB_NAME, x.JOB_GROUP },
                    principalTable: $"{tablePrefix}JOB_DETAILS",
                    principalColumns: ["SCHED_NAME", "JOB_NAME", "JOB_GROUP"],
                    principalSchema: schema);
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
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_CRON_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_CRON_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    principalSchema: schema,
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
                REPEAT_COUNT = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("REPEAT_COUNT", "BIGINT"), nullable: false),
                REPEAT_INTERVAL = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("REPEAT_INTERVAL", "BIGINT"), nullable: false),
                TIMES_TRIGGERED = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("TIMES_TRIGGERED", "BIGINT"), nullable: false)
            },
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_SIMPLE_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_SIMPLE_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    principalSchema: schema,
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
                LONG_PROP_1 = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LONG_PROP_1", "BIGINT"), nullable: true),
                LONG_PROP_2 = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LONG_PROP_2", "BIGINT"), nullable: true),
                DEC_PROP_1 = table.Column<decimal>(type: options.ColumnTypeOverrides.GetValueOrDefault("DEC_PROP_1", "NUMERIC(13,4)"), nullable: true),
                DEC_PROP_2 = table.Column<decimal>(type: options.ColumnTypeOverrides.GetValueOrDefault("DEC_PROP_2", "NUMERIC(13,4)"), nullable: true),
                BOOL_PROP_1 = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("BOOL_PROP_1", "BOOL"), nullable: true),
                BOOL_PROP_2 = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("BOOL_PROP_2", "BOOL"), nullable: true),
                TIME_ZONE_ID = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("TIME_ZONE_ID", "TEXT"), nullable: true)
            },
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_SIMPROP_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_SIMPROP_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    principalSchema: schema,
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
                BLOB_DATA = table.Column<byte[]>(type: options.ColumnTypeOverrides.GetValueOrDefault("BLOB_DATA", "BYTEA"), nullable: true)
            },
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_BLOB_TRIGGERS", x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP });
                table.ForeignKey(
                    name: $"FK_{tablePrefix}_BLOB_TRIGGERS_TRIGGERS",
                    columns: x => new { x.SCHED_NAME, x.TRIGGER_NAME, x.TRIGGER_GROUP },
                    principalTable: $"{tablePrefix}TRIGGERS",
                    principalColumns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                    principalSchema: schema,
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
                FIRED_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("FIRED_TIME", "BIGINT"), nullable: false),
                SCHED_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHED_TIME", "BIGINT"), nullable: false),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: false),
                STATE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STATE", "TEXT"), nullable: false),
                JOB_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_NAME", "TEXT"), nullable: true),
                JOB_GROUP = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_GROUP", "TEXT"), nullable: true),
                IS_NONCONCURRENT = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("IS_NONCONCURRENT", "BOOL"), nullable: true),
                REQUESTS_RECOVERY = table.Column<bool>(type: options.ColumnTypeOverrides.GetValueOrDefault("REQUESTS_RECOVERY", "BOOL"), nullable: true)
            },
            schema: schema,
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
            schema: schema,
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
                LAST_CHECKIN_TIME = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("LAST_CHECKIN_TIME", "BIGINT"), nullable: false),
                CHECKIN_INTERVAL = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("CHECKIN_INTERVAL", "BIGINT"), nullable: false)
            },
            schema: schema,
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
            schema: schema,
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
                START_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("START_TIME", "TIMESTAMP"), nullable: false),
                END_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("END_TIME", "TIMESTAMP"), nullable: true),
                SCHEDULED_TIME = table.Column<DateTime>(type: options.ColumnTypeOverrides.GetValueOrDefault("SCHEDULED_TIME", "TIMESTAMP"), nullable: false),
                DURATION_MS = table.Column<long>(type: options.ColumnTypeOverrides.GetValueOrDefault("DURATION_MS", "BIGINT"), nullable: true),
                STATUS = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("STATUS", "TEXT"), nullable: false),
                ERROR_MESSAGE = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("ERROR_MESSAGE", "TEXT"), nullable: true),
                JOB_DATA_JSON = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("JOB_DATA_JSON", "TEXT"), nullable: true),
                INSTANCE_NAME = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("INSTANCE_NAME", "TEXT"), nullable: true),
                PRIORITY = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("PRIORITY", "INTEGER"), nullable: true),
                RESULT = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("RESULT", "TEXT"), nullable: true),
                RETRY_COUNT = table.Column<int>(type: options.ColumnTypeOverrides.GetValueOrDefault("RETRY_COUNT", "INTEGER"), nullable: false, defaultValue: 0),
                CATEGORY = table.Column<string>(type: options.ColumnTypeOverrides.GetValueOrDefault("CATEGORY", "TEXT"), nullable: true)
            },
            schema: schema,
            constraints: table =>
            {
                table.PrimaryKey($"PK_{tablePrefix}_JOURNAL_TRIGGERS", x => new { x.SCHED_NAME, x.ENTRY_ID });
            });

        if (options.CreateIndexes)
        {
            // Create indexes
            // For QRTZ_JOB_DETAILS
            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}JD_RR",
                table: $"{tablePrefix}JOB_DETAILS",
                column: "REQUESTS_RECOVERY",
                schema: schema);

            // For QRTZ_TRIGGERS
            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_NFT",
                table: $"{tablePrefix}TRIGGERS",
                column: "NEXT_FIRE_TIME",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_TS",
                table: $"{tablePrefix}TRIGGERS",
                column: "TRIGGER_STATE",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_NFT_TS",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["NEXT_FIRE_TIME", "TRIGGER_STATE"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_GJ",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "JOB_GROUP", "JOB_NAME"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_CN",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "CALENDAR_NAME"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_TGS",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "TRIGGER_GROUP", "TRIGGER_STATE"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_TNS",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP", "TRIGGER_STATE"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_MNFT",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "MISFIRE_INSTR", "NEXT_FIRE_TIME", "TRIGGER_STATE"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}T_MNFTG",
                table: $"{tablePrefix}TRIGGERS",
                columns: ["SCHED_NAME", "MISFIRE_INSTR", "NEXT_FIRE_TIME", "TRIGGER_GROUP", "TRIGGER_STATE"],
                schema: schema);

            // For QRTZ_FIRED_TRIGGERS
            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_TN",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "TRIGGER_NAME",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_TG",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "TRIGGER_GROUP",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_TNG",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                columns: ["SCHED_NAME", "TRIGGER_NAME", "TRIGGER_GROUP"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_IN",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "INSTANCE_NAME",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_JN",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "JOB_NAME",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_JG",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "JOB_GROUP",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_RR",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                column: "REQUESTS_RECOVERY",
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_GJ",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                columns: ["SCHED_NAME", "JOB_GROUP", "JOB_NAME"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}FT_GT",
                table: $"{tablePrefix}FIRED_TRIGGERS",
                columns: ["SCHED_NAME", "TRIGGER_GROUP", "TRIGGER_NAME"],
                schema: schema);

            // For QRTZ_JOURNAL_TRIGGERS
            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}JT_GJ",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "JOB_GROUP", "JOB_NAME"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}JT_ST",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "START_TIME"],
                schema: schema,
                descending: [false, true]); // START_TIME DESC

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}JT_S",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "STATUS"],
                schema: schema);

            migrationBuilder.CreateIndex(
                name: $"IX_{schema}_{tablePrefix.TrimEnd('_')}JT_IN",
                table: $"{tablePrefix}JOURNAL_TRIGGERS",
                columns: ["SCHED_NAME", "INSTANCE_NAME"],
                schema: schema);
        }
    }

    public static void DropQuartzTables(MigrationBuilder migrationBuilder, PostgresJobStoreMigrationOptions options = null)
    {
        options ??= new PostgresJobStoreMigrationOptions();
        var schema = options.Schema;
        var tablePrefix = options.TablePrefix;

        // Drop tables in reverse order to respect foreign key constraints
        migrationBuilder.DropTable(name: $"{tablePrefix}JOURNAL_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}CRON_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}SIMPLE_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}SIMPROP_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}BLOB_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}JOB_DETAILS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}CALENDARS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}FIRED_TRIGGERS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}PAUSED_TRIGGER_GRPS", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}SCHEDULER_STATE", schema: schema);
        migrationBuilder.DropTable(name: $"{tablePrefix}LOCKS", schema: schema);
    }
}

public class PostgresJobStoreMigrationOptions
{
    public string Schema { get; set; } = "public";
    public string TablePrefix { get; set; } = "QRTZ_";
    public bool CreateIndexes { get; set; } = true;
    public Dictionary<string, string> ColumnTypeOverrides { get; set; } = [];
}