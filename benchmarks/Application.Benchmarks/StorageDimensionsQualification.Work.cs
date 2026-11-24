// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<object> CaptureDatabaseWorkAsync(IServiceScopeFactory scopes, string engine, bool parentOnly = false)
    {
        await using var scope = scopes.CreateAsyncScope();
        DbContext context = parentOnly ? scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>() : scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        if (engine == "sqlite")
        {
            var database = context.Database.GetDbConnection().DataSource;
            var wal = database + "-wal";
            return new
            {
                WalFileBytes = File.Exists(wal) ? new FileInfo(wal).Length : 0,
                DatabaseFileBytes = File.Exists(database) ? new FileInfo(database).Length : 0,
                Note = "File-length snapshots, not cumulative WAL-write bytes. SQLite may reuse/checkpoint WAL storage."
            };
        }

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = engine == "sqlserver"
            ? "SELECT f.name,f.type_desc,s.num_of_reads,s.num_of_bytes_read,s.io_stall_read_ms,s.num_of_writes,s.num_of_bytes_written,s.io_stall_write_ms FROM sys.dm_io_virtual_file_stats(DB_ID(),NULL) s JOIN sys.database_files f ON f.file_id=s.file_id ORDER BY f.file_id"
            : "SELECT d.xact_commit,d.xact_rollback,d.blks_read,d.blks_hit,d.tup_returned,d.tup_fetched,d.tup_inserted,d.tup_updated,d.tup_deleted,d.temp_files,d.temp_bytes,(SELECT wal_bytes FROM pg_stat_wal) AS owned_cluster_wal_bytes FROM pg_stat_database d WHERE d.datname=current_database()";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync()) rows.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, index => reader.GetValue(index)));
        return new { CapturedAt = DateTimeOffset.UtcNow, Rows = rows, Scope = engine == "sqlserver" ? "Current run-owned catalog" : "Current catalog; WAL counter spans only the owned container cluster" };
    }
}
