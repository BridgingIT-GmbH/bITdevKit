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

/// <summary>How the key set of a simulated request is spread over the partitions.</summary>
public enum DocumentBenchmarkSpread
{
    /// <summary>All 100 keys live in one partition.</summary>
    SinglePartition,

    /// <summary>The 100 keys are sampled across all 20 partitions.</summary>
    AcrossPartitions
}

/// <summary>How the internal row ids of a bulk seeded table are generated.</summary>
public enum DocumentBenchmarkIdStrategy
{
    /// <summary>Random GUIDs, the behavior of the provider before sequential ids.</summary>
    Random,

    /// <summary>GUIDs that increase in SQL Server sort order, the behavior of the EF generator on SQL Server.</summary>
    SqlServerSequential
}

/// <summary>Which keys a simulated large table request reads.</summary>
public enum DocumentBenchmarkAccess
{
    /// <summary>100 keys sampled across the whole table.</summary>
    Random,

    /// <summary>The 100 most recently written documents.</summary>
    Recent
}

/// <summary>Whether the SQL Server buffer pool is emptied before each measured iteration.</summary>
public enum DocumentBenchmarkCache
{
    /// <summary>The buffer pool is left alone.</summary>
    Warm,

    /// <summary>DBCC DROPCLEANBUFFERS runs before each iteration.</summary>
    Cold
}

/// <summary>Which expiration index the table has.</summary>
public enum DocumentBenchmarkExpirationIndex
{
    /// <summary>The unfiltered (TypeHash, ExpiresAtUnixMilliseconds) index of the annotated entity.</summary>
    Plain,

    /// <summary>The filtered index created by ApplyDocumentStoreConfiguration.</summary>
    Filtered
}

/// <summary>The document read by the benchmarks. The length of <see cref="Value" /> sets the payload size.</summary>
internal sealed class BenchmarkDocument
{
    public string Value { get; set; }
}

/// <summary>A separate document type for benchmarks that write into the shared read tables.</summary>
internal sealed class BenchmarkWriteDocument
{
    public string Value { get; set; }
}

/// <summary>The benchmark context. It carries the filtered expiration index when the provider supports it.</summary>
internal sealed class BenchmarkDocumentDbContext(DbContextOptions<BenchmarkDocumentDbContext> options)
    : DbContext(options), IDocumentStoreContext
{
    public DbSet<StorageDocument> StorageDocuments { get; set; }

#if !DOC_BASELINE
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyDocumentStoreConfiguration();
#endif
}

/// <summary>A benchmark context that keeps the unfiltered expiration index.</summary>
internal sealed class BenchmarkPlainDocumentDbContext(DbContextOptions<BenchmarkPlainDocumentDbContext> options)
    : DbContext(options), IDocumentStoreContext
{
    public DbSet<StorageDocument> StorageDocuments { get; set; }
}

/// <summary>
/// Hosts the shared SQL Server used by the EF document benchmarks. By default a SQL Server 2022 container is started
/// once per process (requires Docker). Set DOC_BENCH_SQL_CONNECTION (or BLOB_BENCH_SQL_CONNECTION) to benchmark an
/// existing server instead. Set DOC_BENCH_RESEED=1 to drop and reseed the benchmark databases.
/// </summary>
internal static class DocumentStorageEfSqlServerBenchmarkEnvironment
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<int, DocumentStorageEfSqlServerSmallTableStore> Stores = [];
    private static MsSqlContainer container;
    private static string serverConnectionString;

    public static bool Reseed => Environment.GetEnvironmentVariable("DOC_BENCH_RESEED") == "1";

    /// <summary>
    /// Appended to every database name (DOC_BENCH_DB_SUFFIX) so a run on new code gets freshly created databases
    /// with the current schema instead of reusing databases created by an older version.
    /// </summary>
    public static string DatabaseName(string name) => name + Environment.GetEnvironmentVariable("DOC_BENCH_DB_SUFFIX");

    public static async Task<DocumentStorageEfSqlServerSmallTableStore> GetSmallStoreAsync(int payloadBytes)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Stores.TryGetValue(payloadBytes, out var existing))
            {
                return existing;
            }

            await EnsureServerAsync().ConfigureAwait(false);
            var store = await DocumentStorageEfSqlServerSmallTableStore.CreateAsync(serverConnectionString, payloadBytes)
                .ConfigureAwait(false);
            Stores[payloadBytes] = store;

            return store;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<string> GetServerConnectionStringAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureServerAsync().ConfigureAwait(false);

            return serverConnectionString;
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

        var external = Environment.GetEnvironmentVariable("DOC_BENCH_SQL_CONNECTION")
            ?? Environment.GetEnvironmentVariable("BLOB_BENCH_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            serverConnectionString = external;
            Console.WriteLine("[doc-bench] using external SQL Server from the environment");

            return;
        }

        Console.WriteLine("[doc-bench] starting SQL Server container");
        container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync().ConfigureAwait(false);
        serverConnectionString = container.GetConnectionString();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            foreach (var store in Stores.Values)
            {
                store.Database.Dispose();
            }

            container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
    }
}

