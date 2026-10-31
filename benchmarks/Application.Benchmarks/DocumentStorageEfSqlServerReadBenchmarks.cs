// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Measures how long reading a request worth of documents (100 keys) takes on the EF document store provider against
/// SQL Server, per payload size and key spread. The one-by-one DevKit read is the baseline, the raw SQL rows are the
/// lower bound a hand-written query reaches.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerReadBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerReadBenchmarks
{
    private DocumentStorageEfSqlServerSmallTableStore store;
    private DocumentKey[] request;
#if !DOC_BASELINE
    private IDocumentStoreClient<BenchmarkDocument> sequentialClient;
#endif

    [Params(1_024, 10_240, 102_400)]
    public int PayloadBytes { get; set; }

    [Params(DocumentBenchmarkSpread.SinglePartition, DocumentBenchmarkSpread.AcrossPartitions)]
    public DocumentBenchmarkSpread Spread { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.store = DocumentStorageEfSqlServerBenchmarkEnvironment.GetSmallStoreAsync(this.PayloadBytes).GetAwaiter().GetResult();
        this.request = this.store.Request(this.Spread);
#if !DOC_BASELINE
        this.sequentialClient = this.store.Database.CreateClient<BenchmarkDocument>(getManyMaxParallelism: 1);
#endif
    }

    [Benchmark(Baseline = true)]
    public Task<long> DevKit_Sequential100() => this.store.ReadSequentialAsync(this.request);

    [Benchmark]
    public Task<long> DevKit_Parallel16() => this.store.ReadParallelAsync(this.request, 16);

    [Benchmark]
    public Task<long> RawSql_Sequential100() => this.store.ReadRawSqlSequentialAsync(this.request);

    [Benchmark]
    public Task<long> RawSql_Batch() => this.store.ReadRawSqlBatchAsync(this.request, 16);

#if !DOC_BASELINE
    [Benchmark]
    public Task<long> DevKit_GetMany() => this.store.ReadManyAsync(this.request);

    [Benchmark]
    public Task<long> DevKit_GetMany_Sequential() => this.store.ReadManyAsync(this.request, this.sequentialClient);
#endif
}

#if !DOC_BASELINE
/// <summary>
/// Measures the effect of <see cref="DocumentStoreOptions.GetManyBatchSize" /> and of the request size on
/// <c>GetManyAsync</c>, to validate the default batch size.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerBatchSizeBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerBatchSizeBenchmarks
{
    private DocumentStorageEfSqlServerSmallTableStore store;
    private IDocumentStoreClient<BenchmarkDocument> client;
    private DocumentKey[] request;

    [Params(1_024, 10_240)]
    public int PayloadBytes { get; set; }

    [Params(100, 1000)]
    public int Keys { get; set; }

    [Params(DocumentBenchmarkSpread.SinglePartition, DocumentBenchmarkSpread.AcrossPartitions)]
    public DocumentBenchmarkSpread Spread { get; set; }

    [Params(10, 25, 100, 250)]
    public int GetManyBatchSize { get; set; }

    [Params(1, 4, 8)]
    public int GetManyMaxParallelism { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.store = DocumentStorageEfSqlServerBenchmarkEnvironment.GetSmallStoreAsync(this.PayloadBytes).GetAwaiter().GetResult();
        this.client = this.store.Database.CreateClient<BenchmarkDocument>(this.GetManyBatchSize, this.GetManyMaxParallelism);

        // A partition holds at most 100 documents, so a request of 1000 keys always spans partitions.
        this.request = this.Keys <= 100
            ? this.store.Request(this.Spread)
            : Enumerable.Range(0, this.Keys).Select(DocumentBenchmarkDatabase.KeyFor).ToArray();
    }

    [Benchmark]
    public Task<long> GetMany() => this.store.ReadManyAsync(this.request, this.client);
}
#endif
