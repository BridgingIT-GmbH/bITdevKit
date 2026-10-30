// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// How the internal blob row ids of the seeded table are generated.
/// </summary>
public enum BlobBenchmarkIdStrategy
{
    /// <summary>Random GUIDs (<c>Guid.NewGuid()</c>), the current provider behavior.</summary>
    Random,

    /// <summary>Time ordered GUIDs (<c>Guid.CreateVersion7()</c>).</summary>
    Sequential
}

/// <summary>
/// Which keys a simulated request reads.
/// </summary>
public enum BlobBenchmarkAccess
{
    /// <summary>100 keys sampled across the whole table.</summary>
    Random,

    /// <summary>The 100 most recently written blobs.</summary>
    Recent
}

/// <summary>
/// Whether the SQL Server buffer pool is emptied before each measured iteration.
/// </summary>
public enum BlobBenchmarkCache
{
    /// <summary>The buffer pool is left alone.</summary>
    Warm,

    /// <summary>DBCC DROPCLEANBUFFERS runs before each iteration.</summary>
    Cold
}

/// <summary>
/// Measures how reading a request worth of blobs (100 keys) scales with table size and with the strategy used to
/// generate the internal row ids. The 1M row tables are seeded with SqlBulkCopy and reused between runs.
/// </summary>
/// <remarks>
/// Needs a persistent SQL Server (set BLOB_BENCH_SQL_CONNECTION), otherwise every benchmark process would start a
/// new container and reseed. Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *BlobStorageEfSqlServerLargeTableReadBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class BlobStorageEfSqlServerLargeTableReadBenchmarks
{
    private BlobStorageEfSqlServerLargeTableStore store;
    private BlobKey[] request;

    [Params(1_000, 1_000_000)]
    public int Rows { get; set; }

    [Params(BlobBenchmarkIdStrategy.Random, BlobBenchmarkIdStrategy.Sequential)]
    public BlobBenchmarkIdStrategy Ids { get; set; }

    [Params(BlobBenchmarkAccess.Random, BlobBenchmarkAccess.Recent)]
    public BlobBenchmarkAccess Access { get; set; }

    [Params(BlobBenchmarkCache.Warm, BlobBenchmarkCache.Cold)]
    public BlobBenchmarkCache Cache { get; set; }

    [GlobalSetup]
    public void Setup() =>
        this.store = BlobStorageEfSqlServerLargeTableStore.GetAsync(this.Rows, this.Ids).GetAwaiter().GetResult();

    [IterationSetup]
    public void IterationSetup()
    {
        if (this.Cache == BlobBenchmarkCache.Cold)
        {
            this.store.DropCleanBuffersAsync().GetAwaiter().GetResult();
        }

        this.request = this.store.NextRequest(this.Access);
    }

    [Benchmark(Baseline = true)]
    public Task<long> DevKit_Sequential100() => this.store.ReadSequentialAsync(this.request);

    [Benchmark]
    public Task<long> DevKit_Parallel16() => this.store.ReadParallelAsync(this.request, 16);

    [Benchmark]
    public Task<long> DevKit_Many_Batches4() => this.store.ReadManyAsync(this.request, 16, 4);
}

/// <summary>
/// A large seeded blob database (one chunk per blob) and the DevKit client used to read it.
/// </summary>
internal sealed class BlobStorageEfSqlServerLargeTableStore : IDisposable
{
    private const string ContainerName = "telemetry";
    private const int PayloadBytes = 1024;
    private const int RequestSize = 100;
    private const int RequestSetCount = 64;
    private const int BulkBatchSize = 20_000;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, BlobStorageEfSqlServerLargeTableStore> Stores = [];

    private readonly ServiceProvider serviceProvider;
    private readonly string connectionString;
    private readonly BlobKey[][] randomRequests;
    private readonly BlobKey[] recentRequest;
    private int cursor = -1;

