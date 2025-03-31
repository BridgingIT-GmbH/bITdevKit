// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static partial class SqlStatements
{
    public static string QuartzTables(string database, string tablePrefix)
    {
        return $@"
        USE [{database}]

        DECLARE @tablePrefix NVARCHAR(255) = '{tablePrefix}';
        DECLARE @schema NVARCHAR(255);
        DECLARE @tablePrefixOnly NVARCHAR(255);
        DECLARE @dotPosition INT = CHARINDEX('.', @tablePrefix);
        SET @schema = REPLACE(REPLACE(SUBSTRING(@tablePrefix, 1, @dotPosition - 1), '[', ''), ']', '');
        SET @tablePrefixOnly = REPLACE(REPLACE(SUBSTRING(@tablePrefix, @dotPosition + 1, LEN(@tablePrefix) - @dotPosition), '[', ''), ']', '');

        IF EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema
                AND TABLE_NAME IN (
                    @tablePrefixOnly + 'CALENDARS',
                    @tablePrefixOnly + 'CRON_TRIGGERS',
                    @tablePrefixOnly + 'BLOB_TRIGGERS',
                    @tablePrefixOnly + 'FIRED_TRIGGERS',
                    @tablePrefixOnly + 'PAUSED_TRIGGER_GRPS',
                    @tablePrefixOnly + 'SCHEDULER_STATE',
                    @tablePrefixOnly + 'LOCKS',
                    @tablePrefixOnly + 'JOB_DETAILS',
                    @tablePrefixOnly + 'SIMPLE_TRIGGERS',
                    @tablePrefixOnly + 'SIMPROP_TRIGGERS',
                    @tablePrefixOnly + 'TRIGGERS',
                    @tablePrefixOnly + 'JOURNAL_TRIGGERS'
                )
        )
        BEGIN
            PRINT 'One or more QRTZ tables already exist. Aborting script.';
            RETURN;
        END

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'CALENDARS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [CALENDAR_NAME] nvarchar(200) NOT NULL,
            [CALENDAR] varbinary(max) NOT NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_CALENDARS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [CALENDAR_NAME])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'CRON_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [CRON_EXPRESSION] nvarchar(120) NOT NULL,
            [TIME_ZONE_ID] nvarchar(80),
            CONSTRAINT [PK_' + @tablePrefixOnly + '_CRON_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [ENTRY_ID] nvarchar(140) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [INSTANCE_NAME] nvarchar(200) NOT NULL,
            [FIRED_TIME] bigint NOT NULL,
            [SCHED_TIME] bigint NOT NULL,
            [PRIORITY] int NOT NULL,
            [STATE] nvarchar(16) NOT NULL,
            [JOB_NAME] nvarchar(150) NULL,
            [JOB_GROUP] nvarchar(150) NULL,
            [IS_NONCONCURRENT] bit NULL,
            [REQUESTS_RECOVERY] bit NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_FIRED_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [ENTRY_ID])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'PAUSED_TRIGGER_GRPS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_PAUSED_TRIGGER_GRPS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'SCHEDULER_STATE] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [INSTANCE_NAME] nvarchar(200) NOT NULL,
            [LAST_CHECKIN_TIME] bigint NOT NULL,
            [CHECKIN_INTERVAL] bigint NOT NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_SCHEDULER_STATE] PRIMARY KEY CLUSTERED ([SCHED_NAME], [INSTANCE_NAME])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'LOCKS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [LOCK_NAME] nvarchar(40) NOT NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_LOCKS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [LOCK_NAME])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'JOB_DETAILS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [JOB_NAME] nvarchar(150) NOT NULL,
            [JOB_GROUP] nvarchar(150) NOT NULL,
            [DESCRIPTION] nvarchar(250) NULL,
            [JOB_CLASS_NAME] nvarchar(250) NOT NULL,
            [IS_DURABLE] bit NOT NULL,
            [IS_NONCONCURRENT] bit NOT NULL,
            [IS_UPDATE_DATA] bit NOT NULL,
            [REQUESTS_RECOVERY] bit NOT NULL,
            [JOB_DATA] varbinary(max) NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_JOB_DETAILS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [JOB_NAME], [JOB_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'SIMPLE_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [REPEAT_COUNT] int NOT NULL,
            [REPEAT_INTERVAL] bigint NOT NULL,
            [TIMES_TRIGGERED] int NOT NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_SIMPLE_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'SIMPROP_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [STR_PROP_1] nvarchar(512) NULL,
            [STR_PROP_2] nvarchar(512) NULL,
            [STR_PROP_3] nvarchar(512) NULL,
            [INT_PROP_1] int NULL,
            [INT_PROP_2] int NULL,
            [LONG_PROP_1] bigint NULL,
            [LONG_PROP_2] bigint NULL,
            [DEC_PROP_1] numeric(13,4) NULL,
            [DEC_PROP_2] numeric(13,4) NULL,
            [BOOL_PROP_1] bit NULL,
            [BOOL_PROP_2] bit NULL,
            [TIME_ZONE_ID] nvarchar(80) NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_SIMPROP_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'BLOB_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [BLOB_DATA] varbinary(max) NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_BLOB_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [JOB_NAME] nvarchar(150) NOT NULL,
            [JOB_GROUP] nvarchar(150) NOT NULL,
            [DESCRIPTION] nvarchar(250) NULL,
            [NEXT_FIRE_TIME] bigint NULL,
            [PREV_FIRE_TIME] bigint NULL,
            [PRIORITY] int NULL,
            [TRIGGER_STATE] nvarchar(16) NOT NULL,
            [TRIGGER_TYPE] nvarchar(8) NOT NULL,
            [START_TIME] bigint NOT NULL,
            [END_TIME] bigint NULL,
            [CALENDAR_NAME] nvarchar(200) NULL,
            [MISFIRE_INSTR] int NULL,
            [JOB_DATA] varbinary(max) NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        )');

        EXEC('CREATE TABLE [' + @schema + '].[' + @tablePrefixOnly + 'JOURNAL_TRIGGERS] (
            [SCHED_NAME] nvarchar(120) NOT NULL,
            [ENTRY_ID] nvarchar(140) NOT NULL,
            [TRIGGER_NAME] nvarchar(150) NOT NULL,
            [TRIGGER_GROUP] nvarchar(150) NOT NULL,
            [JOB_NAME] nvarchar(150) NOT NULL,
            [JOB_GROUP] nvarchar(150) NOT NULL,
            [DESCRIPTION] nvarchar(250) NULL,
            [START_TIME] datetime2(7) NOT NULL,
            [END_TIME] datetime2(7) NULL,
            [SCHEDULED_TIME] datetime2(7) NOT NULL,
            [RUN_TIME_MS] bigint NULL,
            [STATUS] nvarchar(16) NOT NULL,
            [ERROR_MESSAGE] nvarchar(max) NULL,
            [JOB_DATA_JSON] nvarchar(max) NULL,
            [INSTANCE_NAME] nvarchar(200) NULL,
            [PRIORITY] int NULL,
            [RESULT] nvarchar(max) NULL,
            [RETRY_COUNT] int NOT NULL DEFAULT 0,
            [CATEGORY] nvarchar(100) NULL,
            CONSTRAINT [PK_' + @tablePrefixOnly + '_JOURNAL_TRIGGERS] PRIMARY KEY CLUSTERED ([SCHED_NAME], [ENTRY_ID])
        )');

        -- Constraints
        EXEC('ALTER TABLE [' + @schema + '].[' + @tablePrefixOnly + 'CRON_TRIGGERS]
        ADD CONSTRAINT [FK_' + @tablePrefixOnly + '_CRON_TRIGGERS_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE');

        EXEC('ALTER TABLE [' + @schema + '].[' + @tablePrefixOnly + 'SIMPLE_TRIGGERS]
        ADD CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPLE_TRIGGERS_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE');

        EXEC('ALTER TABLE [' + @schema + '].[' + @tablePrefixOnly + 'SIMPROP_TRIGGERS]
        ADD CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPROP_TRIGGERS_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE');

        EXEC('ALTER TABLE [' + @schema + '].[' + @tablePrefixOnly + 'BLOB_TRIGGERS]
        ADD CONSTRAINT [FK_' + @tablePrefixOnly + '_BLOB_TRIGGERS_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE');

        EXEC('ALTER TABLE [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS]
        ADD CONSTRAINT [FK_' + @tablePrefixOnly + '_TRIGGERS_JOB_DETAILS]
        FOREIGN KEY ([SCHED_NAME], [JOB_NAME], [JOB_GROUP])
        REFERENCES [' + @schema + '].[' + @tablePrefixOnly + 'JOB_DETAILS] ([SCHED_NAME], [JOB_NAME], [JOB_GROUP])');

        -- Indexes
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_JD_RR] ON [' + @schema + '].[' + @tablePrefixOnly + 'JOB_DETAILS] ([REQUESTS_RECOVERY])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_NFT] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([NEXT_FIRE_TIME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_TS] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_NFT_TS] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([NEXT_FIRE_TIME], [TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_GJ] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [JOB_GROUP], [JOB_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_CN] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [CALENDAR_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_TGS] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_GROUP], [TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_TNS] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP], [TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_MNFT] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [MISFIRE_INSTR], [NEXT_FIRE_TIME], [TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_T_MNFTG] ON [' + @schema + '].[' + @tablePrefixOnly + 'TRIGGERS] ([SCHED_NAME], [MISFIRE_INSTR], [NEXT_FIRE_TIME], [TRIGGER_GROUP], [TRIGGER_STATE])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_TN] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([TRIGGER_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_TG] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([TRIGGER_GROUP])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_TNG] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_IN] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([INSTANCE_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_JN] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([JOB_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_JG] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([JOB_GROUP])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_RR] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([REQUESTS_RECOVERY])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_GJ] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([SCHED_NAME], [JOB_GROUP], [JOB_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_FT_GT] ON [' + @schema + '].[' + @tablePrefixOnly + 'FIRED_TRIGGERS] ([SCHED_NAME], [TRIGGER_GROUP], [TRIGGER_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_JT_GJ] ON [' + @schema + '].[' + @tablePrefixOnly + 'JOURNAL_TRIGGERS] ([SCHED_NAME], [JOB_GROUP], [JOB_NAME])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_JT_ST] ON [' + @schema + '].[' + @tablePrefixOnly + 'JOURNAL_TRIGGERS] ([SCHED_NAME], [START_TIME] DESC)');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_JT_S] ON [' + @schema + '].[' + @tablePrefixOnly + 'JOURNAL_TRIGGERS] ([SCHED_NAME], [STATUS])');
        EXEC('CREATE INDEX [IX_' + @schema + '_' + @tablePrefixOnly + '_JT_IN] ON [' + @schema + '].[' + @tablePrefixOnly + 'JOURNAL_TRIGGERS] ([SCHED_NAME], [INSTANCE_NAME])');
        ";
    }
}