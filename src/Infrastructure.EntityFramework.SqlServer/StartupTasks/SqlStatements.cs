// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public static class SqlStatements
{
    public static string QuartzTables(string database, string tablePrefix)
    {
        // source: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_sqlServer.sql
        return $@"
        USE [{database}]
        GO

        DECLARE @tablePrefix NVARCHAR(255) = '{tablePrefix}';
        DECLARE @schema NVARCHAR(255);
        DECLARE @tablePrefixOnly NVARCHAR(255);
        DECLARE @dotPosition INT = CHARINDEX('.', @tablePrefix);
        -- Extract and trim schema
        SET @schema = REPLACE(REPLACE(SUBSTRING(@tablePrefix, 1, @dotPosition - 1), '[', ''), ']', '');
        -- Extract and trim table prefix
        SET @tablePrefixOnly = REPLACE(REPLACE(SUBSTRING(@tablePrefix, @dotPosition + 1, LEN(@tablePrefix) - @dotPosition), '[', ''), ']', '');

        -- Print results for verification
        PRINT 'Full prefix: ' + @tablePrefix;
        PRINT 'Schema: ' + @schema;
        PRINT 'Table prefix: ' + @tablePrefixOnly;

        -- Check existence of any table
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
                @tablePrefixOnly + 'JOB_LISTENERS',
                @tablePrefixOnly + 'SCHEDULER_STATE',
                @tablePrefixOnly + 'LOCKS',
                @tablePrefixOnly + 'TRIGGER_LISTENERS',
                @tablePrefixOnly + 'JOB_DETAILS',
                @tablePrefixOnly + 'SIMPLE_TRIGGERS',
                @tablePrefixOnly + 'SIMPROP_TRIGGERS',
                @tablePrefixOnly + 'TRIGGERS'
            )
        )
        BEGIN
            PRINT 'One or more QRTZ tables already exist. Aborting script.';
            -- RAISERROR('One or more QRTZ tables already exist. Aborting script.', 16, 1);
            RETURN;
        END
        GO

        -- Create the tables, explained here https://stackoverflow.com/a/52048642/1758814
        CREATE TABLE {tablePrefix}CALENDARS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [CALENDAR_NAME] nvarchar(200) NOT NULL,
          [CALENDAR] varbinary(max) NOT NULL
        );
        GO

        CREATE TABLE {tablePrefix}CRON_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [CRON_EXPRESSION] nvarchar(120) NOT NULL,
          [TIME_ZONE_ID] nvarchar(80)
        );
        GO

        CREATE TABLE {tablePrefix}FIRED_TRIGGERS (
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
          [REQUESTS_RECOVERY] bit NULL
        );
        GO

        CREATE TABLE {tablePrefix}PAUSED_TRIGGER_GRPS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL
        );
        GO

        CREATE TABLE {tablePrefix}SCHEDULER_STATE (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [INSTANCE_NAME] nvarchar(200) NOT NULL,
          [LAST_CHECKIN_TIME] bigint NOT NULL,
          [CHECKIN_INTERVAL] bigint NOT NULL
        );
        GO

        CREATE TABLE {tablePrefix}LOCKS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [LOCK_NAME] nvarchar(40) NOT NULL
        );
        GO

        CREATE TABLE {tablePrefix}JOB_DETAILS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [JOB_NAME] nvarchar(150) NOT NULL,
          [JOB_GROUP] nvarchar(150) NOT NULL,
          [DESCRIPTION] nvarchar(250) NULL,
          [JOB_CLASS_NAME] nvarchar(250) NOT NULL,
          [IS_DURABLE] bit NOT NULL,
          [IS_NONCONCURRENT] bit NOT NULL,
          [IS_UPDATE_DATA] bit NOT NULL,
          [REQUESTS_RECOVERY] bit NOT NULL,
          [JOB_DATA] varbinary(max) NULL
        );
        GO

        CREATE TABLE {tablePrefix}SIMPLE_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [REPEAT_COUNT] int NOT NULL,
          [REPEAT_INTERVAL] bigint NOT NULL,
          [TIMES_TRIGGERED] int NOT NULL
        );
        GO

        CREATE TABLE {tablePrefix}SIMPROP_TRIGGERS (
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
          [TIME_ZONE_ID] nvarchar(80) NULL
        );
        GO

        CREATE TABLE {tablePrefix}BLOB_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [BLOB_DATA] varbinary(max) NULL
        );
        GO

        CREATE TABLE {tablePrefix}TRIGGERS (
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
          [JOB_DATA] varbinary(max) NULL
        );
        GO

        -- custom table to store the triger history
        CREATE TABLE {tablePrefix}JOURNAL_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [ENTRY_ID] nvarchar(140) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [JOB_NAME] nvarchar(150) NOT NULL,
          [JOB_GROUP] nvarchar(150) NOT NULL,
          [DESCRIPTION] nvarchar(250) NULL,
          [FIRE_TIME] bigint NULL,
          [TRIGGER_STATE] nvarchar(16) NOT NULL,
          [TRIGGER_TYPE] nvarchar(8) NOT NULL,
          [START_TIME] bigint NOT NULL,
          [END_TIME] bigint NULL,
          [CALENDAR_NAME] nvarchar(200) NULL,
          [JOB_DATA] varbinary(max) NULL
        );
        GO

        ALTER TABLE {tablePrefix}CALENDARS WITH NOCHECK ADD
          CONSTRAINT [PK_' + @tablePrefixOnly + '_CALENDARS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [CALENDAR_NAME]
          );
        GO

        ALTER TABLE {tablePrefix}CRON_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_CRON_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}FIRED_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_FIRED_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [ENTRY_ID]
          );
        GO

        ALTER TABLE {tablePrefix}PAUSED_TRIGGER_GRPS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_PAUSED_TRIGGER_GRPS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}SCHEDULER_STATE WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_SCHEDULER_STATE] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [INSTANCE_NAME]
          );
        GO

        ALTER TABLE {tablePrefix}LOCKS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_LOCKS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [LOCK_NAME]
          );
        GO

        ALTER TABLE {tablePrefix}JOB_DETAILS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_JOB_DETAILS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [JOB_NAME],
            [JOB_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}SIMPLE_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_SIMPLE_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}SIMPROP_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_SIMPROP_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}BLOB_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_BLOB_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}CRON_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_CRON_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES {tablePrefix}TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;
        GO

        ALTER TABLE {tablePrefix}SIMPLE_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPLE_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES {tablePrefix}TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;
        GO

        ALTER TABLE {tablePrefix}SIMPROP_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPROP_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES {tablePrefix}TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;
        GO

        ALTER TABLE {tablePrefix}TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_TRIGGERS_QRTZ_JOB_DETAILS] FOREIGN KEY
          (
            [SCHED_NAME],
            [JOB_NAME],
            [JOB_GROUP]
          ) REFERENCES {tablePrefix}JOB_DETAILS (
            [SCHED_NAME],
            [JOB_NAME],
            [JOB_GROUP]
          );
        GO

        ALTER TABLE {tablePrefix}JOURNAL_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_JOURNAL_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [ENTRY_ID]
          );
        GO

        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_G_J]                 ON {tablePrefix}TRIGGERS(SCHED_NAME, JOB_GROUP, JOB_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_C]                   ON {tablePrefix}TRIGGERS(SCHED_NAME, CALENDAR_NAME);

        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_N_G_STATE]           ON {tablePrefix}TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_STATE);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_STATE]               ON {tablePrefix}TRIGGERS(SCHED_NAME, TRIGGER_STATE);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_N_STATE]             ON {tablePrefix}TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, TRIGGER_STATE);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NEXT_FIRE_TIME]      ON {tablePrefix}TRIGGERS(SCHED_NAME, NEXT_FIRE_TIME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST]              ON {tablePrefix}TRIGGERS(SCHED_NAME, TRIGGER_STATE, NEXT_FIRE_TIME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST_MISFIRE]      ON {tablePrefix}TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_STATE);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST_MISFIRE_GRP]  ON {tablePrefix}TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_GROUP, TRIGGER_STATE);

        CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_INST_JOB_REQ_RCVRY] ON {tablePrefix}FIRED_TRIGGERS(SCHED_NAME, INSTANCE_NAME, REQUESTS_RECOVERY);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_G_J]                ON {tablePrefix}FIRED_TRIGGERS(SCHED_NAME, JOB_GROUP, JOB_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_G_T]                ON {tablePrefix}FIRED_TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_NAME);

        CREATE INDEX [IDX_' + @tablePrefixOnly + '_JT_G_J]                ON {tablePrefix}JOURNAL_TRIGGERS(SCHED_NAME, JOB_GROUP, JOB_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_JT_J]                  ON {tablePrefix}JOURNAL_TRIGGERS(JOB_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_JT_G_T]                ON {tablePrefix}JOURNAL_TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_JT_T]                  ON {tablePrefix}JOURNAL_TRIGGERS(TRIGGER_NAME);
        CREATE INDEX [IDX_' + @tablePrefixOnly + '_JT_FT]                 ON {tablePrefix}JOURNAL_TRIGGERS(SCHED_NAME, FIRE_TIME);
        GO".Replace("GO", string.Empty);
        // GO not allowed in sql statements https://stackoverflow.com/questions/18596876/go-statements-blowing-up-sql-execution-in-net
    }
}