// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Data.Common;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

/// <summary>Runs storage regressions in a disposable relational database.</summary>
/// <example><code>await StorageReliabilityDatabase.RunAsync("sqlite", (services, connection) => Task.CompletedTask);</code></example>
public static class StorageReliabilityDatabase
{
    /// <summary>Creates the storage model, runs the test, and disposes its database.</summary>
    /// <example><code>await StorageReliabilityDatabase.RunAsync("sqlserver", TestAsync, pooled: true);</code></example>
    public static async Task RunAsync(string engine, Func<IServiceProvider, string, Task> test,
        bool pooled = false, bool binary = false, bool retries = false, IInterceptor[] interceptors = null)
    {
        await using var sql = engine == "sqlserver" ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("MSSQL_COLLATION", binary ? "Latin1_General_100_BIN2" : "SQL_Latin1_General_CP1_CI_AS").Build() : null;
        await using var postgres = engine == "postgres" ? new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("storage_reliability").Build() : null;
        var sqlite = Path.Combine(Path.GetTempPath(), $"bdk-storage-reliability-{Guid.NewGuid():N}.db");
        if (sql is not null) await sql.StartAsync();
        if (postgres is not null) await postgres.StartAsync();

        var connection = engine switch
        {
            "sqlserver" => new SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = "storage_reliability" }.ConnectionString,
            "postgres" => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Options = "-c lock_timeout=500ms" }.ConnectionString,
            "sqlite" => $"Data Source={sqlite};Pooling=False;Default Timeout=1",
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };
        var registration = new ServiceCollection();
        void Configure(DbContextOptionsBuilder options)
        {
            if (interceptors is not null) options.AddInterceptors(interceptors);
            if (sql is not null)
                options.UseSqlServer(connection, settings => { if (retries) settings.EnableRetryOnFailure(); });
            else if (postgres is not null)
                options.UseNpgsql(connection);
            else
                options.UseSqlite(connection);
        }

        if (pooled) registration.AddDbContextPool<Context>(Configure, poolSize: 4);
        else registration.AddDbContext<Context>(Configure);

        registration.AddSingleton<IContinuationTokenProtector>(new HmacContinuationTokenProtector(new byte[32]));
        registration.AddBlobStorage()
            .WithEntityFrameworkClient<Context>("reliability", options =>
            {
                options.ChunkSize = 4;
                options.AllowFullScans = true;
            });

        try
        {
            await using var services = registration.BuildServiceProvider();
            await using (var scope = services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<Context>().Database.EnsureCreatedAsync();

            await test(services, connection);
        }
        finally
        {
            File.Delete(sqlite);
            File.Delete(sqlite + "-wal");
            File.Delete(sqlite + "-shm");
        }
    }

    /// <summary>Maps both storage features for the relational regressions.</summary>
    /// <example><code>var context = new StorageReliabilityDatabase.Context(options);</code></example>
    public sealed class Context(DbContextOptions<Context> options) : DbContext(options), IBlobStoreContext, IDocumentStoreContext
    {
        /// <inheritdoc />
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        /// <inheritdoc />
        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }

        /// <inheritdoc />
        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }

    /// <summary>Throws after one real database commit.</summary>
    /// <example><code>var fault = new StorageReliabilityDatabase.CommitFault { LoseNextCommit = true };</code></example>
    public sealed class CommitFault : DbTransactionInterceptor
    {
        /// <summary>Gets the number of started transactions.</summary>
        /// <example><code>fault.Started.ShouldBe(1);</code></example>
        public int Started { get; private set; }

        /// <summary>Gets the number of completed database commits.</summary>
        /// <example><code>fault.Committed.ShouldBe(1);</code></example>
        public int Committed { get; private set; }

        /// <summary>Enables a timeout after the next real commit.</summary>
        /// <example><code>fault.LoseNextCommit = true;</code></example>
        public bool LoseNextCommit { get; set; }

        /// <inheritdoc />
        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result, CancellationToken cancellationToken = default)
        {
            this.Started++;
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            this.Committed++;
            if (this.LoseNextCommit)
            {
                this.LoseNextCommit = false;
                throw new TimeoutException("The test lost acknowledgement after the database committed.");
            }

            return Task.CompletedTask;
        }
    }
}
