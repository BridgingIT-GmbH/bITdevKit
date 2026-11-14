// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using System.Collections.Concurrent;

/// <summary>Measures a bounded burst across actual jittered expiration boundaries with exact expected hit and source counts.</summary>
/// <remarks>Each invocation seeds 32 entries, advances UTC validity between completed operations, and fully consumes all 32 lookup results. Real cancellation timers and elapsed-time measurement remain unchanged.</remarks>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheExpiryBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheExpiryBenchmarks
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private CacheBenchmarkEnvironment environment;
    private CacheBenchmarkClock clock;

    /// <summary>Gets or sets the explicit complete native provider.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite"]);

    /// <summary>Creates a finite host with 20% downward TTL jitter and a separately labeled controlled validity clock.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.clock = new();
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256, policy: "Jitter", concurrency: 32,
            clock: this.clock, absoluteTtl: Lifetime);
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Completes 32 simultaneous lookups and verifies one retained source result for each expired key and zero source work for every valid key.</summary>
    /// <remarks>Seeding, persisted-jitter validation, validity advancement, all lookups, encoding, and publication are included. The cutoff uses the largest observed deadline gap; no particular random distribution is assumed.</remarks>
    [Benchmark]
    public async Task<int> JitteredExpiryBurstAsync()
    {
        var metadata = new CacheEntryMetadata[32];
        var original = new string('o', 256);
        var replacement = new string('r', 256);
        for (var index = 0; index < metadata.Length; index++)
        {
            var seeded = await this.environment.Collection.SetAsync(new("expiry:" + index), original);
            CacheBenchmarkEnvironment.Ensure(seeded);
            if (seeded.Value.StorageOutcome != CacheStorageOutcome.Stored || seeded.Value.Metadata is null)
            {
                throw new InvalidDataException("The expiry workload did not retain every seeded version.");
            }

            metadata[index] = seeded.Value.Metadata;
            var lifetime = metadata[index].ExpiresAt - metadata[index].PublishedAt;
            if (lifetime < Lifetime * 0.8 || lifetime > Lifetime || metadata[index].AbsoluteExpiresAt != metadata[index].ExpiresAt)
            {
                throw new InvalidDataException("A persisted jittered lifetime escaped its declared downward-only bounds.");
            }
        }

        var deadlines = metadata.Select(item => item.ExpiresAt).Order().ToArray();
        var cutoff = deadlines[^1].AddSeconds(1);
        var largestGap = TimeSpan.Zero;
        for (var index = 1; index < deadlines.Length; index++)
        {
            var gap = deadlines[index] - deadlines[index - 1];
            if (gap <= largestGap) { continue; }

            largestGap = gap;
            cutoff = deadlines[index - 1].AddTicks(gap.Ticks / 2);
        }

        var advance = cutoff - this.clock.GetUtcNow();
        if (advance > TimeSpan.Zero) { this.clock.Advance(advance); }

        var observedAt = this.clock.GetUtcNow();
        var expected = metadata.Select(item => item.ExpiresAt <= observedAt).ToArray();
        var sources = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext context, CancellationToken _)
        {
            sources.AddOrUpdate(context.Item.Key, 1, static (_, count) => count + 1);
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new(replacement)));
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Result<CacheLookup<string>>> ReadAsync(int index)
        {
            await start.Task;
            return await this.environment.Collection.GetOrLoadAsync(new("expiry:" + index), Load);
        }

        var tasks = Enumerable.Range(0, metadata.Length).Select(ReadAsync).ToArray();
        start.TrySetResult();
        var results = await Task.WhenAll(tasks);
        for (var index = 0; index < results.Length; index++)
        {
            var result = results[index];
            CacheBenchmarkEnvironment.Ensure(result);
            var calls = sources.GetValueOrDefault("expiry:" + index);
            if (!result.Value.Found || result.Value.Version is null || result.Value.Value != (expected[index] ? replacement : original) ||
                calls != (expected[index] ? 1 : 0) || (expected[index] && result.Value.StorageOutcome != CacheStorageOutcome.Stored))
            {
                throw new InvalidDataException("The expiry burst's completed values or source count did not match the persisted jittered deadlines.");
            }
        }

        if (sources.Count != expected.Count(expired => expired)) { throw new InvalidDataException("The expiry burst executed unexpected source work."); }

        return results.Length;
    }

    /// <summary>Disposes actual provider work and reports the cumulative validity-clock advance with finite resource limits.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}