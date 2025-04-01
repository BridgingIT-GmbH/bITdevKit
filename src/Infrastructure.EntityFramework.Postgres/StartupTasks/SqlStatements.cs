// Source: https://github.com/quartznet/quartznet/blob/main/database/tables/tables_postgres.sql
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
        DO $$
        DECLARE
            table_prefix TEXT := '{tablePrefix}';
            schema_name TEXT;
            table_prefix_only TEXT;
            dot_position INT;
            table_exists BOOLEAN;
        BEGIN
            -- Extract schema and table prefix
            dot_position := POSITION('.' IN table_prefix);
            IF dot_position > 0 THEN
                schema_name := TRIM(BOTH '[]' FROM SUBSTRING(table_prefix FROM 1 FOR dot_position - 1));
                table_prefix_only := TRIM(BOTH '[]' FROM SUBSTRING(table_prefix FROM dot_position + 1));
            ELSE
                schema_name := 'public';
                table_prefix_only := TRIM(BOTH '[]' FROM table_prefix);
            END IF;

            RAISE NOTICE 'Full prefix: %', table_prefix;
            RAISE NOTICE 'Schema: %', schema_name;
            RAISE NOTICE 'Table prefix: %', table_prefix_only;

            -- Check if any Quartz tables already exist
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = schema_name
                AND table_name IN (
                    lower(table_prefix_only || 'calendars'),
                    lower(table_prefix_only || 'cron_triggers'),
                    lower(table_prefix_only || 'blob_triggers'),
                    lower(table_prefix_only || 'fired_triggers'),
                    lower(table_prefix_only || 'paused_trigger_grps'),
                    lower(table_prefix_only || 'scheduler_state'),
                    lower(table_prefix_only || 'locks'),
                    lower(table_prefix_only || 'job_details'),
                    lower(table_prefix_only || 'simple_triggers'),
                    lower(table_prefix_only || 'simprop_triggers'),
                    lower(table_prefix_only || 'triggers'),
                    lower(table_prefix_only || 'journal_triggers')
                )
            ) INTO table_exists;

            IF table_exists THEN
                RAISE NOTICE 'One or more Quartz tables already exist. Aborting script.';
                RETURN;
            END IF;

            -- Create tables
            EXECUTE format('
                CREATE TABLE %I.%Ijob_details (
                    sched_name TEXT NOT NULL,
                    job_name TEXT NOT NULL,
                    job_group TEXT NOT NULL,
                    description TEXT,
                    job_class_name TEXT NOT NULL,
                    is_durable BOOL NOT NULL,
                    is_nonconcurrent BOOL NOT NULL,
                    is_update_data BOOL NOT NULL,
                    requests_recovery BOOL NOT NULL,
                    job_data BYTEA,
                    PRIMARY KEY (sched_name, job_name, job_group)
                );', schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Itriggers (
                    sched_name TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    job_name TEXT NOT NULL,
                    job_group TEXT NOT NULL,
                    description TEXT,
                    next_fire_time BIGINT,
                    prev_fire_time BIGINT,
                    priority INTEGER,
                    trigger_state TEXT NOT NULL,
                    trigger_type TEXT NOT NULL,
                    start_time BIGINT NOT NULL,
                    end_time BIGINT,
                    calendar_name TEXT,
                    misfire_instr SMALLINT,
                    job_data BYTEA,
                    PRIMARY KEY (sched_name, trigger_name, trigger_group),
                    FOREIGN KEY (sched_name, job_name, job_group)
                        REFERENCES %I.%Ijob_details (sched_name, job_name, job_group)
                );', schema_name, table_prefix_only, schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Isimple_triggers (
                    sched_name TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    repeat_count BIGINT NOT NULL,
                    repeat_interval BIGINT NOT NULL,
                    times_triggered BIGINT NOT NULL,
                    PRIMARY KEY (sched_name, trigger_name, trigger_group),
                    FOREIGN KEY (sched_name, trigger_name, trigger_group)
                        REFERENCES %I.%Itriggers (sched_name, trigger_name, trigger_group) ON DELETE CASCADE
                );', schema_name, table_prefix_only, schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Isimprop_triggers (
                    sched_name TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    str_prop_1 TEXT,
                    str_prop_2 TEXT,
                    str_prop_3 TEXT,
                    int_prop_1 INTEGER,
                    int_prop_2 INTEGER,
                    long_prop_1 BIGINT,
                    long_prop_2 BIGINT,
                    dec_prop_1 NUMERIC(13,4),
                    dec_prop_2 NUMERIC(13,4),
                    bool_prop_1 BOOL,
                    bool_prop_2 BOOL,
                    time_zone_id TEXT,
                    PRIMARY KEY (sched_name, trigger_name, trigger_group),
                    FOREIGN KEY (sched_name, trigger_name, trigger_group)
                        REFERENCES %I.%Itriggers (sched_name, trigger_name, trigger_group) ON DELETE CASCADE
                );', schema_name, table_prefix_only, schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Icron_triggers (
                    sched_name TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    cron_expression TEXT NOT NULL,
                    time_zone_id TEXT,
                    PRIMARY KEY (sched_name, trigger_name, trigger_group),
                    FOREIGN KEY (sched_name, trigger_name, trigger_group)
                        REFERENCES %I.%Itriggers (sched_name, trigger_name, trigger_group) ON DELETE CASCADE
                );', schema_name, table_prefix_only, schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Iblob_triggers (
                    sched_name TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    blob_data BYTEA,
                    PRIMARY KEY (sched_name, trigger_name, trigger_group),
                    FOREIGN KEY (sched_name, trigger_name, trigger_group)
                        REFERENCES %I.%Itriggers (sched_name, trigger_name, trigger_group) ON DELETE CASCADE
                );', schema_name, table_prefix_only, schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Icalendars (
                    sched_name TEXT NOT NULL,
                    calendar_name TEXT NOT NULL,
                    calendar BYTEA NOT NULL,
                    PRIMARY KEY (sched_name, calendar_name)
                );', schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Ipaused_trigger_grps (
                    sched_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    PRIMARY KEY (sched_name, trigger_group)
                );', schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Ifired_triggers (
                    sched_name TEXT NOT NULL,
                    entry_id TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    instance_name TEXT NOT NULL,
                    fired_time BIGINT NOT NULL,
                    sched_time BIGINT NOT NULL,
                    priority INTEGER NOT NULL,
                    state TEXT NOT NULL,
                    job_name TEXT,
                    job_group TEXT,
                    is_nonconcurrent BOOL,
                    requests_recovery BOOL,
                    PRIMARY KEY (sched_name, entry_id)
                );', schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Ischeduler_state (
                    sched_name TEXT NOT NULL,
                    instance_name TEXT NOT NULL,
                    last_checkin_time BIGINT NOT NULL,
                    checkin_interval BIGINT NOT NULL,
                    PRIMARY KEY (sched_name, instance_name)
                );', schema_name, table_prefix_only);

            EXECUTE format('
                CREATE TABLE %I.%Ilocks (
                    sched_name TEXT NOT NULL,
                    lock_name TEXT NOT NULL,
                    PRIMARY KEY (sched_name, lock_name)
                );', schema_name, table_prefix_only);

            -- custom table to store the triger history
            EXECUTE format('
                CREATE TABLE %I.%Ijournal_triggers (
                    sched_name TEXT NOT NULL,
                    entry_id TEXT NOT NULL,
                    trigger_name TEXT NOT NULL,
                    trigger_group TEXT NOT NULL,
                    job_name TEXT NOT NULL,
                    job_group TEXT NOT NULL,
                    description TEXT,
                    start_time TIMESTAMP NOT NULL,
                    end_time TIMESTAMP,
                    scheduled_time TIMESTAMP NOT NULL,
                    duration_ms BIGINT,
                    status TEXT NOT NULL,
                    error_message TEXT,
                    job_data_json TEXT,
                    instance_name TEXT,
                    priority INTEGER,
                    result TEXT,
                    retry_count INTEGER NOT NULL DEFAULT 0,
                    category TEXT,
                    PRIMARY KEY (sched_name, entry_id)
                );', schema_name, table_prefix_only);

            -- Create indexes
            EXECUTE format('
                CREATE INDEX idx_%s_j_req_recovery ON %I.%Ijob_details (requests_recovery);
                CREATE INDEX idx_%s_t_next_fire_time ON %I.%Itriggers (next_fire_time);
                CREATE INDEX idx_%s_t_state ON %I.%Itriggers (trigger_state);
                CREATE INDEX idx_%s_t_nft_st ON %I.%Itriggers (next_fire_time, trigger_state);
                CREATE INDEX idx_%s_t_g_j ON %I.%Itriggers (sched_name, job_group, job_name);
                CREATE INDEX idx_%s_t_c ON %I.%Itriggers (sched_name, calendar_name);
                CREATE INDEX idx_%s_t_n_g_state ON %I.%Itriggers (sched_name, trigger_group, trigger_state);
                CREATE INDEX idx_%s_t_n_state ON %I.%Itriggers (sched_name, trigger_name, trigger_group, trigger_state);
                CREATE INDEX idx_%s_t_nft_st_misfire ON %I.%Itriggers (sched_name, misfire_instr, next_fire_time, trigger_state);
                CREATE INDEX idx_%s_t_nft_st_misfire_grp ON %I.%Itriggers (sched_name, misfire_instr, next_fire_time, trigger_group, trigger_state);
                CREATE INDEX idx_%s_ft_trig_name ON %I.%Ifired_triggers (trigger_name);
                CREATE INDEX idx_%s_ft_trig_group ON %I.%Ifired_triggers (trigger_group);
                CREATE INDEX idx_%s_ft_trig_nm_gp ON %I.%Ifired_triggers (sched_name, trigger_name, trigger_group);
                CREATE INDEX idx_%s_ft_trig_inst_name ON %I.%Ifired_triggers (instance_name);
                CREATE INDEX idx_%s_ft_job_name ON %I.%Ifired_triggers (job_name);
                CREATE INDEX idx_%s_ft_job_group ON %I.%Ifired_triggers (job_group);
                CREATE INDEX idx_%s_ft_job_req_recovery ON %I.%Ifired_triggers (requests_recovery);
                CREATE INDEX idx_%s_ft_g_j ON %I.%Ifired_triggers (sched_name, job_group, job_name);
                CREATE INDEX idx_%s_ft_g_t ON %I.%Ifired_triggers (sched_name, trigger_group, trigger_name);
                CREATE INDEX idx_%s_jt_g_j ON %I.%Ijournal_triggers (sched_name, job_group, job_name);
                CREATE INDEX idx_%s_jt_st ON %I.%Ijournal_triggers (sched_name, start_time DESC);
                CREATE INDEX idx_%s_jt_status ON %I.%Ijournal_triggers (sched_name, status);
                CREATE INDEX idx_%s_jt_inst ON %I.%Ijournal_triggers (sched_name, instance_name);
            ', table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only,
               table_prefix_only, schema_name, table_prefix_only);

        END $$;
        ";
    }
}