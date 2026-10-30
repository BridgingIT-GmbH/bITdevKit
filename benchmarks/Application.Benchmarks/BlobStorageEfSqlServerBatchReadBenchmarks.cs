// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

/// <summary>
/// Compares reading a request worth of blobs (100 keys) with 16 parallel single-key reads against multi-key SQL
/// queries. The raw SQL batch rows show the ceiling a provider level multi-get could reach.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *BlobStorageEfSqlServerBatchReadBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 20)]
public class BlobStorageEfSqlServerBatchReadBenchmarks
{
    private BlobStorageEfSqlServerReadBenchmarkStore store;

    [Params(10 * 1024, 100 * 1024, 1024 * 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup() =>
        this.store = await BlobStorageEfSqlServerReadBenchmarkEnvironment.GetStoreAsync(this.PayloadBytes);

    [Benchmark(Baseline = true)]
    public Task<long> DevKit_Parallel16() => this.store.ReadRequestParallelAsync(16, rawSql: false);

    [Benchmark]
    public Task<long> DevKit_Many_Batches4() => this.store.ReadRequestManyAsync(16, 4);

    [Benchmark]
    public Task<long> DevKit_Many_Batches8() => this.store.ReadRequestManyAsync(32, 8);

    [Benchmark]
    public Task<long> RawSql_Parallel16() => this.store.ReadRequestParallelAsync(16, rawSql: true);

    [Benchmark]
    public Task<long> RawSql_Batch100_Single() => this.store.ReadRequestRawSqlBatchAsync(100, 1);

    [Benchmark]
    public Task<long> RawSql_Batch25_Parallel4() => this.store.ReadRequestRawSqlBatchAsync(25, 4);

    [Benchmark]
    public Task<long> RawSql_Batch10_Parallel10() => this.store.ReadRequestRawSqlBatchAsync(10, 10);
}