    private BlobStorageEfSqlServerLargeTableStore(
        string connectionString,
        ServiceProvider serviceProvider,
        BlobKey[][] randomRequests,
        BlobKey[] recentRequest)
    {
        this.connectionString = connectionString;
        this.serviceProvider = serviceProvider;
        this.randomRequests = randomRequests;
        this.recentRequest = recentRequest;
        this.Client = new BlobStoreClient(
            EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext>.ProviderName,
            new EntityFrameworkBlobStoreProvider<BenchmarkBlobDbContext>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                new BlobStoreOptions(),
                storeName: "benchmark"));
    }

    public IBlobStoreClient Client { get; }

    public static async Task<BlobStorageEfSqlServerLargeTableStore> GetAsync(int rows, BlobBenchmarkIdStrategy ids)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var name = $"blobbench_large_{rows}_{ids.ToString().ToLowerInvariant()}";
            if (Stores.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var server = await BlobStorageEfSqlServerReadBenchmarkEnvironment.GetServerConnectionStringAsync()
                .ConfigureAwait(false);
            var store = await CreateAsync(server, name, rows, ids).ConfigureAwait(false);
            Stores[name] = store;

            return store;
        }
        finally
        {
            Gate.Release();
        }
    }

    public BlobKey[] NextRequest(BlobBenchmarkAccess access) => access == BlobBenchmarkAccess.Recent
        ? this.recentRequest
        : this.randomRequests[(uint)Interlocked.Increment(ref this.cursor) % RequestSetCount];

    public async Task<long> ReadSequentialAsync(BlobKey[] keys)
    {
        long total = 0;
        foreach (var key in keys)
        {
            total += await this.ReadAsync(key).ConfigureAwait(false);
        }

        return total;
    }

    public async Task<long> ReadParallelAsync(BlobKey[] keys, int parallelism)
    {
        long total = 0;
        await Parallel.ForEachAsync(
            keys,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (key, cancellationToken) =>
                Interlocked.Add(ref total, await this.ReadAsync(key, cancellationToken).ConfigureAwait(false)))
            .ConfigureAwait(false);

        return total;
    }

    public async Task<long> ReadManyAsync(BlobKey[] keys, int parallelism, int maxConcurrentBatches)
    {
        var result = await this.Client.DownloadBytesManyAsync(
            keys,
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

            total += Verify(item.Result.Value.Bytes.Length);
        }

        return total;
    }

    public async Task DropCleanBuffersAsync()
    {
        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "CHECKPOINT; DBCC DROPCLEANBUFFERS;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() => this.serviceProvider.Dispose();

    private static string KeyName(int row) => $"{row % 1000:D4}/{row / 1000:D6}";

    private static int Verify(int length) => length == PayloadBytes
        ? length
        : throw new InvalidOperationException($"Expected {PayloadBytes} bytes but read {length}.");

    private static async Task<BlobStorageEfSqlServerLargeTableStore> CreateAsync(
        string serverConnectionString,
        string databaseName,
        int rows,
        BlobBenchmarkIdStrategy ids)
    {
        var connectionString = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = databaseName,
            MaxPoolSize = 200
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddDbContext<BenchmarkBlobDbContext>(options => options.UseSqlServer(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var random = new Random(42);
        var randomRequests = new BlobKey[RequestSetCount][];
        for (var set = 0; set < RequestSetCount; set++)
        {
            var picked = new HashSet<int>();
            while (picked.Count < RequestSize)
            {
                picked.Add(random.Next(rows));
            }

            randomRequests[set] = picked.Select(row => new BlobKey(ContainerName, KeyName(row))).ToArray();
        }

        var recentRequest = Enumerable.Range(rows - RequestSize, RequestSize)
            .Select(row => new BlobKey(ContainerName, KeyName(row)))
            .ToArray();

        var store = new BlobStorageEfSqlServerLargeTableStore(connectionString, serviceProvider, randomRequests, recentRequest);
        await store.EnsureSeededAsync(databaseName, rows, ids).ConfigureAwait(false);
        await store.ReportAsync(databaseName, rows, ids).ConfigureAwait(false);

        return store;
    }

    private async Task<int> ReadAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.Client.DownloadBytesAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return Verify(result.Value.Bytes.Length);
    }

    private async Task EnsureSeededAsync(string databaseName, int rows, BlobBenchmarkIdStrategy ids)
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BenchmarkBlobDbContext>();

        if (Environment.GetEnvironmentVariable("BLOB_BENCH_RESEED") == "1")
        {
            await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
        }

        await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
        var existing = await dbContext.StorageBlobs.CountAsync().ConfigureAwait(false);
        if (existing == rows)
        {
            Console.WriteLine($"[blob-bench] {databaseName} reusing {existing} seeded blobs");

            return;
        }

        if (existing != 0)
        {
            await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        Console.WriteLine($"[blob-bench] {databaseName} seeding {rows} blobs");
        var watch = Stopwatch.StartNew();

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            // keeps the transaction log small while bulk loading
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET RECOVERY SIMPLE";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var payload = new byte[PayloadBytes];
        new Random(7).NextBytes(payload);
        var contentHash = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(payload))}";
        var containerHash = HashHelper.ComputeSha256(ContainerName);
        var origin = DateTimeOffset.UtcNow.AddDays(-30);

        for (var start = 0; start < rows; start += BulkBatchSize)
        {
            var count = Math.Min(BulkBatchSize, rows - start);
            var blobs = CreateBlobTable();
            var chunks = CreateChunkTable();
            for (var row = start; row < start + count; row++)
            {
                var id = ids == BlobBenchmarkIdStrategy.Sequential
                    ? Guid.CreateVersion7(origin.AddMilliseconds(row)).ToString("N")
                    : Guid.NewGuid().ToString("N");
                var name = KeyName(row);
                var createdAt = origin.AddMilliseconds(row);
                blobs.Rows.Add(
                    id, ContainerName, name, containerHash, HashHelper.ComputeSha256(name), (long)PayloadBytes,
                    "application/octet-stream", contentHash, $"\"{id}\"", createdAt, createdAt, Guid.NewGuid());
                chunks.Rows.Add(id, 0, payload, PayloadBytes);
            }

            await BulkInsertAsync(connection, "__Storage_Blobs", blobs).ConfigureAwait(false);
            await BulkInsertAsync(connection, "__Storage_BlobChunks", chunks).ConfigureAwait(false);

            if ((start + count) % 100_000 == 0)
            {
                Console.WriteLine($"[blob-bench] {databaseName} seeded {start + count}/{rows} in {watch.Elapsed.TotalSeconds:F0}s");
            }
        }

        Console.WriteLine($"[blob-bench] {databaseName} seeded in {watch.Elapsed.TotalSeconds:F1}s");
    }

    private static DataTable CreateBlobTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(string));
        table.Columns.Add("Container", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("ContainerHash", typeof(string));
        table.Columns.Add("NameHash", typeof(string));
        table.Columns.Add("Length", typeof(long));
        table.Columns.Add("ContentTypeMimeType", typeof(string));
        table.Columns.Add("ContentHash", typeof(string));
        table.Columns.Add("ETag", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        table.Columns.Add("LastModifiedAt", typeof(DateTimeOffset));
        table.Columns.Add("ConcurrencyVersion", typeof(Guid));

        return table;
    }

    private static DataTable CreateChunkTable()
    {
        var table = new DataTable();
        table.Columns.Add("BlobId", typeof(string));
        table.Columns.Add("Index", typeof(int));
        table.Columns.Add("Content", typeof(byte[]));
        table.Columns.Add("Length", typeof(int));

        return table;
    }

    private static async Task BulkInsertAsync(SqlConnection connection, string tableName, DataTable table)
    {
        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = 0,
            BatchSize = BulkBatchSize
        };
        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table).ConfigureAwait(false);
    }

    private async Task ReportAsync(string databaseName, int rows, BlobBenchmarkIdStrategy ids)
    {
        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText =
            "SELECT OBJECT_NAME(s.object_id), i.name, s.page_count, s.avg_fragmentation_in_percent, s.avg_page_space_used_in_percent " +
            "FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'SAMPLED') s " +
            "JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id " +
            "WHERE s.index_level = 0 AND OBJECT_NAME(s.object_id) IN ('__Storage_Blobs', '__Storage_BlobChunks') " +
            "ORDER BY 1, 2";
        Console.WriteLine($"[blob-bench] {databaseName} rows={rows} ids={ids} index stats (pages, fragmentation %, page fullness %)");
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            Console.WriteLine(
                $"[blob-bench]   {reader.GetString(0)}.{reader.GetString(1)}: {reader.GetInt64(2)} pages, " +
                $"{reader.GetDouble(3):F1}% fragmented, {reader.GetDouble(4):F1}% full");
        }

        var probe = await this.ReadAsync(this.recentRequest[0]).ConfigureAwait(false);
        Console.WriteLine($"[blob-bench] {databaseName} probe read ok ({probe} bytes)");
    }
}