/// <summary>
/// One benchmark database: the DI container, the EF provider under test, a SQL command counter and the helpers shared by
/// the small and the large table stores.
/// </summary>
internal sealed class DocumentBenchmarkDatabase : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly Func<Task<int>> ensureCreated;
    private readonly Func<Task> recreate;
    private readonly Func<DocumentStoreOptions, IDocumentStoreProvider> providerFactory;

    private DocumentBenchmarkDatabase(
        string name,
        string connectionString,
        ServiceProvider serviceProvider,
        IDocumentStoreProvider provider,
        SqlQueryCounter counter,
        Func<Task<int>> ensureCreated,
        Func<Task> recreate,
        Func<DocumentStoreOptions, IDocumentStoreProvider> providerFactory)
    {
        this.Name = name;
        this.ConnectionString = connectionString;
        this.serviceProvider = serviceProvider;
        this.Provider = provider;
        this.Counter = counter;
        this.ensureCreated = ensureCreated;
        this.recreate = recreate;
        this.providerFactory = providerFactory;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public IDocumentStoreProvider Provider { get; }

    public SqlQueryCounter Counter { get; }

    public static DocumentBenchmarkDatabase Create<TContext>(string serverConnectionString, string databaseName)
        where TContext : DbContext, IDocumentStoreContext
    {
        var connectionString = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = databaseName,
            MaxPoolSize = 200
        }.ConnectionString;
        var counter = new SqlQueryCounter();
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(options => options.UseSqlServer(connectionString).AddInterceptors(counter));
        var serviceProvider = services.BuildServiceProvider();
        var provider = new EntityFrameworkDocumentStoreProvider<TContext>(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        return new DocumentBenchmarkDatabase(
            databaseName,
            connectionString,
            serviceProvider,
            provider,
            counter,
            async () =>
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
                await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);

                return await dbContext.StorageDocuments.CountAsync().ConfigureAwait(false);
            },
            async () =>
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
                await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
                await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
            },
            options => new EntityFrameworkDocumentStoreProvider<TContext>(serviceProvider.GetRequiredService<IServiceScopeFactory>(), options));
    }

    public IDocumentStoreClient<T> CreateClient<T>(int? getManyBatchSize = null, int? getManyMaxParallelism = null)
        where T : class, new()
    {
        var options = new DocumentStoreOptions();
#if !DOC_BASELINE
        options.GetManyBatchSize = getManyBatchSize ?? options.GetManyBatchSize;
        options.GetManyMaxParallelism = getManyMaxParallelism ?? options.GetManyMaxParallelism;
#endif

        return new DocumentStoreClient<T>(this.providerFactory(options), options: options);
    }

    /// <summary>Creates the database when needed and returns true when it already holds the expected row count.</summary>
    public async Task<bool> PrepareAsync(int expectedRows)
    {
        if (DocumentStorageEfSqlServerBenchmarkEnvironment.Reseed)
        {
            await this.recreate().ConfigureAwait(false);
        }

        var existing = await this.ensureCreated().ConfigureAwait(false);
        if (existing == expectedRows)
        {
            Console.WriteLine($"[doc-bench] {this.Name} reusing {existing} seeded documents");

            return true;
        }

        if (existing != 0)
        {
            await this.recreate().ConfigureAwait(false);
        }

        return false;
    }

    public static string KeyOf(int row, out string rowKey)
    {
        rowKey = $"r{row / 20:D7}";

        return $"p{row % 20:D2}";
    }

    public static DocumentKey KeyFor(int row)
    {
        var partition = KeyOf(row, out var rowKey);

        return new DocumentKey(partition, rowKey);
    }

    public static string CreatePayload(int length, int seed = 0)
    {
        var chars = new char[length];
        var random = new Random(seed);
        for (var index = 0; index < length; index++)
        {
            chars[index] = (char)('a' + random.Next(26));
        }

        return new string(chars);
    }

    public async Task DropCleanBuffersAsync() =>
        await this.ExecuteAsync("CHECKPOINT; DBCC DROPCLEANBUFFERS;").ConfigureAwait(false);

    public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task DeleteDocumentsOfTypeAsync<T>()
    {
        var typeHash = HashHelper.ComputeSha256(DocumentTypeIdentity.For<T>().Value);
        await this.ExecuteAsync(
            "DELETE FROM [__Storage_Documents] WHERE [TypeHash] = @typeHash",
            new SqlParameter("@typeHash", SqlDbType.NVarChar, 64) { Value = typeHash }).ConfigureAwait(false);
    }

    public async Task DeleteRowAsync(DocumentKey key) =>
        await this.ExecuteAsync(
            "DELETE FROM [__Storage_Documents] WHERE [PartitionKey] = @p AND [RowKey] = @r",
            new SqlParameter("@p", key.PartitionKey),
            new SqlParameter("@r", key.RowKey)).ConfigureAwait(false);

    public async Task ReportIndexesAsync()
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText =
            "SELECT i.name, s.page_count, s.avg_fragmentation_in_percent, s.avg_page_space_used_in_percent " +
            "FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('__Storage_Documents'), NULL, NULL, 'SAMPLED') s " +
            "JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id " +
            "WHERE s.index_level = 0 ORDER BY 1";
        Console.WriteLine($"[doc-bench] {this.Name} index stats (pages, fragmentation %, page fullness %)");
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            Console.WriteLine(
                $"[doc-bench]   {reader.GetString(0)}: {reader.GetInt64(1)} pages, " +
                $"{reader.GetDouble(2):F1}% fragmented, {reader.GetDouble(3):F1}% full");
        }
    }

    /// <summary>Reads one stored row back with all columns so it can serve as the template for bulk seeding.</summary>
    public async Task<(string[] Names, Type[] Types, object[] Values)> ReadRawRowAsync(DocumentKey key)
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP 1 * FROM [__Storage_Documents] WHERE [PartitionKey] = @p AND [RowKey] = @r";
        command.Parameters.Add(new SqlParameter("@p", key.PartitionKey));
        command.Parameters.Add(new SqlParameter("@r", key.RowKey));
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("Template row not found.");
        }

        var names = new string[reader.FieldCount];
        var types = new Type[reader.FieldCount];
        var values = new object[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            names[index] = reader.GetName(index);
            types[index] = reader.GetFieldType(index);
            values[index] = reader.GetValue(index);
        }

        return (names, types, values);
    }

    public async Task BulkInsertAsync(DataTable table, int batchSize = 20_000)
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = "__Storage_Documents",
            BulkCopyTimeout = 0,
            BatchSize = batchSize
        };
        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table).ConfigureAwait(false);
    }

    public void Dispose() => this.serviceProvider.Dispose();

    internal sealed class SqlQueryCounter : DbCommandInterceptor
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

