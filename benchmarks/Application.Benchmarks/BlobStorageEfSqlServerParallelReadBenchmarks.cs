// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

/// <summary>
/// Baseline for reading a request worth of blobs (100 random id/day keys, fewer for the larger payloads) with
/// bounded parallelism through the Entity Framework provider on SQL Server. The reported time is per request,
/// so reads per second is 100 divided by the mean time.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *BlobStorageEfSqlServerParallelReadBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 20)]
public class BlobStorageEfSqlServerParallelReadBenchmarks
{
    private BlobStorageEfSqlServerReadBenchmarkStore store;

    [Params(10 * 1024, 100 * 1024, 1024 * 1024, 3 * 1024 * 1024)]
    public int PayloadBytes { get; set; }

    [Params(4, 16, 32)]
    public int Parallelism { get; set; }

    [GlobalSetup]
    public async Task Setup() =>
        this.store = await BlobStorageEfSqlServerReadBenchmarkEnvironment.GetStoreAsync(this.PayloadBytes);

    [IterationSetup(Target = nameof(DevKit_ReadRequest_Cold))]
    public void DropBuffers() => this.store.DropCleanBuffersAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)]
    public Task<long> DevKit_ReadRequest() => this.store.ReadRequestParallelAsync(this.Parallelism, rawSql: false);

    [Benchmark]
    public Task<long> RawSql_ReadRequest() => this.store.ReadRequestParallelAsync(this.Parallelism, rawSql: true);

    [Benchmark]
    public Task<long> DevKit_ReadRequest_Cold() => this.store.ReadRequestParallelAsync(this.Parallelism, rawSql: false);
}
