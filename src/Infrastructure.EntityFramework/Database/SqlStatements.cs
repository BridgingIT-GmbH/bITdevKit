// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Database;

public static class SqlStatements
{
    public static class SqlServer
    {
        public static string TruncateAllTables(IEnumerable<string> ignoreTables = null)
        {
            ignoreTables = ignoreTables.EmptyToNull();
            ignoreTables ??=
                new List<string>([Guid.Empty.ToString()]); // need one non-empty value to avoid sql syntax error

            return @$"
-- Define the patterns of tables to be excluded using a global temporary table
CREATE TABLE ##ExcludedTablePatterns (TablePattern NVARCHAR(128));

INSERT INTO ##ExcludedTablePatterns (TablePattern) VALUES
    {ignoreTables.Select(t => $"('%{t}%')").ToString(", ")};

-- Disable all constraints
EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT all'

-- Delete data from all tables except the excluded ones
EXEC sp_MSForEachTable '
    DECLARE @FullTableName NVARCHAR(256);
    DECLARE @SQL NVARCHAR(MAX);
    SET @FullTableName = ''?'';
    IF NOT EXISTS (
        SELECT 1
        FROM ##ExcludedTablePatterns
        WHERE @FullTableName LIKE TablePattern
    )
    BEGIN
        PRINT ''Deleting data from '' + @FullTableName;
        SET @SQL = ''SET QUOTED_IDENTIFIER ON; DELETE FROM '' + @FullTableName;
        EXEC sp_executesql @SQL;
    END
'

-- Reseed identity columns for empty tables only
EXEC sp_MSForEachTable '
    DECLARE @FullTableName NVARCHAR(256);
    DECLARE @SQL NVARCHAR(MAX);
    SET @FullTableName = ''?'';
    IF OBJECTPROPERTY(OBJECT_ID(@FullTableName), ''TableHasIdentity'') = 1
    AND NOT EXISTS (
        SELECT 1
        FROM ##ExcludedTablePatterns
        WHERE @FullTableName LIKE TablePattern
    )
    BEGIN
        -- Check if the table is empty
        DECLARE @RowCount INT;
        SET @SQL = ''SELECT @RowCount = COUNT(*) FROM '' + @FullTableName;
        EXEC sp_executesql @SQL, N''@RowCount INT OUTPUT'', @RowCount OUTPUT;

        IF @RowCount = 0
        BEGIN
            PRINT ''Reseeding identity column in '' + @FullTableName;
            SET @SQL = ''SET QUOTED_IDENTIFIER ON; DBCC CHECKIDENT ('' + QUOTENAME(@FullTableName) + '', RESEED, 0)'';
            EXEC sp_executesql @SQL;
        END
    END
'

-- Enable all constraints
EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all'

-- Drop the global temporary table
DROP TABLE ##ExcludedTablePatterns
";
        }
    }

    public static class Sqlite
    {
        public static string TruncateAllTables(IEnumerable<string> ignoreTables = null)
        {
            ignoreTables = ignoreTables.EmptyToNull();
            ignoreTables ??=
                new List<string>([Guid.Empty.ToString()]); // need one non-empty value to avoid sql syntax error

            return @$"
-- Define the patterns of tables to be excluded using a temporary table
CREATE TEMP TABLE ExcludedTablePatterns (TablePattern TEXT);

INSERT INTO ExcludedTablePatterns (TablePattern) VALUES
    {ignoreTables.Select(t => $"('%{t}%')").ToString(", ")};

-- Disable foreign key constraints (SQLite does not have check constraints)
PRAGMA foreign_keys=off;

-- Begin transaction to ensure atomicity
BEGIN TRANSACTION;

-- Delete data from all tables except the excluded ones
SELECT 'DELETE FROM ' || name || ';'
FROM sqlite_master
WHERE type = 'table'
    AND name NOT IN (SELECT name FROM ExcludedTablePatterns)
    AND name NOT LIKE 'sqlite_%'
    AND name NOT LIKE 'sqlite_sequence'
    AND name NOT LIKE '__%' -- Exclude internal tables
UNION ALL
SELECT 'DELETE FROM ' || name || ';'
FROM sqlite_temp_master
WHERE type = 'table'
    AND name NOT IN (SELECT name FROM ExcludedTablePatterns)
    AND name NOT LIKE 'sqlite_%'
    AND name NOT LIKE 'sqlite_sequence'
    AND name NOT LIKE '__%';

-- Execute the delete statements
WITH DeleteStatements AS (
    SELECT 'DELETE FROM ' || name || ';' AS sql_statement
    FROM sqlite_master
    WHERE type = 'table'
    AND name NOT IN (SELECT name FROM ExcludedTablePatterns)
    AND name NOT LIKE 'sqlite_%'
    AND name NOT LIKE 'sqlite_sequence'
    AND name NOT LIKE '__%'
    UNION ALL
    SELECT 'DELETE FROM ' || name || ';'
    FROM sqlite_temp_master
    WHERE type = 'table'
    AND name NOT IN (SELECT name FROM ExcludedTablePatterns)
    AND name NOT LIKE 'sqlite_%'
    AND name NOT LIKE 'sqlite_sequence'
    AND name NOT LIKE '__%'
)
SELECT sql_statement FROM DeleteStatements;

-- Reseeding identity-like behavior is not directly supported in SQLite due to its autoincrement behavior.
-- SQLite handles autoincrement columns differently and automatically.

-- End transaction to commit changes
COMMIT;

-- Enable foreign key constraints (SQLite does not have check constraints)
PRAGMA foreign_keys=on;

-- Drop the temporary table
DROP TABLE ExcludedTablePatterns;
";
        }
    }

