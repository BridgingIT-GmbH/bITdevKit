// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Data;
using System.Diagnostics;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Data.SqlClient;

/// <summary>Which part of the key order a paging benchmark starts at.</summary>
public enum DocumentBenchmarkPageDepth
{
    /// <summary>The first page, no continuation token.</summary>
    First,

    /// <summary>A page in the middle of the table.</summary>
    Middle,

    /// <summary>A page at 90 percent of the table.</summary>
    Deep
}

/// <summary>
/// A bulk seeded document table (1 KB documents over 20 partitions with sequential row keys) and the DevKit access to it.
/// The rows are copies of one row written through the client, so only the key and id columns differ.
/// </summary>
internal sealed class DocumentStorageEfSqlServerLargeTableStore
{
    private const int PayloadBytes = 1024;
    private const int RequestSize = 100;
    private const int RequestSetCount = 64;
    private const int BulkBatchSize = 20_000;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, DocumentStorageEfSqlServerLargeTableStore> Stores = [];

    private readonly DocumentKey[][] randomRequests;
    private readonly DocumentKey[] recentRequest;
    private readonly DocumentTypeIdentity type = DocumentTypeIdentity.For<BenchmarkDocument>();
    private int cursor = -1;

    private DocumentStorageEfSqlServerLargeTableStore(
        DocumentBenchmarkDatabase database,
        int rows,
        DocumentKey[][] randomRequests,
        DocumentKey[] recentRequest)
    {
        this.Database = database;
        this.Rows = rows;
        this.randomRequests = randomRequests;
        this.recentRequest = recentRequest;
        this.Client = database.CreateClient<BenchmarkDocument>();
    }

    public DocumentBenchmarkDatabase Database { get; }

    public int Rows { get; }

    public IDocumentStoreClient<BenchmarkDocument> Client { get; }

    public static async Task<DocumentStorageEfSqlServerLargeTableStore> GetAsync(int rows, DocumentBenchmarkIdStrategy ids)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var name = DocumentStorageEfSqlServerBenchmarkEnvironment.DatabaseName($"docbench_large_{rows}_{ids.ToString().ToLowerInvariant()}");
            if (Stores.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var server = await DocumentStorageEfSqlServerBenchmarkEnvironment.GetServerConnectionStringAsync().ConfigureAwait(false);
            var database = DocumentBenchmarkDatabase.Create<BenchmarkDocumentDbContext>(server, name);
            var store = await CreateAsync(database, rows, ids).ConfigureAwait(false);
            Stores[name] = store;

            return store;
        }
        finally
        {
            Gate.Release();
        }
    }

    public DocumentKey[] NextRequest(DocumentBenchmarkAccess access) => access == DocumentBenchmarkAccess.Recent
        ? this.recentRequest
        : this.randomRequests[(uint)Interlocked.Increment(ref this.cursor) % RequestSetCount];

    public async Task<long> ReadSequentialAsync(DocumentKey[] keys)
    {
        long total = 0;
        foreach (var key in keys)
        {
            total += await this.ReadAsync(key).ConfigureAwait(false);
        }

        return total;
    }

    public async Task<long> ReadParallelAsync(DocumentKey[] keys, int parallelism)
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

#if !DOC_BASELINE
    public async Task<long> ReadManyAsync(DocumentKey[] keys)
    {
        var result = await this.Client.GetManyAsync(keys).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Count != keys.Length)
        {
            throw new InvalidOperationException("The documents could not be read.");
        }

        long total = 0;
        foreach (var item in result.Value)
        {
            total += Verify(item.Value.Value.Value.Length);
        }

        return total;
    }
