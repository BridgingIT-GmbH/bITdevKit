// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

/// <summary>
/// Baseline for reading blobs below the 4 MB chunk size through the Entity Framework provider on SQL Server,
/// one read after the other. Latency per read is reported for the single-read benchmarks.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *BlobStorageEfSqlServerSequentialReadBenchmarks*
/// The "RawSql" benchmarks use one hand-written join query and show the floor the DevKit path is compared against.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 30)]
public class BlobStorageEfSqlServerSequentialReadBenchmarks
{
    private const int ReadsPerInvoke = 20;

    private BlobStorageEfSqlServerReadBenchmarkStore store;

    [Params(10 * 1024, 100 * 1024, 1024 * 1024, 3 * 1024 * 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public async Task Setup() =>
        this.store = await BlobStorageEfSqlServerReadBenchmarkEnvironment.GetStoreAsync(this.PayloadBytes);

    [IterationSetup(Target = nameof(DevKit_ReadRequest_Cold))]
    public void DropBuffers() => this.store.DropCleanBuffersAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true, OperationsPerInvoke = ReadsPerInvoke)]
    public async Task<long> DevKit_Read()
    {
        long total = 0;
        for (var index = 0; index < ReadsPerInvoke; index++)
        {
            total += await this.store.ReadAsync(this.store.Request[index % this.store.Request.Length]);
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = ReadsPerInvoke)]
    public async Task<long> RawSql_Read()
    {
        long total = 0;
        for (var index = 0; index < ReadsPerInvoke; index++)
        {
            total += await this.store.ReadRawSqlAsync(index % this.store.Request.Length);
        }

        return total;
    }

    [Benchmark]
    public Task<long> DevKit_ReadRequest() => this.store.ReadRequestSequentialAsync(rawSql: false);

    [Benchmark]
    public Task<long> RawSql_ReadRequest() => this.store.ReadRequestSequentialAsync(rawSql: true);

    [Benchmark]
    public Task<long> DevKit_ReadRequest_Cold() => this.store.ReadRequestSequentialAsync(rawSql: false);
}