    public static class PostgreSQL
    {
        public static string TruncateAllTables(IEnumerable<string> ignoreTables = null)
        {
            ignoreTables = ignoreTables.EmptyToNull();
            ignoreTables ??=
                new List<string>([Guid.Empty.ToString()]);

            return @$"
-- Create temporary table for excluded patterns
CREATE TEMP TABLE IF NOT EXISTS excluded_table_patterns (table_pattern TEXT);

DELETE FROM excluded_table_patterns;
INSERT INTO excluded_table_patterns (table_pattern) VALUES
    {ignoreTables.Select(t => $"('%{t}%')").ToString(", ")};

DO $$
DECLARE
    r RECORD;
    table_full_name TEXT;
    should_exclude BOOLEAN;
    seq_record RECORD;
BEGIN
    -- Disable all triggers (includes FK constraints)
    FOR r IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        table_full_name := quote_ident(r.schemaname) || '.' || quote_ident(r.tablename);
        EXECUTE 'ALTER TABLE ' || table_full_name || ' DISABLE TRIGGER ALL';
    END LOOP;

    -- Delete data from all non-excluded tables
    FOR r IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        table_full_name := quote_ident(r.schemaname) || '.' || quote_ident(r.tablename);

        SELECT EXISTS (
            SELECT 1
            FROM excluded_table_patterns
            WHERE table_full_name LIKE table_pattern
        ) INTO should_exclude;

        IF NOT should_exclude THEN
            RAISE NOTICE 'Deleting data from: %', table_full_name;
            EXECUTE 'DELETE FROM ' || table_full_name;
        END IF;
    END LOOP;

    -- Reset sequences for empty tables
    FOR r IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        table_full_name := quote_ident(r.schemaname) || '.' || quote_ident(r.tablename);

        SELECT EXISTS (
            SELECT 1
            FROM excluded_table_patterns
            WHERE table_full_name LIKE table_pattern
        ) INTO should_exclude;

        IF NOT should_exclude THEN
            -- Reset sequences for this table
            FOR seq_record IN
                SELECT pg_get_serial_sequence(table_full_name, column_name) as seq_name
                FROM information_schema.columns
                WHERE table_schema = r.schemaname
                AND table_name = r.tablename
                AND pg_get_serial_sequence(table_full_name, column_name) IS NOT NULL
            LOOP
                EXECUTE 'ALTER SEQUENCE ' || seq_record.seq_name || ' RESTART WITH 1';
                RAISE NOTICE 'Reset sequence: %', seq_record.seq_name;
            END LOOP;
        END IF;
    END LOOP;

    -- Re-enable all triggers
    FOR r IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
    LOOP
        table_full_name := quote_ident(r.schemaname) || '.' || quote_ident(r.tablename);
        EXECUTE 'ALTER TABLE ' || table_full_name || ' ENABLE TRIGGER ALL';
    END LOOP;
END $$;

DROP TABLE IF EXISTS excluded_table_patterns;
";
        }
    }
}