#endif

    /// <summary>Reads one page of full documents through the provider, starting after the document at <paramref name="depth" />.</summary>
    public async Task<int> FindPageAsync(DocumentBenchmarkPageDepth depth)
    {
        var result = await this.Database.Provider.FindPageAsync(this.type, this.PageQuery(depth), DateTimeOffset.UtcNow).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Items.Count != RequestSize)
        {
            throw new InvalidOperationException("The page could not be read.");
        }

        return result.Value.Items.Count;
    }

    /// <summary>Reads one page of keys through the provider, starting after the document at <paramref name="depth" />.</summary>
    public async Task<int> ListPageAsync(DocumentBenchmarkPageDepth depth)
    {
        var result = await this.Database.Provider.ListPageAsync(this.type, this.PageQuery(depth), DateTimeOffset.UtcNow).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Items.Count != RequestSize)
        {
            throw new InvalidOperationException("The page could not be read.");
        }

        return result.Value.Items.Count;
    }

    private static int Verify(int length) => length == PayloadBytes
        ? length
        : throw new InvalidOperationException($"Expected {PayloadBytes} characters but read {length}.");

    private static Guid CreateId(DocumentBenchmarkIdStrategy ids, int row) => ids == DocumentBenchmarkIdStrategy.SqlServerSequential
        ? Guid.Parse($"00000000-0000-0000-0000-{row:x12}")
        : Guid.NewGuid();

    private static async Task<DocumentStorageEfSqlServerLargeTableStore> CreateAsync(
        DocumentBenchmarkDatabase database,
        int rows,
        DocumentBenchmarkIdStrategy ids)
    {
        var random = new Random(42);
        var randomRequests = new DocumentKey[RequestSetCount][];
        for (var set = 0; set < RequestSetCount; set++)
        {
            var picked = new HashSet<int>();
            while (picked.Count < RequestSize)
            {
                picked.Add(random.Next(rows));
            }

            randomRequests[set] = picked.Select(DocumentBenchmarkDatabase.KeyFor).ToArray();
        }

        var recentRequest = Enumerable.Range(rows - RequestSize, RequestSize).Select(DocumentBenchmarkDatabase.KeyFor).ToArray();
        var store = new DocumentStorageEfSqlServerLargeTableStore(database, rows, randomRequests, recentRequest);
        if (!await database.PrepareAsync(rows).ConfigureAwait(false))
        {
            await store.SeedAsync(ids).ConfigureAwait(false);
        }

        await database.ReportIndexesAsync().ConfigureAwait(false);
        var probe = await store.ReadAsync(recentRequest[0]).ConfigureAwait(false);
        Console.WriteLine($"[doc-bench] {database.Name} rows={rows} ids={ids} probe read ok ({probe} characters)");

        return store;
    }

    private DocumentQuery PageQuery(DocumentBenchmarkPageDepth depth)
    {
        var builder = DocumentQueries.Query().AllowFullScan().Take(RequestSize);
        if (depth == DocumentBenchmarkPageDepth.First)
        {
            return builder.Build();
        }

        // The rows are ordered by (PartitionKey, RowKey): position n is partition n / perPartition, row n % perPartition.
        var perPartition = this.Rows / 20;
        var position = (depth == DocumentBenchmarkPageDepth.Middle ? this.Rows / 2 : this.Rows / 10 * 9) - 1;
        var key = new DocumentKey($"p{position / perPartition:D2}", $"r{position % perPartition:D7}");
        var token = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = this.type.Value,
            QueryHash = "benchmark",
            VisibilityCutoff = DateTimeOffset.UtcNow,
            NativeToken = JsonSerializer.Serialize(key)
        });

        return builder.ContinueWith(token.Value).Build();
    }

    private async Task<int> ReadAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.Client.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return Verify(result.Value.Value.Value.Length);
    }

    private async Task SeedAsync(DocumentBenchmarkIdStrategy ids)
    {
        Console.WriteLine($"[doc-bench] {this.Database.Name} seeding {this.Rows} documents");
        var watch = Stopwatch.StartNew();

        // keeps the transaction log small while bulk loading
        await this.Database.ExecuteAsync($"ALTER DATABASE [{this.Database.Name}] SET RECOVERY SIMPLE").ConfigureAwait(false);
        var template = await DocumentBenchmarkTemplate.CreateAsync(this.Database, this.Client).ConfigureAwait(false);
        var origin = DateTimeOffset.UtcNow.AddDays(-30);
        for (var start = 0; start < this.Rows; start += BulkBatchSize)
        {
            var count = Math.Min(BulkBatchSize, this.Rows - start);
            var table = template.CreateTable();
            for (var row = start; row < start + count; row++)
            {
                template.AddRow(table, CreateId(ids, row), DocumentBenchmarkDatabase.KeyFor(row), origin.AddMilliseconds(row), null);
            }

            await this.Database.BulkInsertAsync(table).ConfigureAwait(false);
            if ((start + count) % 100_000 == 0)
            {
                Console.WriteLine($"[doc-bench] {this.Database.Name} seeded {start + count}/{this.Rows} in {watch.Elapsed.TotalSeconds:F0}s");
            }
        }

        Console.WriteLine($"[doc-bench] {this.Database.Name} seeded in {watch.Elapsed.TotalSeconds:F1}s");
    }
}

