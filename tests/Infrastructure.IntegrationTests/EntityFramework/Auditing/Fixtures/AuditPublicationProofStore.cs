// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;

// Test-only proof of the explicitly approved singleton-control-table protocol.
// Production mapping and provider conformance remain separate implementation gates.
internal sealed class AuditPublicationProofStore(string backend, Func<DbConnection> connectionFactory)
{
    internal async Task InitializeAsync()
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        var textType = backend == "SqlServer" ? "nvarchar(max)" : "text";
        await ExecuteAsync(connection, null, $"""
            CREATE TABLE audit_records (
                id varchar(32) NOT NULL PRIMARY KEY,
                position bigint NOT NULL UNIQUE,
                completed_ticks bigint NOT NULL,
                archived integer NOT NULL,
                document {textType} NOT NULL);
            CREATE TABLE audit_actions (
                id varchar(32) NOT NULL PRIMARY KEY,
                record_id varchar(32) NOT NULL REFERENCES audit_records(id),
                document {textType} NOT NULL);
            CREATE TABLE audit_changes (
                id varchar(32) NOT NULL PRIMARY KEY,
                record_id varchar(32) NOT NULL REFERENCES audit_records(id),
                document {textType} NOT NULL);
            CREATE TABLE audit_control (
                singleton integer NOT NULL PRIMARY KEY CHECK (singleton = 1),
                store_id varchar(32) NOT NULL,
                epoch varchar(32) NOT NULL,
                position bigint NOT NULL,
                generation bigint NOT NULL,
                removed_through bigint NOT NULL,
                reject_before_ticks bigint NOT NULL);
            """);
        await ExecuteAsync(connection, null, """
            INSERT INTO audit_control
                (singleton, store_id, epoch, position, generation, removed_through, reject_before_ticks)
            VALUES (1, @store, @epoch, 0, 0, 0, 0)
            """, ("store", Guid.NewGuid().ToString("N")), ("epoch", Guid.NewGuid().ToString("N")));
    }

    internal async Task<long> AppendGraphAsync(
        string id,
        long completedTicks,
        Func<Task> beforeCommit = null,
        Action beforeGate = null)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        beforeGate?.Invoke();
        await using var transaction = await this.AcquireAsync(connection);
        var state = await ReadStateAsync(connection, transaction);
        if (completedTicks < state.RejectBeforeTicks)
        {
            throw new InvalidOperationException("EvidenceExpired");
        }

        var position = checked(state.Position + 1);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO audit_records (id, position, completed_ticks, archived, document)
            VALUES (@id, @position, @completed, 0, @document)
            """, ("id", id), ("position", position), ("completed", completedTicks), ("document", "{\"kind\":\"root\"}"));
        await ExecuteAsync(connection, transaction,
            "INSERT INTO audit_actions (id, record_id, document) VALUES (@id, @record, @document)",
            ("id", Guid.NewGuid().ToString("N")), ("record", id), ("document", "{\"kind\":\"action\"}"));
        await ExecuteAsync(connection, transaction,
            "INSERT INTO audit_changes (id, record_id, document) VALUES (@id, @record, @document)",
            ("id", Guid.NewGuid().ToString("N")), ("record", id), ("document", "{\"kind\":\"change\"}"));
        await ExecuteAsync(connection, transaction, "UPDATE audit_control SET position = @position WHERE singleton = 1", ("position", position));
        if (beforeCommit is not null)
        {
            await beforeCommit();
        }

        await transaction.CommitAsync();
        return position;
    }

    internal async Task<ProofWindow> OpenWindowAsync(bool all = false, Action beforeGate = null)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        beforeGate?.Invoke();
        await using var transaction = await this.AcquireAsync(connection);
        var state = await ReadStateAsync(connection, transaction);
        await transaction.CommitAsync();
        return new ProofWindow(state, 0, all);
    }

    internal async Task<IReadOnlyList<ProofRecord>> ReadWindowAsync(ProofWindow window)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var transaction = await this.AcquireAsync(connection);
        var state = await ReadStateAsync(connection, transaction);
        Validate(window, state);
        await using var command = CreateCommand(connection, transaction, """
            SELECT id, position, archived FROM audit_records
            WHERE position > @lower AND position <= @upper
            """ + (window.All ? "" : " AND archived = 0") + " ORDER BY position",
            ("lower", window.Lower), ("upper", window.State.Position));
        var records = new List<ProofRecord>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                records.Add(new ProofRecord(reader.GetString(0), reader.GetInt64(1), Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0));
            }
        }

        await transaction.CommitAsync();
        return records;
    }

    internal async Task ArchiveAsync(long cutoff)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var transaction = await this.AcquireAsync(connection);
        var affected = await ExecuteAsync(connection, transaction,
            "UPDATE audit_records SET archived = 1 WHERE archived = 0 AND completed_ticks < @cutoff", ("cutoff", cutoff));
        if (affected > 0)
        {
            await ExecuteAsync(connection, transaction, "UPDATE audit_control SET generation = generation + 1 WHERE singleton = 1");
        }

        await transaction.CommitAsync();
    }

    internal async Task RemoveAsync(long cutoff)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var transaction = await this.AcquireAsync(connection);
        var state = await ReadStateAsync(connection, transaction);
        await using var command = CreateCommand(connection, transaction,
            "SELECT MAX(position) FROM audit_records WHERE archived = 1 AND completed_ticks < @cutoff", ("cutoff", cutoff));
        var highest = await command.ExecuteScalarAsync();
        if (highest is not null and not DBNull)
        {
            var boundary = Math.Max(state.RemovedThrough, Convert.ToInt64(highest, CultureInfo.InvariantCulture));
            foreach (var table in new[] { "audit_actions", "audit_changes" })
            {
                await ExecuteAsync(connection, transaction,
                    $"DELETE FROM {table} WHERE record_id IN (SELECT id FROM audit_records WHERE archived = 1 AND completed_ticks < @cutoff)", ("cutoff", cutoff));
            }

            await ExecuteAsync(connection, transaction, "DELETE FROM audit_records WHERE archived = 1 AND completed_ticks < @cutoff", ("cutoff", cutoff));
            await ExecuteAsync(connection, transaction, """
                UPDATE audit_control SET removed_through = @boundary,
                reject_before_ticks = @cutoff, generation = generation + 1 WHERE singleton = 1
                """, ("boundary", boundary), ("cutoff", Math.Max(cutoff, state.RejectBeforeTicks)));
        }

        await transaction.CommitAsync();
    }

    internal async Task RotateEpochAfterRestoreAsync()
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var transaction = await this.AcquireAsync(connection);
        await ExecuteAsync(connection, transaction,
            "UPDATE audit_control SET epoch = @epoch WHERE singleton = 1", ("epoch", Guid.NewGuid().ToString("N")));
        await transaction.CommitAsync();
    }

    internal async Task<ProofState> VerifyDurableCoordinationAsync()
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var transaction = await this.AcquireAsync(connection);
        var state = await ReadStateAsync(connection, transaction);
        await transaction.CommitAsync();
        return state;
    }

    internal async Task<IReadOnlyList<string>> ReadTablesAsync()
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        var sql = backend switch
        {
            "SqlServer" => "SELECT name FROM sys.tables WHERE name LIKE 'audit[_]%' ORDER BY name",
            "Postgres" => "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name LIKE 'audit_%' ORDER BY table_name",
            _ => "SELECT name FROM sqlite_schema WHERE type = 'table' AND name LIKE 'audit_%' ORDER BY name"
        };
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    internal async Task<long> CountGraphRowsAsync()
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM audit_records) + (SELECT COUNT(*) FROM audit_actions) + (SELECT COUNT(*) FROM audit_changes)";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static void Validate(ProofWindow window, ProofState state)
    {
        if (window.State.StoreId != state.StoreId || window.State.Epoch != state.Epoch)
        {
            throw new InvalidOperationException("EpochChanged");
        }

        if (state.RemovedThrough > window.State.RemovedThrough && window.Lower < state.RemovedThrough)
        {
            throw new InvalidOperationException("HistoryGap");
        }

        if (!window.All && window.State.Generation != state.Generation)
        {
            throw new InvalidOperationException("LifecycleChanged");
        }
    }

    private async Task<DbTransaction> AcquireAsync(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
        {
            return sqlite.BeginTransaction(deferred: false);
        }

        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            if (backend == "SqlServer")
            {
                await ExecuteAsync(connection, transaction, """
                    DECLARE @result integer;
                    EXEC @result = sys.sp_getapplock @Resource = 'bdk.audit.publication.proof',
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @result < 0 THROW 51000, 'AuditPublicationLockUnavailable', 1;
                    """);
            }
            else
            {
                await ExecuteAsync(connection, transaction, "SELECT pg_advisory_xact_lock(6617094476251901)");
            }

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static async Task<ProofState> ReadStateAsync(DbConnection connection, DbTransaction transaction)
    {
        await using var command = CreateCommand(connection, transaction, """
            SELECT store_id, epoch, position, generation, removed_through, reject_before_ticks
            FROM audit_control WHERE singleton = 1
            """);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("AuditControlMissing");
        }

        return new ProofState(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5));
    }

    private static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 15;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}

internal sealed record ProofState(string StoreId, string Epoch, long Position, long Generation, long RemovedThrough, long RejectBeforeTicks);

internal sealed record ProofWindow(ProofState State, long Lower, bool All);

internal sealed record ProofRecord(string Id, long Position, bool Archived);