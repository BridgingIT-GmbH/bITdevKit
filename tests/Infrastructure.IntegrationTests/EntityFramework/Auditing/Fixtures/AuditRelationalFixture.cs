// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

// Deliberately independent of the repository's broad environment fixture.
// Only these two disposable database containers are started.
public sealed class AuditRelationalFixture : IAsyncLifetime
{
    private readonly MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly List<string> sqliteFiles = [];

    public Task InitializeAsync() => this.StartAsync();

    public async Task StartAsync()
    {
        await this.sqlServer.StartAsync();
        await this.postgres.StartAsync();
    }

    public async Task<Func<DbConnection>> CreateDatabaseAsync(string backend)
    {
        var database = $"audit_proof_{Guid.NewGuid():N}";
        switch (backend)
        {
            case "SqlServer":
                await using (var connection = new SqlConnection(this.sqlServer.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"CREATE DATABASE [{database}]";
                    await command.ExecuteNonQueryAsync();
                }

                var sqlOptions = new SqlConnectionStringBuilder(this.sqlServer.GetConnectionString()) { InitialCatalog = database };
                return () => new SqlConnection(sqlOptions.ConnectionString);
            case "Postgres":
                await using (var connection = new NpgsqlConnection(this.postgres.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"CREATE DATABASE {database}";
                    await command.ExecuteNonQueryAsync();
                }

                var pgOptions = new NpgsqlConnectionStringBuilder(this.postgres.GetConnectionString()) { Database = database };
                return () => new NpgsqlConnection(pgOptions.ConnectionString);
            case "Sqlite":
                var path = Path.Combine(Path.GetTempPath(), $"{database}.db");
                this.sqliteFiles.Add(path);
                return () => new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Pooling = false,
                    DefaultTimeout = 10
                }.ConnectionString);
            default:
                throw new ArgumentOutOfRangeException(nameof(backend));
        }
    }

    public async Task<Func<DbConnection>> BackupRestoreCopyAsync(string backend, Func<DbConnection> sourceFactory)
    {
        var targetFactory = await this.CreateDatabaseAsync(backend);
        await using var source = sourceFactory(); await using var target = targetFactory();
        if (backend == "Sqlite")
        {
            await source.OpenAsync(); await target.OpenAsync();
            ((SqliteConnection)source).BackupDatabase((SqliteConnection)target);
            return targetFactory;
        }

        var sourceName = source.Database; var targetName = target.Database;
        if (!sourceName.StartsWith("audit_proof_", StringComparison.Ordinal) || !targetName.StartsWith("audit_proof_", StringComparison.Ordinal)) throw new InvalidOperationException("Native restore requires fixture-owned databases.");
        if (backend == "Postgres")
        {
            var user = new NpgsqlConnectionStringBuilder(this.postgres.GetConnectionString()).Username;
            var file = "/tmp/audit_backup_" + Guid.NewGuid().ToString("N") + ".dump";
            var dump = await this.postgres.ExecAsync(["pg_dump", "--format=custom", "--username=" + user, "--dbname=" + sourceName, "--file=" + file]);
            if (dump.ExitCode != 0) throw new InvalidOperationException("Test-owned pg_dump failed: " + dump.Stderr);
            var restore = await this.postgres.ExecAsync(["pg_restore", "--exit-on-error", "--no-owner", "--no-privileges", "--username=" + user, "--dbname=" + targetName, file]);
            if (restore.ExitCode != 0) throw new InvalidOperationException("Test-owned pg_restore failed: " + restore.Stderr);
            await this.postgres.ExecAsync(["rm", "-f", file]);
            return targetFactory;
        }

        await source.OpenAsync();
        var files = new List<(string Name, int Type)>();
        await using (var list = source.CreateCommand())
        {
            list.CommandText = "SELECT name,type FROM sys.database_files";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync()) files.Add((reader.GetString(0), reader.GetByte(1)));
        }

        await source.CloseAsync();
        await using var administrator = new SqlConnection(this.sqlServer.GetConnectionString()); await administrator.OpenAsync();
        var backupFile = "/var/opt/mssql/data/audit_backup_" + Guid.NewGuid().ToString("N") + ".bak";
        await using var command = administrator.CreateCommand(); command.CommandTimeout = 60;
        command.Parameters.AddWithValue("@backup", backupFile);
        command.CommandText = $"BACKUP DATABASE [{sourceName}] TO DISK=@backup WITH COPY_ONLY, INIT";
        await command.ExecuteNonQueryAsync();
        var moves = string.Join(",", files.Select(file => $"MOVE N'{file.Name.Replace("'", "''")}' TO N'/var/opt/mssql/data/{targetName}_{file.Type}.{(file.Type == 1 ? "ldf" : "mdf")}'"));
        command.CommandText = $"ALTER DATABASE [{targetName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"; await command.ExecuteNonQueryAsync();
        command.CommandText = $"RESTORE DATABASE [{targetName}] FROM DISK=@backup WITH REPLACE,{moves}"; await command.ExecuteNonQueryAsync();
        command.CommandText = $"ALTER DATABASE [{targetName}] SET MULTI_USER"; await command.ExecuteNonQueryAsync();
        return targetFactory;
    }

    public async Task DisposeAsync()
    {
        await this.postgres.DisposeAsync();
        await this.sqlServer.DisposeAsync();
        foreach (var path in this.sqliteFiles)
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }
}

[CollectionDefinition(nameof(AuditRelationalCollection), DisableParallelization = true)]
public sealed class AuditRelationalCollection : ICollectionFixture<AuditRelationalFixture>;