/// <summary>A document row written through the client and read back raw, used to bulk copy new rows.</summary>
internal sealed class DocumentBenchmarkTemplate
{
    private readonly string[] names;
    private readonly Type[] types;
    private readonly object[] values;

    private DocumentBenchmarkTemplate(string[] names, Type[] types, object[] values)
    {
        this.names = names;
        this.types = types;
        this.values = values;
    }

    public static async Task<DocumentBenchmarkTemplate> CreateAsync(DocumentBenchmarkDatabase database, IDocumentStoreClient<BenchmarkDocument> client)
    {
        var key = new DocumentKey("template", "template");
        var result = await client.UpsertAsync(key, new BenchmarkDocument { Value = DocumentBenchmarkDatabase.CreatePayload(1024, 7) }).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException("The template document could not be written.");
        }

        var (names, types, values) = await database.ReadRawRowAsync(key).ConfigureAwait(false);
        await database.DeleteRowAsync(key).ConfigureAwait(false);

        return new DocumentBenchmarkTemplate(names, types, values);
    }

    public DataTable CreateTable()
    {
        var table = new DataTable();
        for (var index = 0; index < this.names.Length; index++)
        {
            table.Columns.Add(this.names[index], this.types[index]);
        }

        return table;
    }

    public void AddRow(DataTable table, Guid id, DocumentKey key, DateTimeOffset createdAt, long? expiresAtUnixMilliseconds)
    {
        var row = (object[])this.values.Clone();
        row[this.Index("Id")] = id;
        row[this.Index("PartitionKey")] = key.PartitionKey;
        row[this.Index("PartitionKeyHash")] = HashHelper.ComputeSha256(key.PartitionKey);
        row[this.Index("RowKey")] = key.RowKey;
        row[this.Index("RowKeyHash")] = HashHelper.ComputeSha256(key.RowKey);
        row[this.Index("ConcurrencyVersion")] = Guid.NewGuid();
        row[this.Index("CreatedDate")] = createdAt;
        row[this.Index("ExpiresAtUnixMilliseconds")] = expiresAtUnixMilliseconds is null ? DBNull.Value : expiresAtUnixMilliseconds.Value;
        table.Rows.Add(row);
    }

    private int Index(string name) => Array.IndexOf(this.names, name);
}

/// <summary>
/// Measures how reading a request worth of documents (100 keys) scales with table size and with the strategy used to
/// generate the internal row ids. The 1M row tables are seeded with SqlBulkCopy and reused between runs.
/// </summary>
/// <remarks>
/// Needs a persistent SQL Server (set DOC_BENCH_SQL_CONNECTION), otherwise every benchmark process would start a
/// new container and reseed. Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerLargeTableReadBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerLargeTableReadBenchmarks
{
    private DocumentStorageEfSqlServerLargeTableStore store;
    private DocumentKey[] request;

    [Params(1_000, 1_000_000)]
    public int Rows { get; set; }

    [Params(DocumentBenchmarkIdStrategy.Random, DocumentBenchmarkIdStrategy.SqlServerSequential)]
    public DocumentBenchmarkIdStrategy Ids { get; set; }

    [Params(DocumentBenchmarkAccess.Random, DocumentBenchmarkAccess.Recent)]
    public DocumentBenchmarkAccess Access { get; set; }

    [Params(DocumentBenchmarkCache.Warm, DocumentBenchmarkCache.Cold)]
    public DocumentBenchmarkCache Cache { get; set; }

    [GlobalSetup]
    public void Setup() =>
        this.store = DocumentStorageEfSqlServerLargeTableStore.GetAsync(this.Rows, this.Ids).GetAwaiter().GetResult();

    [IterationSetup]
    public void IterationSetup()
    {
        if (this.Cache == DocumentBenchmarkCache.Cold)
        {
            this.store.Database.DropCleanBuffersAsync().GetAwaiter().GetResult();
        }

        this.request = this.store.NextRequest(this.Access);
    }

    [Benchmark(Baseline = true)]
    public Task<long> DevKit_Sequential100() => this.store.ReadSequentialAsync(this.request);

    [Benchmark]
    public Task<long> DevKit_Parallel16() => this.store.ReadParallelAsync(this.request, 16);

#if !DOC_BASELINE
    [Benchmark]
    public Task<long> DevKit_GetMany() => this.store.ReadManyAsync(this.request);
#endif
}

