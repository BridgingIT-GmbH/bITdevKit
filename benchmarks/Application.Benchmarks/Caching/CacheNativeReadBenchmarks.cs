// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

/// <summary>Compares complete plain binary reads through the collection engine and directly through native snapshot/envelope contracts.</summary>
/// <remarks>Both paths check the acquired version, every frame, terminal integrity, exact length and every returned byte. DirectNative excludes collection dispatch, observer and transfer-admission overhead.</remarks>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheNativeReadBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheNativeReadBenchmarks
{
    private CacheBenchmarkEnvironment environment;

    /// <summary>Gets or sets the complete provider under measurement.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles selected by BDK_CACHE_BENCH_PROVIDERS.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite", "FileSystem"]);

    /// <summary>Gets or sets the fixed raw logical bytes returned by each completed entry read.</summary>
    [Params(256, 4096)]
    public int PayloadBytes { get; set; }

    /// <summary>Stores one uncompressed binary resource and resets setup counters before measurement.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256);
        using var source = new MemoryStream(new byte[this.PayloadBytes], writable: false);
        var stored = await this.environment.Binary.SetAsync(new("native-baseline"), source);
        CacheBenchmarkEnvironment.Ensure(stored);
        if (stored.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Native baseline preparation was not stored."); }

        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Completes ordinary binary collection acquisition, consumption, verification and owned-handle disposal.</summary>
    [Benchmark]
    public async Task<long> EngineAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var opened = await this.environment.Binary.OpenReadAsync("native-baseline");
        CacheBenchmarkEnvironment.Ensure(opened);
        if (!opened.Value.Found || opened.Value.Version is null) { throw new InvalidDataException("The engine baseline did not acquire its valid version."); }

        long bytes;
        await using (var handle = opened.Value.Value)
        {
            bytes = await this.VerifyZerosAsync(handle.Stream);
            var completion = await handle.WaitForCompletionAsync();
            CacheBenchmarkEnvironment.Ensure(completion);
            if (completion.Value != CacheReadCompletion.Validated) { throw new InvalidDataException("The engine did not validate the complete representation."); }
        }

        this.environment.Measurements.RecordDuration("EngineRead", started);
        return bytes;
    }

    /// <summary>Completes native validity/snapshot acquisition and the same persisted-envelope and logical-byte verification without collection dispatch.</summary>
    [Benchmark(Baseline = true)]
    public async Task<long> DirectNativeAsync()
    {
        var started = Stopwatch.GetTimestamp();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var opened = await this.environment.Provider.LookupAsync(new("benchmark", "binary", "1", "native-baseline"), new(OpenPayload: true), lifetime.Token);
        CacheBenchmarkEnvironment.Ensure(opened);
        if (opened.Value.State != CacheReadValidationState.Valid || opened.Value.Handle is null || opened.Value.Metadata.Compression is not (null or "none"))
        {
            throw new InvalidDataException("The direct baseline did not acquire its plain authoritative snapshot.");
        }

        long bytes;
        await using (var handle = opened.Value.Handle)
        {
            var resources = this.environment.Services.GetRequiredService<CacheRuntimeResources>();
            var framed = await CacheEnvelopeReader.OpenAsync(handle.Stream, handle.Metadata, resources.Transient, resources.ForCollection("binary").Resident, lifetime.Token);
            CacheBenchmarkEnvironment.Ensure(framed);
            await using var envelope = framed.Value;
            bytes = await this.VerifyZerosAsync(envelope, lifetime.Token);
            CacheBenchmarkEnvironment.Ensure(await envelope.VerifyAsync(false, lifetime.Token));
        }

        this.environment.Measurements.RecordDuration("DirectNativeRead", started);
        return bytes;
    }

    private async Task<long> VerifyZerosAsync(Stream stream, CancellationToken token = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        lifetime.CancelAfter(TimeSpan.FromMinutes(5));
        var buffer = new byte[64 * 1024];
        long total = 0;
        int taken;
        do
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            idle.CancelAfter(TimeSpan.FromSeconds(30));
            taken = await stream.ReadAsync(buffer, idle.Token);
            if (buffer.AsSpan(0, taken).IndexOfAnyExcept((byte)0) >= 0) { throw new InvalidDataException("The baseline returned incorrect logical bytes."); }

            total += taken;
        }
        while (taken != 0);

        if (total != this.PayloadBytes) { throw new InvalidDataException("The baseline returned an incomplete resource."); }

        return total;
    }

    /// <summary>Stops actual work and emits SQL/resource observations before temporary-store cleanup.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}