// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using System.Data.Common;
using System.Diagnostics;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

/// <summary>
/// Hosts the shared SQL Server and the seeded blob stores used by the EF blob read benchmarks.
/// By default a SQL Server 2022 container is started once per process (requires Docker). Set
/// BLOB_BENCH_SQL_CONNECTION to a SQL Server connection string to benchmark an existing server instead, for example
/// Azure SQL. Set BLOB_BENCH_RESEED=1 to drop and reseed the benchmark databases.
/// </summary>
internal static class BlobStorageEfSqlServerReadBenchmarkEnvironment
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<int, BlobStorageEfSqlServerReadBenchmarkStore> Stores = [];
    private static MsSqlContainer container;
    private static string serverConnectionString;

    public static async Task<BlobStorageEfSqlServerReadBenchmarkStore> GetStoreAsync(int payloadBytes)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Stores.TryGetValue(payloadBytes, out var existing))
            {
                return existing;
            }

            await EnsureServerAsync().ConfigureAwait(false);
            var store = await BlobStorageEfSqlServerReadBenchmarkStore.CreateAsync(serverConnectionString, payloadBytes)
                .ConfigureAwait(false);
            Stores[payloadBytes] = store;

            return store;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task EnsureServerAsync()
    {
        if (serverConnectionString is not null)
        {
            return;
        }

        var external = Environment.GetEnvironmentVariable("BLOB_BENCH_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            serverConnectionString = external;
            Console.WriteLine("[blob-bench] using external SQL Server from BLOB_BENCH_SQL_CONNECTION");

            return;
        }

        Console.WriteLine("[blob-bench] starting SQL Server container");
        container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync().ConfigureAwait(false);
        serverConnectionString = container.GetConnectionString();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            foreach (var store in Stores.Values)
            {
                store.Dispose();
            }

            container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
    }
}

/// <summary>
/// A seeded blob database containing <c>id/day</c> keyed blobs of one payload size, plus the clients used to read it.
/// </summary>
internal sealed class BlobStorageEfSqlServerReadBenchmarkStore : IDisposable
{
    private const string ContainerName = "telemetry";
    private const long SeedBudgetBytes = 256L * 1024 * 1024;
    private const int IdCount = 20;
    private const int RequestSize = 100;

    private readonly ServiceProvider serviceProvider;
    private readonly SqlQueryCounter counter;
    private readonly (string ContainerHash, string NameHash)[] requestHashes;

