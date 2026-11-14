// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Measures strictly validated decoded reuse against its identical cold representation path.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheHotEntryBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheHotEntryBenchmarks
{
    private CacheBenchmarkEnvironment environment;
    private CacheBenchmarkProviderBehavior observed;
    private int position;

    /// <summary>Gets or sets the complete provider for authoritative validity on every reuse.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite"]);

    /// <summary>Gets or sets the explicitly measured local representation, with identical payload and strict validity semantics.</summary>
    [Params("Off", "Binary", "Decoded", "BinaryAndDecoded")]
    public string Retention { get; set; }

    /// <summary>Gets or sets the exact seeded key cardinality for hot-key and distinct-scan profiles.</summary>
    [ParamsSource(nameof(EntryProfiles))]
    public int Entries { get; set; }

    /// <summary>Gets data-set counts selected by BDK_CACHE_BENCH_ENTRIES.</summary>
    public IReadOnlyList<int> EntryProfiles => CacheBenchmarkEnvironment.SelectEntryCounts();

    /// <summary>Seeds and performs second-access admission before measurement.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        var representation = this.Retention == "Off" ? CacheHotEntryRepresentation.Decoded : Enum.Parse<CacheHotEntryRepresentation>(this.Retention);
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, this.Entries, 4096, this.Retention != "Off", hotRepresentation: representation,
            providerBehavior: native => this.observed = new(native));
        for (var access = 0; access < 3; access++)
        {
            CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.GetAsync("seed:0"));
        }

        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Measures complete same-version reuse, including authoritative provider metadata validation.</summary>
    [Benchmark]
    public async Task<int> GetAsync()
    {
        var validations = this.observed.Validations;
        var result = await this.environment.Collection.GetAsync("seed:0");
        CacheBenchmarkEnvironment.Ensure(result);
        if (!result.Value.Found || result.Value.Value.Length != 4096) { throw new InvalidDataException("The hot workload did not return the full seeded value."); }

        if (this.Retention != "Off" && this.observed.Validations != validations + 1)
        {
            throw new InvalidDataException("A hot reuse did not perform exactly one authoritative provider validation.");
        }

        return result.Value.Value.Length;
    }

    /// <summary>Reads up to 32 successive distinct keys and completes each decode, exposing admission and scan turnover under the same finite hot limits.</summary>
    /// <remarks>The exact successful entry count is min(32, Entries), rather than one entry per scenario.</remarks>
    [Benchmark]
    public async Task<int> DistinctScanAsync()
    {
        var count = Math.Min(32, this.Entries);
        for (var index = 0; index < count; index++)
        {
            var result = await this.environment.Collection.GetAsync("seed:" + (this.position++ % this.Entries));
            CacheBenchmarkEnvironment.Ensure(result);
            if (!result.Value.Found || result.Value.Value.Length != 4096 || result.Value.Version is null)
            {
                throw new InvalidDataException("A distinct scan did not acquire every complete retained entry.");
            }
        }

        return count;
    }

    /// <summary>Completes a fixed 32-read skew with 24 reads targeting the hot key and eight scanning the seeded working set.</summary>
    /// <remarks>Uses the same finite retention and authoritative validity policy as the distinct and same-key profiles.</remarks>
    [Benchmark]
    public async Task<int> SkewedReadsAsync()
    {
        for (var index = 0; index < 32; index++)
        {
            var key = index % 4 == 0 ? this.position++ % this.Entries : 0;
            var result = await this.environment.Collection.GetAsync("seed:" + key);
            CacheBenchmarkEnvironment.Ensure(result);
            if (!result.Value.Found || result.Value.Value.Length != 4096 || result.Value.Version is null)
            {
                throw new InvalidDataException("The skewed profile did not deliver all complete current entries.");
            }
        }

        return 32;
    }

    /// <summary>Replaces a retained hot key and verifies the current version through three complete reads and new-version admission.</summary>
    /// <remarks>Includes the old snapshot read, replacement publication and new snapshot reads, not just a warm-hit getter.</remarks>
    [Benchmark]
    public async Task<int> VersionTurnoverAsync()
    {
        var previous = await this.environment.Collection.GetAsync("seed:0");
        CacheBenchmarkEnvironment.Ensure(previous);
        var replacement = new string('v', 4096);
        var written = await this.environment.Collection.SetAsync(new("seed:0"), replacement);
        CacheBenchmarkEnvironment.Ensure(written);
        if (written.Value.StorageOutcome != CacheStorageOutcome.Stored || written.Value.Version == previous.Value.Version)
        {
            throw new InvalidDataException("Turnover did not publish a distinct version.");
        }

        for (var read = 0; read < 3; read++)
        {
            var result = await this.environment.Collection.GetAsync("seed:0");
            CacheBenchmarkEnvironment.Ensure(result);
            if (!result.Value.Found || result.Value.Value != replacement || result.Value.Version != written.Value.Version)
            {
                throw new InvalidDataException("A turnover read reused the obsolete retained representation.");
            }
        }

        return 3;
    }

    /// <summary>Releases every pinned representation and native provider.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        Console.WriteLine($"[cache-hot-boundary-counts] retention={this.Retention} validations={this.observed.Validations} payloadLookups={this.observed.PayloadLookups}");
        await this.environment.DisposeAsync();
    }
}