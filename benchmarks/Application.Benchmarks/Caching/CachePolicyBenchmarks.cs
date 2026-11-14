// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

/// <summary>Measures explicitly selected traffic, partial-result, invalidation, capacity and provider-failure scenarios through complete cache hosts.</summary>
/// <remarks>Scenario totals include their stated preparation and completion work. A failure/rejection count is never positive-entry throughput.</remarks>
/// <example><code>$env:BDK_CACHE_BENCH_POLICY_CASES = 'TrackedInvalidation'; dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CachePolicyBenchmarks*</code></example>
[MemoryDiagnoser]
public class CachePolicyBenchmarks
{
    private static readonly string[] Cases = ["DistinctCold", "MixedTraffic", "PartialBatch", "SelectionInvalidate", "TrackedInvalidation", "Clear",
        "EvictionPressure", "ExpiredCleanup", "BreakerCycle", "DiagnosticsMinimal", "DiagnosticsStandard", "DiagnosticsDetailed"];
    private CacheBenchmarkEnvironment environment;
    private CacheBenchmarkProviderBehavior observed;
    private CacheBenchmarkClock clock;
    private string value;

    /// <summary>Gets or sets a complete real provider; breaker scenarios require a persistent provider.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets selected complete profiles, with SQLite as the normal local representative.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["Sqlite"]);

    /// <summary>Gets or sets the exact work profile visible in the report.</summary>
    [ParamsSource(nameof(ScenarioProfiles))]
    public string Scenario { get; set; }

    /// <summary>Gets all scenarios or the explicit BDK_CACHE_BENCH_POLICY_CASES subset.</summary>
    public IReadOnlyList<string> ScenarioProfiles
    {
        get
        {
            var setting = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_POLICY_CASES");
            var selected = setting is null ? Cases : setting.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selected.Length == 0 || selected.Any(name => !Cases.Contains(name, StringComparer.Ordinal))) { throw new ArgumentException("Unknown cache policy benchmark scenario."); }

            return selected.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Creates the selected finite host and its explicit logical clock, pressure or diagnostic policy.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        if (this.Scenario == "BreakerCycle" && this.Provider == "InMemory") { throw new ArgumentException("The provider breaker requires a persistent provider."); }

        this.clock = new();
        this.value = new('x', 256);
        var diagnostics = this.Scenario.StartsWith("Diagnostics", StringComparison.Ordinal) ? this.Scenario[11..] : "Default";
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256, concurrency: 32,
            policy: this.Scenario == "BreakerCycle" ? "Breaker" : "Plain", clock: this.clock,
            absoluteTtl: this.Scenario == "ExpiredCleanup" ? TimeSpan.FromMinutes(1) : null,
            providerBehavior: native => this.observed = new(native), diagnosticProfile: diagnostics,
            configureCollection: this.Scenario == "EvictionPressure" ? collection => collection.With(x => x.Limits.MaxEntries, 32L) : null);
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Completes the selected scenario and returns its explicitly verified successful logical work count.</summary>
    /// <remarks>Results are scenario-specific. PartialBatch returns three positive entries; BreakerCycle returns seven control outcomes; invalidation returns affected versions.</remarks>
    [Benchmark]
    public Task<int> RunAsync() => this.Scenario switch
    {
        "DistinctCold" => this.DistinctColdAsync(),
        "MixedTraffic" => this.MixedTrafficAsync(),
        "PartialBatch" => this.PartialBatchAsync(),
        "SelectionInvalidate" => this.SelectionInvalidateAsync(),
        "TrackedInvalidation" => this.TrackedInvalidationAsync(),
        "Clear" => this.ClearAsync(),
        "EvictionPressure" => this.EvictionPressureAsync(),
        "ExpiredCleanup" => this.ExpiredCleanupAsync(),
        "BreakerCycle" => this.BreakerCycleAsync(),
        _ => this.DiagnosticsAsync()
    };

    private async Task<int> DistinctColdAsync()
    {
        for (var index = 0; index < 32; index++) { CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync("distinct:" + index)); }

        var calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            if (Interlocked.Increment(ref calls) == 32) { entered.TrySetResult(); }