    private BlobStorageEfSqlServerReadBenchmarkStore(
        string connectionString,
        int payloadBytes,
        ServiceProvider serviceProvider,
        SqlQueryCounter counter,
        BlobKey[] request)
    {
        this.ConnectionString = connectionString;
        this.PayloadBytes = payloadBytes;
        this.serviceProvider = serviceProvider;
        this.counter = counter;
        this.Request = request;
        this.Client = new BlobStoreClient(
            EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext>.ProviderName,
            new EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                new BlobStoreOptions(),
                storeName: "benchmark"));
        this.requestHashes = request
            .Select(key => (HashHelper.ComputeSha256(key.Container), HashHelper.ComputeSha256(key.Name)))
            .ToArray();
    }

    public string ConnectionString { get; }

    public int PayloadBytes { get; }

    public IBlobStoreClient Client { get; }

    /// <summary>
    /// Gets the keys read by one simulated request (a random sample of the seeded blobs).
    /// </summary>
    public BlobKey[] Request { get; }

    public int SqlQueriesPerRead { get; private set; }

    public static async Task<BlobStorageEfSqlServerReadBenchmarkStore> CreateAsync(
        string serverConnectionString,
        int payloadBytes)
    {
        var blobCount = (int)Math.Clamp(SeedBudgetBytes / payloadBytes, 100, 1000);
        blobCount -= blobCount % IdCount;
        var dayCount = blobCount / IdCount;

        var builder = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = $"blobbench_{payloadBytes}",
            MaxPoolSize = 200
        };
        var connectionString = builder.ConnectionString;

        var counter = new SqlQueryCounter();
        var services = new ServiceCollection();
        services.AddDbContext<BenchmarkBlobDbContext>(options => options
            .UseSqlServer(connectionString)
            .AddInterceptors(counter));
        var serviceProvider = services.BuildServiceProvider();

        var ids = Enumerable.Range(0, IdCount)
            .Select(i => new Guid(i + 1, 0, 0, new byte[8]).ToString("N"))
            .ToArray();
        var start = new DateTime(2026, 1, 1);
        var keys = ids
            .SelectMany(id => Enumerable.Range(0, dayCount)
                .Select(day => new BlobKey(ContainerName, $"{id}/{start.AddDays(day):yyyy-MM-dd}")))
            .ToArray();
        var random = new Random(42);
        var request = keys.OrderBy(_ => random.Next()).Take(Math.Min(RequestSize, keys.Length)).ToArray();

        var store = new BlobStorageEfSqlServerReadBenchmarkStore(
            connectionString,
            payloadBytes,
            serviceProvider,
            counter,
            request);
        await store.EnsureSeededAsync(keys).ConfigureAwait(false);
        await store.ProbeAsync(keys.Length).ConfigureAwait(false);

        return store;
    }

    /// <summary>
    /// Reads one blob through the DevKit client and returns its length.
    /// </summary>
    public async Task<int> ReadAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.Client.DownloadBytesAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return this.Verify(result.Value.Bytes.Length);
    }

    /// <summary>
    /// Reads one blob with a hand-written single SQL query and returns its length. This is the lower bound
    /// that the DevKit read path is compared against.
    /// </summary>
    public async Task<int> ReadRawSqlAsync(int requestIndex, CancellationToken cancellationToken = default)
    {
        var (containerHash, nameHash) = this.requestHashes[requestIndex];
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT c.[Content] FROM [__Storage_Blobs] b " +
            "INNER JOIN [__Storage_BlobChunks] c ON c.[BlobId] = b.[Id] AND c.[Index] = 0 " +
            "WHERE b.[ContainerHash] = @containerHash AND b.[NameHash] = @nameHash";
        command.Parameters.Add(new SqlParameter("@containerHash", SqlDbType.NVarChar, 64) { Value = containerHash });
        command.Parameters.Add(new SqlParameter("@nameHash", SqlDbType.NVarChar, 64) { Value = nameHash });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Blob row not found by raw SQL.");
        }

        var content = await reader.GetFieldValueAsync<byte[]>(0, cancellationToken).ConfigureAwait(false);

        return this.Verify(content.Length);
    }

    /// <summary>
    /// Reads the request keys one after the other.
    /// </summary>
    public async Task<long> ReadRequestSequentialAsync(bool rawSql)
    {
        long total = 0;
        for (var index = 0; index < this.Request.Length; index++)
        {
            total += rawSql
                ? await this.ReadRawSqlAsync(index).ConfigureAwait(false)
                : await this.ReadAsync(this.Request[index]).ConfigureAwait(false);
        }

        return total;
    }

    /// <summary>
    /// Reads the request keys with bounded parallelism.
    /// </summary>
    public async Task<long> ReadRequestParallelAsync(int parallelism, bool rawSql)
    {
        long total = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, this.Request.Length),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (index, cancellationToken) =>
            {
                var length = rawSql
                    ? await this.ReadRawSqlAsync(index, cancellationToken).ConfigureAwait(false)
                    : await this.ReadAsync(this.Request[index], cancellationToken).ConfigureAwait(false);
                Interlocked.Add(ref total, length);
            }).ConfigureAwait(false);

        return total;
    }

    /// <summary>
    /// Reads the request keys through the DevKit bulk API, which batches the keys on the EF provider.
    /// </summary>
    public async Task<long> ReadRequestManyAsync(int parallelism, int maxConcurrentBatches)
    {
        var result = await this.Client.DownloadBytesManyAsync(
            this.Request,
            new BlobBytesDownloadManyOptions
            {
                MaxDegreeOfParallelism = parallelism,
                MaxConcurrentBatches = maxConcurrentBatches
            }).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        long total = 0;
        foreach (var item in result.Value)
        {
            if (item.Result.IsFailure)
            {
                throw new InvalidOperationException($"Blob '{item.Key}' could not be read.");
            }

            total += this.Verify(item.Result.Value.Bytes.Length);
        }

        return total;
    }

    /// <summary>
    /// Reads the request keys with hand-written multi-key SQL queries (one query per batch, batches run in
    /// parallel). This is the ceiling a provider level multi-get could reach.
    /// </summary>
    public async Task<long> ReadRequestRawSqlBatchAsync(int batchSize, int parallelism)
    {
        long total = 0;
        var batches = Enumerable.Range(0, this.requestHashes.Length)
            .Chunk(batchSize)
            .ToArray();
        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (batch, cancellationToken) =>
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                var names = batch.Select((requestIndex, i) => (Parameter: $"@n{i}", Hash: this.requestHashes[requestIndex].NameHash)).ToArray();
                command.CommandText =
                    "SELECT b.[NameHash], c.[Content] FROM [__Storage_Blobs] b " +
                    "INNER JOIN [__Storage_BlobChunks] c ON c.[BlobId] = b.[Id] AND c.[Index] = 0 " +
                    $"WHERE b.[ContainerHash] = @containerHash AND b.[NameHash] IN ({string.Join(", ", names.Select(n => n.Parameter))})";
                command.Parameters.Add(new SqlParameter("@containerHash", SqlDbType.NVarChar, 64) { Value = this.requestHashes[batch[0]].ContainerHash });
                foreach (var (parameter, hash) in names)
                {
                    command.Parameters.Add(new SqlParameter(parameter, SqlDbType.NVarChar, 64) { Value = hash });
                }

                var rows = 0;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var content = await reader.GetFieldValueAsync<byte[]>(1, cancellationToken).ConfigureAwait(false);
                    Interlocked.Add(ref total, this.Verify(content.Length));
                    rows++;
                }

                if (rows != batch.Length)
                {
                    throw new InvalidOperationException($"Expected {batch.Length} rows but read {rows}.");
                }
            }).ConfigureAwait(false);

        return total;
    }

    /// <summary>
    /// Empties the SQL Server buffer pool so the next reads hit the disk. Needs sysadmin permissions.
    /// </summary>
    public async Task DropCleanBuffersAsync()
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "CHECKPOINT; DBCC DROPCLEANBUFFERS;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() => this.serviceProvider.Dispose();

    private int Verify(int length) => length == this.PayloadBytes
        ? length
        : throw new InvalidOperationException($"Expected {this.PayloadBytes} bytes but read {length}.");

    private async Task EnsureSeededAsync(BlobKey[] keys)
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BenchmarkBlobDbContext>();

        if (Environment.GetEnvironmentVariable("BLOB_BENCH_RESEED") == "1")
        {
            await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
        }

        await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
        var existing = await dbContext.StorageBlobs.CountAsync().ConfigureAwait(false);
        if (existing == keys.Length)
        {
            Console.WriteLine($"[blob-bench] payload={this.PayloadBytes} reusing {existing} seeded blobs");

            return;
        }

        if (existing != 0)
        {
            await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        Console.WriteLine($"[blob-bench] payload={this.PayloadBytes} seeding {keys.Length} blobs");
        var watch = Stopwatch.StartNew();
        var index = 0;
        await Parallel.ForEachAsync(
            keys,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (key, cancellationToken) =>
            {
                var bytes = new byte[this.PayloadBytes];
                new Random(Interlocked.Increment(ref index)).NextBytes(bytes);
                var result = await this.Client.UploadBytesAsync(key, bytes, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException(
                        string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
                }
            }).ConfigureAwait(false);
        Console.WriteLine($"[blob-bench] payload={this.PayloadBytes} seeded in {watch.Elapsed.TotalSeconds:F1}s");
    }

    private async Task ProbeAsync(int blobCount)
    {
        this.counter.Reset();
        await this.ReadAsync(this.Request[0]).ConfigureAwait(false);
        this.SqlQueriesPerRead = this.counter.Count;
        Console.WriteLine(
            $"[blob-bench] payload={this.PayloadBytes} blobs={blobCount} request={this.Request.Length} " +
            $"sqlQueriesPerRead={this.SqlQueriesPerRead}");
    }

    private sealed class SqlQueryCounter : DbCommandInterceptor
    {
        private int count;

        public int Count => Volatile.Read(ref this.count);

        public void Reset() => Volatile.Write(ref this.count, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.count);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

internal sealed class BenchmarkBlobDbContext(DbContextOptions<BenchmarkBlobDbContext> options)
    : DbContext(options), IBlobStoreContext
{
    public DbSet<StorageBlob> StorageBlobs { get; set; }

    public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
}
