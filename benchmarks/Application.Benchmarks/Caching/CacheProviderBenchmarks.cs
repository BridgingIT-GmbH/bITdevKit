// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using System.Diagnostics;

/// <summary>Measures fully completed ordinary cache operations against real complete providers.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheProviderBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheProviderBenchmarks
{
    private CacheBenchmarkEnvironment environment;
    private string value;
    private int position;
    private CacheLookup<string> snapshot;

    /// <summary>Gets or sets the explicit native engine; additional SQL engines require dedicated host connection settings.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite", "FileSystem"]);

    /// <summary>Gets or sets the explicitly selected healthy-store policy variant.</summary>
    [ParamsSource(nameof(PolicyProfiles))]
    public string Policy { get; set; }

    /// <summary>Gets visible policy parameters selected through BDK_CACHE_BENCH_POLICIES.</summary>
    public IReadOnlyList<string> PolicyProfiles => CacheBenchmarkEnvironment.SelectPolicies();

    /// <summary>Gets or sets the initial data set, independently from measured operation count.</summary>
    [ParamsSource(nameof(EntryProfiles))]
    public int Entries { get; set; }

    /// <summary>Gets actual visible data-set parameters selected through BDK_CACHE_BENCH_ENTRIES.</summary>
    public IReadOnlyList<int> EntryProfiles => CacheBenchmarkEnvironment.SelectEntryCounts();

    /// <summary>Gets or sets logical ASCII payload bytes.</summary>
    [ParamsSource(nameof(PayloadProfiles))]
    public int PayloadBytes { get; set; }

    /// <summary>Gets normal 256-byte/4-KiB values or an explicit BDK_CACHE_BENCH_TYPED_BYTES subset through one MiB.</summary>
    public IReadOnlyList<int> PayloadProfiles
    {
        get
        {
            var selected = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_TYPED_BYTES") ?? "256,4096";
            var sizes = selected.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (sizes.Length == 0 || sizes.Any(size => size is not (256 or 4096 or 65536 or 1048576))) { throw new ArgumentOutOfRangeException(nameof(selected)); }

            return sizes.Distinct().ToArray();
        }
    }

    /// <summary>Seeds the real provider and materializes source input outside measured operations.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, this.Entries, this.PayloadBytes, policy: this.Policy);
        this.value = new('x', this.PayloadBytes);
        CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.GetOrLoadAsync(new("negative"), (_, _) =>
            ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound()))));
        var acquired = await this.environment.Collection.GetAsync("seed:0");
        CacheBenchmarkEnvironment.Ensure(acquired);
        this.snapshot = acquired.Value;
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Measures a positive typed lookup including native validation, decode and complete integrity verification.</summary>
    [Benchmark]
    public async Task<int> GetHitAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var result = await this.environment.Collection.GetAsync("seed:" + (this.position++ % this.Entries));
        CacheBenchmarkEnvironment.Ensure(result);
        if (!result.Value.Found || result.Value.Value.Length != this.PayloadBytes) { throw new InvalidDataException("A seeded hit was not completely returned."); }

        this.environment.Measurements.RecordDuration("TypedHit", started);
        return result.Value.Value.Length;
    }

    /// <summary>Measures an actual absent-key read without source work.</summary>
    [Benchmark]
    public async Task<bool> GetMissAsync()
    {
        var result = await this.environment.Collection.GetAsync("never-seeded");
        CacheBenchmarkEnvironment.Ensure(result);
        if (result.Value.Found) { throw new InvalidDataException("Miss workload unexpectedly found data."); }

        return true;
    }

    /// <summary>Measures replacement, including staging and confirmed publication, over the fixed retained data set.</summary>
    [Benchmark]
    public async Task<bool> SetAsync()
    {
        var result = await this.environment.Collection.SetAsync(new("seed:" + (this.position++ % this.Entries)), this.value);
        CacheBenchmarkEnvironment.Ensure(result);
        if (result.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Publication was not confirmed."); }

        return true;
    }

    /// <summary>Measures one bounded payload-free metadata page separately from payload throughput.</summary>
    [Benchmark]
    public async Task<int> MetadataPageAsync()
    {
        var result = await this.environment.Provider.QueryAsync(new("benchmark", "values", CacheSelection.Prefix("seed:"), DateTimeOffset.UtcNow, PageSize: 100));
        CacheBenchmarkEnvironment.Ensure(result);
        if (result.Value.Entries.Count != Math.Min(100, this.Entries)) { throw new InvalidDataException("Metadata page returned incomplete work."); }

        return result.Value.Entries.Count;
    }

    /// <summary>Measures a persisted confirmed-absence hit without invoking a loader or opening payload content.</summary>
    [Benchmark]
    public async Task<bool> GetNegativeAsync()
    {
        var result = await this.environment.Collection.GetAsync("negative");
        CacheBenchmarkEnvironment.Ensure(result);
        if (!result.Value.IsNotFound || result.Value.Found) { throw new InvalidDataException("The negative marker was not retained."); }

        return true;
    }

    /// <summary>Measures ordered batch overhead separately; each successful operation returns exactly four entries.</summary>
    [Benchmark]
    public async Task<int> GetBatchAsync()
    {
        var result = await this.environment.Collection.GetOrLoadManyAsync([new("seed:0"), new("seed:1"), new("seed:2"), new("seed:3")],
            (CacheLoadDelegate<string>)((_, _) => throw new InvalidOperationException("A batch-hit benchmark must not invoke its source.")));
        CacheBenchmarkEnvironment.Ensure(result);
        if (result.Value.Count != 4 || result.Value.Any(item => item.IsFailure || !item.Value.Found)) { throw new InvalidDataException("The batch did not return all four hits."); }

        return result.Value.Count;
    }

    /// <summary>Measures the token already attached to a snapshot; this getter performs no second provider call.</summary>
    [Benchmark]
    public CacheEntryVersion? AcquiredSnapshotToken() => this.snapshot.Version;

    /// <summary>Measures source-side string allocation independently from cache/provider work.</summary>
    [Benchmark]
    public string SourceMaterialization() => new('x', this.PayloadBytes);

    /// <summary>Disposes actual native work before deleting owned local files.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}