// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Measures bounded live-data maintenance and foreground reads during actual refresh as separate completed scenarios.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheMaintenanceBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheMaintenanceBenchmarks
{
    private CacheBenchmarkEnvironment environment;

    /// <summary>Gets or sets the explicit complete native provider.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite", "FileSystem"]);

    /// <summary>Gets or sets the actual live data-set size, including explicitly reduced native representatives.</summary>
    [ParamsSource(nameof(EntryProfiles))]
    public int Entries { get; set; }

    /// <summary>Gets actual visible data-set parameters selected through BDK_CACHE_BENCH_ENTRIES.</summary>
    public IReadOnlyList<int> EntryProfiles => CacheBenchmarkEnvironment.SelectEntryCounts();

    /// <summary>Seeds a fully live data set outside measurement.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, this.Entries, 256);
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Measures one native bounded cleanup attempt and verifies its outcome.</summary>
    [Benchmark]
    public async Task<bool> MaintainLiveDataAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var result = await this.environment.Provider.MaintainAsync(new(256, now, now.AddSeconds(5)));
        CacheBenchmarkEnvironment.Ensure(result);
        return true;
    }

    /// <summary>Measures one fully decoded foreground hit while one bounded native maintenance unit runs on another worker.</summary>
    /// <remarks>The reported duration covers both completed operations; it is not a foreground-only latency percentile.</remarks>
    [Benchmark]
    public async Task<int> ForegroundWithMaintenanceAsync()
    {
        var maintenance = Task.Run(this.MaintainLiveDataAsync);
        var hit = await this.environment.Collection.GetAsync("seed:0");
        CacheBenchmarkEnvironment.Ensure(hit);
        await maintenance;
        if (!hit.Value.Found || hit.Value.Value.Length != 256) { throw new InvalidDataException("Foreground work did not return the full seeded value."); }

        return hit.Value.Value.Length;
    }

    /// <summary>Completes a foreground old-value read while a real source refresh is held, then verifies the newly published replacement.</summary>
    /// <remarks>Includes seeding, one source refresh, both complete reads, and confirmed publication. This combined scenario duration is not foreground-only latency.</remarks>
    [Benchmark]
    public async Task<int> ForegroundWithRefreshAsync()
    {
        const string Key = "foreground:refresh";
        var original = new string('o', 256);
        var replacement = new string('r', 256);
        var seeded = await this.environment.Collection.SetAsync(new(Key), original);
        CacheBenchmarkEnvironment.Ensure(seeded);
        if (seeded.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("The refresh workload did not retain its original version."); }

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new(replacement));
        }

        var refresh = this.environment.Collection.RefreshAsync(new(Key), Load).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var during = await this.environment.Collection.GetAsync(Key);
            CacheBenchmarkEnvironment.Ensure(during);
            if (!during.Value.Found || during.Value.Value != original || during.Value.Version != seeded.Value.Version || refresh.IsCompleted)
            {
                throw new InvalidDataException("A foreground read did not acquire the valid old version during actual source refresh.");
            }
        }
        finally { release.TrySetResult(); }

        var refreshed = await refresh;
        CacheBenchmarkEnvironment.Ensure(refreshed);
        var after = await this.environment.Collection.GetAsync(Key);
        CacheBenchmarkEnvironment.Ensure(after);
        if (calls != 1 || !refreshed.Value.Found || refreshed.Value.Value != replacement || refreshed.Value.StorageOutcome != CacheStorageOutcome.Stored ||
            !after.Value.Found || after.Value.Value != replacement || after.Value.Version != refreshed.Value.Version || after.Value.Version == seeded.Value.Version)
        {
            throw new InvalidDataException("The refresh scenario did not complete exactly one source and publish the verified replacement.");
        }

        return 2;
    }

    /// <summary>Releases provider work before temporary-root cleanup.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}