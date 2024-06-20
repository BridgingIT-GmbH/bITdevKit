
        USE [bit_devkit_dinnerfiesta]


        DECLARE @tablePrefix NVARCHAR(255) = '[dbo].QRTZ33333_';
        DECLARE @schema NVARCHAR(255);
        DECLARE @tablePrefixOnly NVARCHAR(255);

        -- Find the position of the dot
        DECLARE @dotPosition INT = CHARINDEX('.', @tablePrefix);

        -- Extract and trim schema
        SET @schema = REPLACE(REPLACE(SUBSTRING(@tablePrefix, 1, @dotPosition - 1), '[', ''), ']', '');

        -- Extract and trim table prefix
        SET @tablePrefixOnly = REPLACE(REPLACE(SUBSTRING(@tablePrefix, @dotPosition + 1, LEN(@tablePrefix) - @dotPosition), '[', ''), ']', '');

        -- Print results for verification
        PRINT 'Full prefix: ' + @tablePrefix;
        PRINT 'Schema: ' + @schema;
        PRINT 'Table prefix: ' + @tablePrefixOnly;

        -- Check existence of tables
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


        CREATE TABLE [dbo].QRTZ33333_CALENDARS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [CALENDAR_NAME] nvarchar(200) NOT NULL,
          [CALENDAR] varbinary(max) NOT NULL
        );


        CREATE TABLE [dbo].QRTZ33333_CRON_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [CRON_EXPRESSION] nvarchar(120) NOT NULL,
          [TIME_ZONE_ID] nvarchar(80)
        );


        CREATE TABLE [dbo].QRTZ33333_FIRED_TRIGGERS (
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


        CREATE TABLE [dbo].QRTZ33333_PAUSED_TRIGGER_GRPS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL
        );


        CREATE TABLE [dbo].QRTZ33333_SCHEDULER_STATE (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [INSTANCE_NAME] nvarchar(200) NOT NULL,
          [LAST_CHECKIN_TIME] bigint NOT NULL,
          [CHECKIN_INTERVAL] bigint NOT NULL
        );


        CREATE TABLE [dbo].QRTZ33333_LOCKS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [LOCK_NAME] nvarchar(40) NOT NULL
        );


        CREATE TABLE [dbo].QRTZ33333_JOB_DETAILS (
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


        CREATE TABLE [dbo].QRTZ33333_SIMPLE_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [REPEAT_COUNT] int NOT NULL,
          [REPEAT_INTERVAL] bigint NOT NULL,
          [TIMES_TRIGGERED] int NOT NULL
        );


        CREATE TABLE [dbo].QRTZ33333_SIMPROP_TRIGGERS (
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


        CREATE TABLE [dbo].QRTZ33333_BLOB_TRIGGERS (
          [SCHED_NAME] nvarchar(120) NOT NULL,
          [TRIGGER_NAME] nvarchar(150) NOT NULL,
          [TRIGGER_GROUP] nvarchar(150) NOT NULL,
          [BLOB_DATA] varbinary(max) NULL
        );


        CREATE TABLE [dbo].QRTZ33333_TRIGGERS (
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


        ALTER TABLE [dbo].QRTZ33333_CALENDARS WITH NOCHECK ADD
          CONSTRAINT [PK_' + @tablePrefixOnly + '_CALENDARS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [CALENDAR_NAME]
          );


        ALTER TABLE [dbo].QRTZ33333_CRON_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_CRON_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );


        ALTER TABLE [dbo].QRTZ33333_FIRED_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_FIRED_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [ENTRY_ID]
          );


        ALTER TABLE [dbo].QRTZ33333_BLOB_TRIGGERS WITH NOCHECK ADD
          CONSTRAINT [PK' + @tablePrefixOnly + '_BLOB_TRIGGERS] PRIMARY KEY  CLUSTERED
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          );


        ALTER TABLE [dbo].QRTZ33333_CRON_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_CRON_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES [dbo].QRTZ33333_TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;


        ALTER TABLE [dbo].QRTZ33333_SIMPLE_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPLE_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES [dbo].QRTZ33333_TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;


        ALTER TABLE [dbo].QRTZ33333_SIMPROP_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_SIMPROP_TRIGGERS_QRTZ_TRIGGERS] FOREIGN KEY
          (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) REFERENCES [dbo].QRTZ33333_TRIGGERS (
            [SCHED_NAME],
            [TRIGGER_NAME],
            [TRIGGER_GROUP]
          ) ON DELETE CASCADE;


        ALTER TABLE [dbo].QRTZ33333_TRIGGERS ADD
          CONSTRAINT [FK_' + @tablePrefixOnly + '_TRIGGERS_QRTZ_JOB_DETAILS] FOREIGN KEY
          (
            [SCHED_NAME],
            [JOB_NAME],
            [JOB_GROUP]
          ) REFERENCES [dbo].QRTZ33333_JOB_DETAILS (
            [SCHED_NAME],
            [JOB_NAME],
            [JOB_GROUP]
          );


        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_G_J]                 ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, JOB_GROUP, JOB_NAME);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_C]                   ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, CALENDAR_NAME);

        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_N_G_STATE]           ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_STATE);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_STATE]               ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, TRIGGER_STATE);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_N_STATE]             ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, TRIGGER_STATE);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NEXT_FIRE_TIME]      ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, NEXT_FIRE_TIME);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST]              ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, TRIGGER_STATE, NEXT_FIRE_TIME);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST_MISFIRE]      ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_STATE);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_T_NFT_ST_MISFIRE_GRP]  ON [dbo].QRTZ33333_TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_GROUP, TRIGGER_STATE);

        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_INST_JOB_REQ_RCVRY] ON [dbo].QRTZ33333_FIRED_TRIGGERS(SCHED_NAME, INSTANCE_NAME, REQUESTS_RECOVERY);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_G_J]                ON [dbo].QRTZ33333_FIRED_TRIGGERS(SCHED_NAME, JOB_GROUP, JOB_NAME);
        -- CREATE INDEX [IDX_' + @tablePrefixOnly + '_FT_G_T]                ON [dbo].QRTZ33333_FIRED_TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_NAME);



        