/// <summary>
/// A small seeded document database (up to 2000 documents of one payload size over 20 partitions) plus the clients
/// and raw SQL baselines used to read it.
/// </summary>
internal sealed class DocumentStorageEfSqlServerSmallTableStore
{
    private const long SeedBudgetBytes = 256L * 1024 * 1024;
    private const int PartitionCount = 20;
    private const int RequestSize = 100;

    private readonly DocumentKey[] singlePartitionRequest;
    private readonly DocumentKey[] acrossPartitionsRequest;
    private readonly string typeHash = HashHelper.ComputeSha256(DocumentTypeIdentity.For<BenchmarkDocument>().Value);

    private DocumentStorageEfSqlServerSmallTableStore(
        DocumentBenchmarkDatabase database,
        int payloadBytes,
        DocumentKey[] singlePartitionRequest,
        DocumentKey[] acrossPartitionsRequest)
    {
        this.Database = database;
        this.PayloadBytes = payloadBytes;
        this.singlePartitionRequest = singlePartitionRequest;
        this.acrossPartitionsRequest = acrossPartitionsRequest;
        this.Client = database.CreateClient<BenchmarkDocument>();
    }

    public DocumentBenchmarkDatabase Database { get; }

    public int PayloadBytes { get; }

    public IDocumentStoreClient<BenchmarkDocument> Client { get; }

