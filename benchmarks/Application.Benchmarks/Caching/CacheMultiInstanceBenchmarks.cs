// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

/// <summary>Measures fixed-size cold bursts through independent application containers sharing one local native store.</summary>
/// <remarks>Instances run in one process. This measures separate application scopes, not independent operating-system hosts or network shares.</remarks>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheMultiInstanceBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheMultiInstanceBenchmarks
{
    private CacheBenchmarkEnvironment[] nodes;
    private CacheBenchmarkProviderBehavior[] observers;

    /// <summary>Gets or sets the real local SQLite or filesystem store shared by all independently composed instances.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets requested profile labels; unsupported central stores are rejected only when this workload's setup is selected.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["Sqlite", "FileSystem"]);

    /// <summary>Gets or sets independent application container count while keeping the total caller count fixed at 32.</summary>
    [Params(2, 4)]
    public int Instances { get; set; }

    /// <summary>Gets or sets shared-store coalescing, per-node coalescing, independent source work, or a source-only control.</summary>
    [ParamsSource(nameof(ModeProfiles))]
    public string Mode { get; set; }

    /// <summary>Gets all source scopes, or the BDK_CACHE_BENCH_LOAD_MODES subset for explicitly selected cohorts.</summary>
    public IReadOnlyList<string> ModeProfiles
    {
        get
        {
            string[] supported = ["SharedStore", "NodeLocal", "Independent", "SourceOnly", "UncachedHandoff"];
            var setting = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_LOAD_MODES");
            var selected = setting is null ? supported : setting.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selected.Length == 0 || selected.Any(mode => !supported.Contains(mode, StringComparer.Ordinal))) { throw new ArgumentException("Unknown multi-instance source scope."); }

            return selected.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Composes all real provider hosts over one isolated local root before measurement.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        if (this.Provider is not ("Sqlite" or "FileSystem")) { throw new ArgumentException("Multi-instance benchmarks require Sqlite or FileSystem."); }

        this.nodes = new CacheBenchmarkEnvironment[this.Instances];
        this.observers = new CacheBenchmarkProviderBehavior[this.Instances];
        var directory = Path.Combine(Path.GetTempPath(), "bdk-cache-multi-benchmark-" + Guid.NewGuid().ToString("N"));
        for (var index = 0; index < this.nodes.Length; index++)
        {
            var slot = index;
            this.nodes[index] = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256, concurrency: 32 / this.Instances,
                directory: directory, ownsDirectory: index == 0,
                coordination: this.Mode is "SharedStore" or "UncachedHandoff" ? CacheCoordinationScope.SharedStore : CacheCoordinationScope.NodeLocal,
                strategy: this.Mode == "Independent" ? CacheLoadingStrategy.Independent : CacheLoadingStrategy.Shared,
                providerBehavior: native => this.observers[slot] = new(native));
        }

        foreach (var node in this.nodes) { node.MarkMeasurementsReady(); }
    }

    /// <summary>Delivers exactly 32 complete answers and verifies the selected scope's actual source count and remote observer bound.</summary>
    /// <remarks>Includes exact removal for cache modes, source barriers, source materialization, complete publication and all caller results. SourceOnly executes no cache operation during measurement.</remarks>
    [Benchmark]
    public async Task<int> ColdBurstAsync()
    {
        const int Callers = 32;
        var shared = this.Mode is "SharedStore" or "UncachedHandoff";
        var expected = this.Mode == "UncachedHandoff" ? 2 : shared ? 1 : this.Mode == "NodeLocal" ? this.Instances : Callers;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            var started = Stopwatch.GetTimestamp();
            var source = Interlocked.Increment(ref calls);
            if (source == (shared ? 1 : expected)) { entered.TrySetResult(); }

            try
            {
                await release.Task.WaitAsync(token);
                return Result<CacheLoadResult<string>>.Success(new(new string('x', 256), new() { SuppressStorage = this.Mode == "UncachedHandoff" && source == 1 }));
            }
            finally { this.nodes[0].Measurements.RecordDuration("Source", started); }
        }

        if (this.Mode == "SourceOnly")
        {
            var sources = Enumerable.Range(0, Callers).Select(_ => this.nodes[0].Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), Load(null, default))).ToArray();
            try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
            finally { release.TrySetResult(); }

            var direct = await Task.WhenAll(sources);
            if (calls != Callers || direct.Any(result => result.IsFailure || result.Value.Value.Length != 256))
            {
                throw new InvalidDataException("The source-only control did not finish all 32 source computations.");
            }

            return direct.Length;
        }

        CacheBenchmarkEnvironment.Ensure(await this.nodes[0].Collection.RemoveAsync("shared:cold"));
        foreach (var observer in this.observers) { observer.ResetPending(); }

        var acquired = this.observers.Select(observer => observer.Acquisitions).ToArray();
        var joins = this.nodes.Select(node => node.Services.GetRequiredService<CacheDiagnostics>().ForCollection("values")
            .Snapshot().Counters.GetValueOrDefault(CacheObservationKind.Join) ?? 0).ToArray();
        var owner = this.nodes[0].Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), this.nodes[0].Collection.GetOrLoadAsync(new("shared:cold"), Load));
        if (shared) { await entered.Task.WaitAsync(TimeSpan.FromSeconds(15)); }

        var others = Enumerable.Range(1, Callers - 1).Select(index =>
        {
            var node = this.nodes[index % this.nodes.Length];
            return node.Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), node.Collection.GetOrLoadAsync(new("shared:cold"), Load));
        }).ToArray();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (shared || this.Mode == "NodeLocal")
            {
                if (shared) { await Task.WhenAll(this.observers.Skip(1).Select(observer => observer.Pending)).WaitAsync(TimeSpan.FromSeconds(15)); }

                using var registrationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (this.nodes.Where((node, index) =>
                    (node.Services.GetRequiredService<CacheDiagnostics>().ForCollection("values").Snapshot().Counters.GetValueOrDefault(CacheObservationKind.Join) ?? 0) - joins[index] <
                    Callers / this.Instances - (shared && index != 0 ? 0 : 1)).Any())
                {
                    await Task.Delay(1, registrationDeadline.Token);
                }
            }
        }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(others.Prepend(owner));
        foreach (var result in results)
        {
            CacheBenchmarkEnvironment.Ensure(result);
            var acquiredHit = result.Value.LookupOutcome == CacheLookupOutcome.Hit &&
                result.Value.StorageOutcome == CacheStorageOutcome.None && result.Value.Version.HasValue;
            var validStorage = this.Mode is "Independent" or "NodeLocal" ?
                result.Value.StorageOutcome is CacheStorageOutcome.Stored or CacheStorageOutcome.Conflict :
                result.Value.StorageOutcome is CacheStorageOutcome.Stored or CacheStorageOutcome.Suppressed;
            if (!result.Value.Found || result.Value.Value.Length != 256 ||
                (result.Value.StorageOutcome == CacheStorageOutcome.Stored || acquiredHit) != result.Value.Version.HasValue ||
                (!validStorage && !acquiredHit))
            {
                throw new InvalidDataException("A multi-instance cold caller did not acquire a complete confirmed stored answer.");
            }
        }

        if (!results.Any(result => result.Value.StorageOutcome == CacheStorageOutcome.Stored)) { throw new InvalidDataException("No independent node confirmed a stored publication."); }

        if (this.Mode == "UncachedHandoff" && results.Count(result => result.Value.StorageOutcome == CacheStorageOutcome.Suppressed) != Callers / this.Instances)
        {
            throw new InvalidDataException("An unretained owner graph crossed node scopes or a remote positive caller avoided confirmed publication.");
        }

        if (calls != expected || (this.Mode == "SharedStore" && this.observers.Where((observer, index) =>
            observer.Acquisitions - acquired[index] != 1 || observer.MaximumObservers > 1).Any()) ||
            (shared && this.observers.Any(observer => observer.MaximumObservers > 1)))
        {
            throw new InvalidDataException("The measured source count or one-observer-per-node contract was violated.");
        }

        return results.Length;
    }

    /// <summary>Disposes all peer work before the owning host removes its dedicated local root.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        for (var index = this.nodes.Length - 1; index >= 0; index--) { if (this.nodes[index] is not null) { await this.nodes[index].DisposeAsync(); } }
    }
}