/// <summary>
/// Measures the latency of one page (100 documents or keys) at the start, the middle and the end of a large table.
/// The latency of a keyset page must not depend on the depth.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerPagingBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerPagingBenchmarks
{
    private DocumentStorageEfSqlServerLargeTableStore store;

    [Params(1_000, 1_000_000)]
    public int Rows { get; set; }

    [Params(DocumentBenchmarkPageDepth.First, DocumentBenchmarkPageDepth.Middle, DocumentBenchmarkPageDepth.Deep)]
    public DocumentBenchmarkPageDepth Depth { get; set; }

    [GlobalSetup]
    public void Setup() =>
        this.store = DocumentStorageEfSqlServerLargeTableStore.GetAsync(this.Rows, DocumentBenchmarkIdStrategy.Random).GetAwaiter().GetResult();

    [Benchmark(Baseline = true)]
    public Task<int> FindPage() => this.store.FindPageAsync(this.Depth);

    [Benchmark]
    public Task<int> ListPage() => this.store.ListPageAsync(this.Depth);
}

/// <summary>
/// Measures inserting 1000 new 1 KB documents into a large table. The provider generates the row ids, which are
/// random before sequential ids and increasing afterwards, so the same benchmark shows the effect of the id change.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerInsertBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerInsertBenchmarks
{
    private const int Inserts = 1000;

    private DocumentStorageEfSqlServerLargeTableStore store;
    private IDocumentStoreClient<BenchmarkWriteDocument> client;
    private DocumentKey[] keys;
    private int iteration;

    [Params(1_000, 1_000_000)]
    public int Rows { get; set; }

    [Params(DocumentBenchmarkIdStrategy.Random, DocumentBenchmarkIdStrategy.SqlServerSequential)]
    public DocumentBenchmarkIdStrategy Ids { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.store = DocumentStorageEfSqlServerLargeTableStore.GetAsync(this.Rows, this.Ids).GetAwaiter().GetResult();
        this.store.Database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
        this.client = this.store.Database.CreateClient<BenchmarkWriteDocument>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.store.Database.ReportIndexesAsync().GetAwaiter().GetResult();
        this.store.Database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var run = Interlocked.Increment(ref this.iteration);
        this.keys = Enumerable.Range(0, Inserts).Select(index => new DocumentKey($"n{index % 20:D2}", $"i{run}-{index}")).ToArray();
    }

    [Benchmark(Baseline = true)]
    public async Task Insert_Sequential()
    {
        foreach (var key in this.keys)
        {
            await this.UpsertAsync(key).ConfigureAwait(false);
        }
    }

    [Benchmark]
    public Task Insert_Parallel8() => Parallel.ForEachAsync(
        this.keys,
        new ParallelOptions { MaxDegreeOfParallelism = 8 },
        async (key, _) => await this.UpsertAsync(key).ConfigureAwait(false));

    private async Task UpsertAsync(DocumentKey key)
    {
        var result = await this.client.UpsertAsync(key, new BenchmarkWriteDocument { Value = new string('x', 1024) }).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }
    }
}

/// <summary>
/// Inserts 100,000 new 1 KB documents per iteration into a 1M row table and reports index fragmentation afterwards.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerSustainedInsertBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 0, iterationCount: 3)]
public class DocumentStorageEfSqlServerSustainedInsertBenchmarks
{
    private const int Inserts = 100_000;

    private DocumentStorageEfSqlServerLargeTableStore store;
    private IDocumentStoreClient<BenchmarkWriteDocument> client;
    private DocumentKey[] keys;
    private int iteration;

    [Params(1_000_000)]
    public int Rows { get; set; }

    [Params(DocumentBenchmarkIdStrategy.Random, DocumentBenchmarkIdStrategy.SqlServerSequential)]
    public DocumentBenchmarkIdStrategy Ids { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.store = DocumentStorageEfSqlServerLargeTableStore.GetAsync(this.Rows, this.Ids).GetAwaiter().GetResult();
        this.store.Database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
        this.client = this.store.Database.CreateClient<BenchmarkWriteDocument>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.store.Database.ReportIndexesAsync().GetAwaiter().GetResult();
        this.store.Database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var run = Interlocked.Increment(ref this.iteration);
        this.keys = Enumerable.Range(0, Inserts).Select(index => new DocumentKey($"s{index % 20:D2}", $"i{run}-{index}")).ToArray();
    }

    [Benchmark]
    public Task Insert100k_Parallel8() => Parallel.ForEachAsync(
        this.keys,
        new ParallelOptions { MaxDegreeOfParallelism = 8 },
        async (key, _) =>
        {
            var result = await this.client.UpsertAsync(key, new BenchmarkWriteDocument { Value = new string('x', 1024) }).ConfigureAwait(false);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("The insert failed.");
            }
        });
}