    public int SqlQueriesPerRead { get; private set; }

    public DocumentKey[] Request(DocumentBenchmarkSpread spread) => spread == DocumentBenchmarkSpread.SinglePartition
        ? this.singlePartitionRequest
        : this.acrossPartitionsRequest;

    public static async Task<DocumentStorageEfSqlServerSmallTableStore> CreateAsync(string serverConnectionString, int payloadBytes)
    {
        var count = (int)Math.Clamp(SeedBudgetBytes / payloadBytes, 100, 2000);
        count -= count % PartitionCount;

        var database = DocumentBenchmarkDatabase.Create<BenchmarkDocumentDbContext>(serverConnectionString, DocumentStorageEfSqlServerBenchmarkEnvironment.DatabaseName($"docbench_{payloadBytes}"));
        var all = Enumerable.Range(0, count).Select(DocumentBenchmarkDatabase.KeyFor).ToArray();
        var single = all.Where(key => key.PartitionKey == "p00").Take(RequestSize).ToArray();
        var random = new Random(42);
        var across = all.OrderBy(_ => random.Next()).Take(Math.Min(RequestSize, all.Length)).ToArray();

        var store = new DocumentStorageEfSqlServerSmallTableStore(database, payloadBytes, single, across);
        await store.EnsureSeededAsync(all).ConfigureAwait(false);
        await store.ProbeAsync(all.Length).ConfigureAwait(false);

        return store;
    }

    public async Task<int> ReadAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.Client.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return this.Verify(result.Value.Value.Value.Length);
    }

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
    public async Task<long> ReadManyAsync(DocumentKey[] keys, IDocumentStoreClient<BenchmarkDocument> client = null)
    {
        var result = await (client ?? this.Client).GetManyAsync(keys).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        long total = 0;
        foreach (var item in result.Value)
        {
            if (item.IsFailure)
            {
                throw new InvalidOperationException("A document could not be read.");
            }

            total += this.Verify(item.Value.Value.Value.Length);
        }

        if (result.Value.Count != keys.Length)
        {
            throw new InvalidOperationException($"Expected {keys.Length} documents but read {result.Value.Count}.");
        }

        return total;
    }