            await release.Task.WaitAsync(token);
            return Result<CacheLoadResult<string>>.Success(new(this.value));
        }

        var tasks = Enumerable.Range(0, 32).Select(index => this.environment.Collection.GetOrLoadAsync(new("distinct:" + index), Load).AsTask()).ToArray();
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { release.TrySetResult(); }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results) { this.AssertStored(result); }

        if (calls != 32) { throw new InvalidDataException("Distinct cold work did not execute exactly one source per key."); }

        return results.Length;
    }

    private async Task<int> MixedTrafficAsync()
    {
        await this.SeedAsync("mixed:", 32);
        var completed = await Task.WhenAll(Enumerable.Range(0, 32).Select(async index =>
        {
            if (index % 4 == 0)
            {
                var stored = await this.environment.Collection.SetAsync(new("mixed:" + index), this.value);
                CacheBenchmarkEnvironment.Ensure(stored);
                if (stored.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("A mixed write was not confirmed."); }
            }
            else
            {
                var read = await this.environment.Collection.GetAsync("mixed:" + index);
                this.AssertStored(read);
            }

            return 1;
        }));
        return completed.Sum();
    }

    private async Task<int> PartialBatchAsync()
    {
        string[] keys = ["partial:0", "partial:1", "partial:2", "partial:3"];
        foreach (var key in keys) { CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync(key)); }

        var calls = 0;
        var results = await this.environment.Collection.GetOrLoadManyAsync(keys.Select(key => new CacheItem(key)).ToArray(),
            (CacheLoadDelegate<string>)((context, _) =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(context.Item.Key.EndsWith(":2", StringComparison.Ordinal) ?
                    Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Selected benchmark source failure.")) :
                    Result<CacheLoadResult<string>>.Success(new(context.Item.Key + this.value)));
            }));
        CacheBenchmarkEnvironment.Ensure(results);
        for (var index = 0; index < keys.Length; index++)
        {
            var result = results.Value[index];
            if (index == 2)
            {
                if (result.IsSuccess) { throw new InvalidDataException("The explicitly failed batch source was hidden."); }
            }
            else
            {
                CacheBenchmarkEnvironment.Ensure(result);
                if (!result.Value.Found || result.Value.Value != keys[index] + this.value || result.Value.Version is null)
                {
                    throw new InvalidDataException("Partial batch result order or retained success was incorrect.");
                }
            }
        }

        if (calls != 4) { throw new InvalidDataException("Partial batch executed unexpected source work."); }

        return 3;
    }

    private async Task<int> SelectionInvalidateAsync()
    {
        await this.SeedAsync("selection:", 32);
        var selection = CacheSelection.Prefix("selection:");
        var preview = await this.environment.Collection.PreviewInvalidationAsync(selection);
        CacheBenchmarkEnvironment.Ensure(preview);
        if (!preview.Value.CountIsExact || preview.Value.Count != 32) { throw new InvalidDataException("The payload-free preview did not inspect all 32 selected entries."); }

        return await this.InvalidateToCompletionAsync(selection, false, 32);
    }

    private async Task<int> ClearAsync()
    {
        await this.ClearToCompletionAsync();
        await this.SeedAsync("clear:", 32);
        return await this.InvalidateToCompletionAsync(CacheSelection.None, true, 32);
    }

    private async Task<int> InvalidateToCompletionAsync(CacheSelection selection, bool clear, int expected)
    {
        var cutoff = this.clock.GetUtcNow();
        var request = new CacheProviderInvalidation("benchmark", "values", selection, cutoff, Clear: clear);
        long affected = 0;
        for (var unit = 0; unit < 64; unit++)
        {
            var result = await this.environment.Provider.InvalidateAsync(request);
            CacheBenchmarkEnvironment.Ensure(result);
            affected = result.Value.AffectedCount;
            if (result.Value.Completed)
            {
                if (affected != expected) { throw new InvalidDataException("The completed invalidation affected an unexpected number of versions."); }

                return checked((int)affected);
            }

            if (result.Value.Continuation is null) { throw new InvalidDataException("An incomplete native invalidation supplied no continuation."); }

            request = request with { Continuation = result.Value.Continuation };
        }

        throw new InvalidDataException("The finite invalidation did not complete within 64 bounded units.");
    }

    private async Task<int> TrackedInvalidationAsync()
    {
        await this.SeedAsync("tracked:", 260);
        var operations = this.environment.Services.GetRequiredService<CacheOperationService>();
        var accepted = await operations.SubmitAsync(new(["values"], CacheSelection.Prefix("tracked:"), Background: true));
        CacheBenchmarkEnvironment.Ensure(accepted);
        CacheBenchmarkEnvironment.Ensure(accepted.Value.Single());
        var submission = accepted.Value.Single().Value;
        if (!submission.Accepted || submission.Status.State != CacheOperationState.Pending) { throw new InvalidDataException("Tracked work was not accepted before mutation."); }

        var binding = this.environment.Services.GetRequiredService<CacheCatalog>().Collections["values"];
        var runner = this.environment.Services.GetRequiredService<CacheOperationRunner>();
        for (var unit = 0; unit < 64; unit++)
        {
            var status = await runner.RunUnitAsync(binding, submission.Handle.Id);
            CacheBenchmarkEnvironment.Ensure(status);
            if (status.Value.State == CacheOperationState.Completed)
            {
                if (!status.Value.Outcome.Completed || status.Value.Outcome.AffectedCount != 260) { throw new InvalidDataException("Tracked completion lost its cumulative confirmed effects."); }

                return 260;
            }

            if (status.Value.State is not (CacheOperationState.Pending or CacheOperationState.Running)) { throw new InvalidDataException("Tracked invalidation reached an unexpected state."); }
        }

        throw new InvalidDataException("Tracked invalidation exceeded its explicit continuation-unit bound.");
    }

    private async Task<int> BreakerCycleAsync()
    {
        this.observed.FailQuota = true;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await this.environment.Provider.GetQuotaAsync();
            if (failed.IsSuccess) { throw new InvalidDataException("A controlled infrastructure failure was hidden."); }
        }

        this.observed.FailQuota = false;
        var before = this.observed.QuotaCalls;
        var rejected = await this.environment.Provider.GetQuotaAsync();
        if (rejected.IsSuccess || this.observed.QuotaCalls != before) { throw new InvalidDataException("An open breaker reached the native provider."); }

        this.clock.Advance(TimeSpan.FromSeconds(11));
        var recovered = await this.environment.Provider.GetQuotaAsync();
        CacheBenchmarkEnvironment.Ensure(recovered);
        if (this.observed.QuotaCalls != before + 2) { throw new InvalidDataException("Recovery must perform one nonmutating native probe and the requested native quota read."); }

        return 7;
    }

    private async Task<int> EvictionPressureAsync()
    {
        await this.ClearToCompletionAsync();
        for (var index = 0; index < 32; index++)
        {
            var stored = await this.environment.Collection.SetAsync(new("pressure:" + index), this.value,
                descriptor: new() { Priority = index == 0 ? CacheEvictionPriority.Low : CacheEvictionPriority.Normal });
            CacheBenchmarkEnvironment.Ensure(stored);
            if (stored.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Pressure preparation did not store its complete finite working set."); }
        }

        var acquired = await this.environment.Collection.OpenReadAsync("pressure:0");
        CacheBenchmarkEnvironment.Ensure(acquired);
        if (!acquired.Value.Found) { throw new InvalidDataException("The pressure reader did not acquire its low-priority version."); }

        await using var pinned = acquired.Value.Value;
        var extra = await this.environment.Collection.SetAsync(new("pressure:new"), this.value);
        CacheBenchmarkEnvironment.Ensure(extra);
        var replacement = await this.environment.Collection.SetAsync(new("pressure:0"), new string('r', 256));
        CacheBenchmarkEnvironment.Ensure(replacement);
        var quota = await this.environment.Provider.GetQuotaAsync();
        CacheBenchmarkEnvironment.Ensure(quota);
        if (extra.Value.StorageOutcome != CacheStorageOutcome.Stored || replacement.Value.StorageOutcome != CacheStorageOutcome.Stored ||
            replacement.Value.Version == pinned.Metadata.Version || quota.Value.CurrentEntries is <= 0 or > 32)
        {
            throw new InvalidDataException($"Pressure failed: added={extra.Value.StorageOutcome}, replacement={replacement.Value.StorageOutcome}, sameVersion={replacement.Value.Version == pinned.Metadata.Version}, entries={quota.Value.CurrentEntries}, retiredBytes={quota.Value.RetiredBytes}.");
        }

        var original = await JsonSerializer.DeserializeAsync<string>(pinned.Stream);
        if (original != this.value) { throw new InvalidDataException("Replacement changed an already acquired snapshot."); }

        await pinned.Stream.CopyToAsync(Stream.Null);
        var completion = await pinned.WaitForCompletionAsync();
        CacheBenchmarkEnvironment.Ensure(completion);
        if (completion.Value != CacheReadCompletion.Validated) { throw new InvalidDataException("Eviction invalidated an already acquired snapshot."); }

        return 2;
    }

    private async Task<int> ExpiredCleanupAsync()
    {
        await this.ClearToCompletionAsync();
        await this.SeedAsync("expired:", 32);
        this.clock.Advance(TimeSpan.FromMinutes(6));
        var missed = await this.environment.Collection.GetAsync("expired:0");
        CacheBenchmarkEnvironment.Ensure(missed);
        if (missed.Value.Found) { throw new InvalidDataException("An expired entry was served before physical cleanup."); }

        long retired = 0;
        for (var unit = 0; unit < 64; unit++)
        {
            var now = this.clock.GetUtcNow();
            var cleanup = await this.environment.Provider.MaintainAsync(new(256, now, now.AddSeconds(5)));
            CacheBenchmarkEnvironment.Ensure(cleanup);
            retired += cleanup.Value.ReclaimedEntries;
            var quota = await this.environment.Provider.GetQuotaAsync();
            CacheBenchmarkEnvironment.Ensure(quota);
            if (quota.Value.CurrentEntries == 0 && retired >= 32) { return 32; }
        }

        throw new InvalidDataException("The expired working set exceeded 64 bounded maintenance units.");
    }

    private async Task<int> DiagnosticsAsync()
    {
        var logged = this.environment.LogRecords;
        var result = await this.environment.Collection.GetAsync("seed:0");
        this.AssertStored(result);
        if (this.Scenario == "DiagnosticsDetailed" && this.environment.LogRecords <= logged)
        {
            throw new InvalidDataException("The detailed logging profile did not emit its enabled cache record.");
        }

        return 1;
    }

    private async Task SeedAsync(string prefix, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var result = await this.environment.Collection.SetAsync(new(prefix + index), this.value);
            CacheBenchmarkEnvironment.Ensure(result);
            if (result.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("Scenario preparation did not store every selected entry."); }
        }
    }

    private async Task ClearToCompletionAsync()
    {
        var cutoff = this.clock.GetUtcNow();
        var request = new CacheProviderInvalidation("benchmark", "values", CacheSelection.None, cutoff, Clear: true);
        for (var unit = 0; unit < 64; unit++)
        {
            var result = await this.environment.Provider.InvalidateAsync(request);
            CacheBenchmarkEnvironment.Ensure(result);
            if (result.Value.Completed) { return; }

            if (result.Value.Continuation is null) { throw new InvalidDataException("Clear supplied no continuation for incomplete work."); }

            request = request with { Continuation = result.Value.Continuation };
        }

        throw new InvalidDataException("Clear exceeded 64 bounded continuation units.");
    }

    private void AssertStored(Result<CacheLookup<string>> result)
    {
        CacheBenchmarkEnvironment.Ensure(result);
        if (!result.Value.Found || result.Value.Value != this.value || result.Value.Version is null) { throw new InvalidDataException("A scenario returned an incomplete retained answer."); }
    }

    /// <summary>Closes actual work and writes separately labeled provider-boundary/SQL counts before deleting the dedicated temporary root.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        Console.WriteLine($"[cache-policy-work] scenario={this.Scenario} providerLookups={this.observed.PayloadLookups} validations={this.observed.Validations} nativeQuotaCalls={this.observed.QuotaCalls} sqlCommands={this.environment.SqlCommands}");
        await this.environment.DisposeAsync();
    }
}