/// <summary>
/// Measures one retention sweep batch of 1000 expired documents in a 1M row table, with the unfiltered and the filtered
/// expiration index, plus a sweep that finds nothing to delete.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerExpirationBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerExpirationBenchmarks
{
    private const int Rows = 1_000_000;

    private DocumentBenchmarkDatabase database;
    private DocumentBenchmarkTemplate template;
    private IDocumentStoreRetentionProvider sweeper;
    private int iteration;

#if DOC_BASELINE
    [Params(DocumentBenchmarkExpirationIndex.Plain)]
#else
    [Params(DocumentBenchmarkExpirationIndex.Plain, DocumentBenchmarkExpirationIndex.Filtered)]
#endif
    public DocumentBenchmarkExpirationIndex Index { get; set; }

    [Params(0, 1000)]
    public int Expired { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var server = DocumentStorageEfSqlServerBenchmarkEnvironment.GetServerConnectionStringAsync().GetAwaiter().GetResult();
        var name = DocumentStorageEfSqlServerBenchmarkEnvironment.DatabaseName($"docbench_expire_{this.Index.ToString().ToLowerInvariant()}_{Rows}");
        this.database = this.Index == DocumentBenchmarkExpirationIndex.Plain
            ? DocumentBenchmarkDatabase.Create<BenchmarkPlainDocumentDbContext>(server, name)
            : DocumentBenchmarkDatabase.Create<BenchmarkDocumentDbContext>(server, name);
        var client = this.database.CreateClient<BenchmarkDocument>();
        if (!this.database.PrepareAsync(Rows).GetAwaiter().GetResult())
        {
            this.SeedAsync(client).GetAwaiter().GetResult();
        }

        this.template = DocumentBenchmarkTemplate.CreateAsync(this.database, client).GetAwaiter().GetResult();
        this.sweeper = (IDocumentStoreRetentionProvider)this.database.Provider;
        this.database.ReportIndexesAsync().GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (this.Expired == 0)
        {
            return;
        }

        var run = Interlocked.Increment(ref this.iteration);
        var table = this.template.CreateTable();
        var expiredAt = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        for (var index = 0; index < this.Expired; index++)
        {
            this.template.AddRow(table, Guid.NewGuid(), new DocumentKey("expired", $"i{run}-{index}"), DateTimeOffset.UtcNow, expiredAt);
        }

        this.database.BulkInsertAsync(table).GetAwaiter().GetResult();
    }

    [Benchmark]
    public async Task<long> SweepBatch()
    {
        var result = await this.sweeper.SweepExpiredAsync(new DocumentRetentionSweepRequest
        {
            DocumentType = DocumentTypeIdentity.For<BenchmarkDocument>(),
            VisibilityCutoff = DateTimeOffset.UtcNow,
            BatchSize = 1000,
            MaxBatches = 1
        }).ConfigureAwait(false);
        if (result.IsFailure || result.Value.DeletedCount != this.Expired)
        {
            throw new InvalidOperationException($"Expected {this.Expired} deleted documents.");
        }

        return result.Value.DeletedCount;
    }

    private async Task SeedAsync(IDocumentStoreClient<BenchmarkDocument> client)
    {
        Console.WriteLine($"[doc-bench] {this.database.Name} seeding {Rows} documents");
        var watch = Stopwatch.StartNew();
        await this.database.ExecuteAsync($"ALTER DATABASE [{this.database.Name}] SET RECOVERY SIMPLE").ConfigureAwait(false);
        var seedTemplate = await DocumentBenchmarkTemplate.CreateAsync(this.database, client).ConfigureAwait(false);
        var origin = DateTimeOffset.UtcNow.AddDays(-30);
        var future = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();
        for (var start = 0; start < Rows; start += 20_000)
        {
            var table = seedTemplate.CreateTable();
            for (var row = start; row < start + 20_000; row++)
            {
                // 5 percent of the documents expire in the future, the rest never expire.
                seedTemplate.AddRow(table, Guid.NewGuid(), DocumentBenchmarkDatabase.KeyFor(row), origin.AddMilliseconds(row), row % 20 == 0 ? future + row : null);
            }

            await this.database.BulkInsertAsync(table).ConfigureAwait(false);
        }

        Console.WriteLine($"[doc-bench] {this.database.Name} seeded in {watch.Elapsed.TotalSeconds:F1}s");
    }
}
