// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Diagnostics;

/// <summary>Measures synchronized node-local cold loads and overlapping batches including invalidation and confirmed publication.</summary>
/// <remarks>The source gate holds completion until all calls are submitted. Callers still performing their initial read may acquire the new entry; actual source/join counters are recorded separately.</remarks>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheSharedLoadBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheSharedLoadBenchmarks
{
    private CacheBenchmarkEnvironment environment;
    private string value;

    /// <summary>Gets or sets the real provider profile.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite"]);

    /// <summary>Gets or sets the same-key burst caller count and native admission profile. The overlapping method always uses two batches.</summary>
    [Params(1, 8, 32, 128)]
    public int Concurrency { get; set; }

    /// <summary>Creates one host and pre-materializes the source value outside measured operations.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 4096, concurrency: this.Concurrency);
        this.value = new('x', 4096);
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Returns the count of fully successful callers and rejects accidental duplicate source work.</summary>
    [Benchmark]
    public async Task<int> SameKeyColdBurstAsync()
    {
        CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync("burst"));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            var started = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref calls);
            try
            {
                await release.Task.WaitAsync(token);
                return Result<CacheLoadResult<string>>.Success(new(this.value));
            }
            finally { this.environment.Measurements.RecordDuration("Source", started); }
        }

        var tasks = Enumerable.Range(0, this.Concurrency).Select(_ => this.environment.Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), this.environment.Collection.GetOrLoadAsync(new("burst"), Load))).ToArray();
        release.TrySetResult();
        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            CacheBenchmarkEnvironment.Ensure(result);
            if (!result.Value.Found || result.Value.Value.Length != 4096) { throw new InvalidDataException("The burst did not complete every source result."); }
        }

        if (calls != 1) { throw new InvalidDataException("The same-key burst executed more than one source."); }

        return results.Length;
    }

    /// <summary>Completes two cold overlapping ordered batches with four delivered entries, three owned keys, and no duplicate source work.</summary>
    /// <remarks>Both batch sources reach a barrier before either publishes. The shared key is loaded exactly once and both callers receive its confirmed retained result.</remarks>
    [Benchmark]
    public async Task<int> OverlappingColdBatchesAsync()
    {
        string[] keys = ["overlap:left", "overlap:shared", "overlap:right"];
        foreach (var key in keys) { CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync(key)); }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sources = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var owned = 0;
        var batchCalls = 0;
        async IAsyncEnumerable<CacheBatchLoadResult<string>> Load(CacheBatchLoadContext context, [EnumeratorCancellation] CancellationToken token)
        {
            Interlocked.Increment(ref batchCalls);
            foreach (var item in context.Items) { sources.AddOrUpdate(item.Item.Key, 1, static (_, count) => count + 1); }

            if (Interlocked.Add(ref owned, context.Items.Count) >= keys.Length) { ready.TrySetResult(); }

            await release.Task.WaitAsync(token);
            foreach (var item in context.Items)
            {
                yield return new(item.Item.Key, Result<CacheLoadResult<string>>.Success(new(item.Item.Key + this.value)));
            }
        }

        CacheItem[][] batches = [[new(keys[0]), new(keys[1])], [new(keys[1]), new(keys[2])]];
        var tasks = batches.Select(items => this.environment.Collection.GetOrLoadManyAsync(items, (CacheBatchLoadDelegate<string>)Load).AsTask()).ToArray();
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(tasks);
        for (var batch = 0; batch < results.Length; batch++)
        {
            CacheBenchmarkEnvironment.Ensure(results[batch]);
            if (results[batch].Value.Count != batches[batch].Length) { throw new InvalidDataException("An overlapping batch omitted an ordered result."); }

            for (var item = 0; item < batches[batch].Length; item++)
            {
                var result = results[batch].Value[item];
                CacheBenchmarkEnvironment.Ensure(result);
                if (!result.Value.Found || result.Value.Version is null || result.Value.Value != batches[batch][item].Key + this.value)
                {
                    throw new InvalidDataException("An overlapping batch returned incomplete or incorrectly ordered retained values.");
                }
            }
        }

        if (batchCalls != 2 || sources.Count != keys.Length || sources.Values.Any(count => count != 1))
        {
            throw new InvalidDataException("Overlapping batches did not execute exactly two source batches owning three distinct keys once each.");
        }

        return results.Sum(result => result.Value.Count);
    }

    /// <summary>Disposes the real native host and its owned temporary files.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}