#endif

    /// <summary>Reads one document with a hand-written query, the lower bound for the single read path.</summary>
    public async Task<long> ReadRawSqlAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(this.Database.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT [Content] FROM [__Storage_Documents] " +
            "WHERE [TypeHash] = @typeHash AND [PartitionKeyHash] = @partitionHash AND [RowKeyHash] = @rowHash";
        command.Parameters.Add(new SqlParameter("@typeHash", SqlDbType.NVarChar, 64) { Value = this.typeHash });
        command.Parameters.Add(new SqlParameter("@partitionHash", SqlDbType.NVarChar, 64) { Value = HashHelper.ComputeSha256(key.PartitionKey) });
        command.Parameters.Add(new SqlParameter("@rowHash", SqlDbType.NVarChar, 64) { Value = HashHelper.ComputeSha256(key.RowKey) });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Document row not found by raw SQL.");
        }

        return (await reader.GetFieldValueAsync<byte[]>(0, cancellationToken).ConfigureAwait(false)).Length;
    }

    public async Task<long> ReadRawSqlSequentialAsync(DocumentKey[] keys)
    {
        long total = 0;
        foreach (var key in keys)
        {
            total += await this.ReadRawSqlAsync(key).ConfigureAwait(false);
        }

        return total;
    }

    /// <summary>Reads the keys with one multi-key query per partition, the ceiling for a provider level multi-get.</summary>
    public async Task<long> ReadRawSqlBatchAsync(DocumentKey[] keys, int parallelism)
    {
        long total = 0;
        var partitions = keys.GroupBy(key => key.PartitionKey).ToArray();
        await Parallel.ForEachAsync(
            partitions,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism },
            async (partition, cancellationToken) =>
            {
                await using var connection = new SqlConnection(this.Database.ConnectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                var rows = partition.Select((key, index) => (Parameter: $"@r{index}", Hash: HashHelper.ComputeSha256(key.RowKey))).ToArray();
                command.CommandText =
                    "SELECT [Content] FROM [__Storage_Documents] " +
                    "WHERE [TypeHash] = @typeHash AND [PartitionKeyHash] = @partitionHash " +
                    $"AND [RowKeyHash] IN ({string.Join(", ", rows.Select(row => row.Parameter))})";
                command.Parameters.Add(new SqlParameter("@typeHash", SqlDbType.NVarChar, 64) { Value = this.typeHash });
                command.Parameters.Add(new SqlParameter("@partitionHash", SqlDbType.NVarChar, 64) { Value = HashHelper.ComputeSha256(partition.Key) });
                foreach (var (parameter, hash) in rows)
                {
                    command.Parameters.Add(new SqlParameter(parameter, SqlDbType.NVarChar, 64) { Value = hash });
                }

                var count = 0;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    Interlocked.Add(ref total, (await reader.GetFieldValueAsync<byte[]>(0, cancellationToken).ConfigureAwait(false)).Length);
                    count++;
                }

                if (count != rows.Length)
                {
                    throw new InvalidOperationException($"Expected {rows.Length} rows but read {count}.");
                }
            }).ConfigureAwait(false);

        return total;
    }

    private int Verify(int length) => length == this.PayloadBytes
        ? length
        : throw new InvalidOperationException($"Expected {this.PayloadBytes} characters but read {length}.");

    private async Task EnsureSeededAsync(DocumentKey[] keys)
    {
        if (await this.Database.PrepareAsync(keys.Length).ConfigureAwait(false))
        {
            return;
        }

        Console.WriteLine($"[doc-bench] payload={this.PayloadBytes} seeding {keys.Length} documents");
        var watch = Stopwatch.StartNew();
        var index = 0;
        await Parallel.ForEachAsync(
            keys,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (key, cancellationToken) =>
            {
                var document = new BenchmarkDocument { Value = DocumentBenchmarkDatabase.CreatePayload(this.PayloadBytes, Interlocked.Increment(ref index)) };
                var result = await this.Client.UpsertAsync(key, document, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
                }
            }).ConfigureAwait(false);
        Console.WriteLine($"[doc-bench] payload={this.PayloadBytes} seeded in {watch.Elapsed.TotalSeconds:F1}s");
    }

    private async Task ProbeAsync(int documentCount)
    {
        this.Database.Counter.Reset();
        await this.ReadAsync(this.acrossPartitionsRequest[0]).ConfigureAwait(false);
        this.SqlQueriesPerRead = this.Database.Counter.Count;
        Console.WriteLine(
            $"[doc-bench] payload={this.PayloadBytes} documents={documentCount} request={this.acrossPartitionsRequest.Length} " +
            $"sqlQueriesPerRead={this.SqlQueriesPerRead}");
    }
}
