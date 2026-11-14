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

/// <summary>Measures equal-size admitted source cohorts with explicitly verified success, failure, departure, fencing and uncached outcomes.</summary>
/// <remarks>Each scenario has 16 submitted callers. Source-only executes 16 computations; shared cases execute one; independent mode executes 16. Polling the bounded join counter keeps source completion behind actual follower registration.</remarks>
/// <example><code>$env:BDK_CACHE_BENCH_SOURCE_CASES = 'SharedSuccess,IndependentSuccess'; dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheSourceOutcomeBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheSourceOutcomeBenchmarks
{
    private static readonly string[] Cases = ["SharedSuccess", "IndependentSuccess", "SourceOnly", "SharedFailure", "SharedInvalidation",
        "SharedSuppressed", "CallerDeparture", "SourceTimeout"];
    private CacheBenchmarkEnvironment environment;
    private CacheObservationSlot observations;

    /// <summary>Gets or sets a real complete provider.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets selected complete native profiles.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite"]);

    /// <summary>Gets or sets the source scenario visible in every result.</summary>
    [ParamsSource(nameof(ScenarioProfiles))]
    public string Scenario { get; set; }

    /// <summary>Gets all scenarios or the finite BDK_CACHE_BENCH_SOURCE_CASES subset.</summary>
    public IReadOnlyList<string> ScenarioProfiles
    {
        get
        {
            var setting = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_SOURCE_CASES");
            var selected = setting is null ? Cases : setting.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selected.Length == 0 || selected.Any(value => !Cases.Contains(value, StringComparer.Ordinal))) { throw new ArgumentException("Unknown source outcome benchmark scenario."); }

            return selected.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Creates equal finite admission limits; only SourceTimeout lowers the owned source budget to an explicit five seconds.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256, concurrency: 16,
            strategy: this.Scenario == "IndependentSuccess" ? CacheLoadingStrategy.Independent : CacheLoadingStrategy.Shared,
            configureCollection: this.Scenario == "SourceTimeout" ? collection => collection.With(x => x.Loading.SourceTimeout, TimeSpan.FromSeconds(5)) : null);
        this.observations = this.environment.Services.GetRequiredService<CacheDiagnostics>().ForCollection("values");
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Completes the selected cohort and checks actual source calls, every result, and whether the final key was retained.</summary>
    /// <remarks>Failure/timeout cohorts return completed failure observations, not positive entries; CallerDeparture returns15 delivered entries and one cancelled waiter. SourceTimeout intentionally includes its five-second deadline.</remarks>
    [Benchmark]
    public async Task<int> RunAsync()
    {
        const int Callers = 16;
        var expectedSources = this.Scenario is "IndependentSuccess" or "SourceOnly" ? Callers : 1;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            var started = Stopwatch.GetTimestamp();
            if (Interlocked.Increment(ref calls) == expectedSources) { entered.TrySetResult(); }

            try
            {
                if (this.Scenario == "SourceTimeout") { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                else { await release.Task.WaitAsync(token); }

                return this.Scenario == "SharedFailure" ?
                    Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Selected benchmark source failure.")) :
                    Result<CacheLoadResult<string>>.Success(new(new string('x', 256), new() { SuppressStorage = this.Scenario == "SharedSuppressed" }));
            }
            finally
            {
                this.environment.Measurements.RecordDuration("Source", started);
                ended.TrySetResult();
            }
        }

        if (this.Scenario == "SourceOnly")
        {
            var work = Enumerable.Range(0, Callers).Select(_ => this.environment.Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), Load(null, default))).ToArray();
            try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
            finally { release.TrySetResult(); }

            var results = await Task.WhenAll(work);
            if (calls != Callers || results.Any(result => result.IsFailure || result.Value.Value.Length != 256))
            {
                throw new InvalidDataException("The source-only cohort did not complete its 16 computations.");
            }

            return Callers;
        }

        CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync("source:outcome"));
        var joins = this.JoinCount();
        using var leaving = new CancellationTokenSource();
        var owner = this.environment.Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), this.environment.Collection.GetOrLoadAsync(new("source:outcome"), Load, leaving.Token));
        if (expectedSources == 1) { await entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); }

        var followers = Enumerable.Range(1, Callers - 1).Select(_ => this.environment.Measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), this.environment.Collection.GetOrLoadAsync(new("source:outcome"), Load))).ToArray();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (expectedSources == 1)
            {
                using var registrationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (this.JoinCount() - joins < Callers - 1) { await Task.Delay(1, registrationDeadline.Token); }
            }

            if (this.Scenario == "SharedInvalidation") { CacheBenchmarkEnvironment.Ensure(await this.environment.Collection.RemoveAsync("source:outcome")); }

            if (this.Scenario == "CallerDeparture") { await leaving.CancelAsync(); }
        }
        finally { if (this.Scenario != "SourceTimeout") { release.TrySetResult(); } }

        var expectedDelivered = Callers;
        if (this.Scenario == "CallerDeparture")
        {
            var cancelled = false;
            try { await owner; }
            catch (OperationCanceledException) { cancelled = true; }

            if (!cancelled) { throw new InvalidDataException("The departing initiating caller did not stop waiting."); }

            expectedDelivered--;
        }

        var completed = await Task.WhenAll(this.Scenario == "CallerDeparture" ? followers : followers.Prepend(owner));
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(20));
        if (calls != expectedSources || completed.Length != expectedDelivered) { throw new InvalidDataException("The cohort changed source count or lost completed caller observations."); }

        foreach (var result in completed)
        {
            if (this.Scenario is "SharedFailure" or "SourceTimeout")
            {
                var expected = this.Scenario == "SourceTimeout" ? CacheErrorCode.SourceTimeout : CacheErrorCode.SourceFailure;
                if (result.IsSuccess || !result.Errors.OfType<CacheError>().Any(error => error.Code == expected))
                {
                    throw new InvalidDataException("A shared source failure/deadline was hidden or reclassified.");
                }
            }
            else
            {
                CacheBenchmarkEnvironment.Ensure(result);
                var storage = this.Scenario == "SharedInvalidation" ? CacheStorageOutcome.Conflict :
                    this.Scenario == "SharedSuppressed" ? CacheStorageOutcome.Suppressed : CacheStorageOutcome.Stored;
                var validStorage = this.Scenario == "IndependentSuccess" ?
                    result.Value.StorageOutcome is CacheStorageOutcome.Stored or CacheStorageOutcome.Conflict : result.Value.StorageOutcome == storage;
                if (!result.Value.Found || result.Value.Value.Length != 256 || !validStorage ||
                    (result.Value.StorageOutcome == CacheStorageOutcome.Stored) != result.Value.Version.HasValue)
                {
                    throw new InvalidDataException("A source cohort returned an incorrect value, retention outcome or confirmed-version token.");
                }
            }
        }

        if (this.Scenario == "IndependentSuccess" && !completed.Any(result => result.Value.StorageOutcome == CacheStorageOutcome.Stored))
        {
            throw new InvalidDataException("No independent attempt confirmed a stored publication.");
        }

        var retained = await this.environment.Collection.GetAsync("source:outcome");
        CacheBenchmarkEnvironment.Ensure(retained);
        var storedExpected = this.Scenario is "SharedSuccess" or "IndependentSuccess" or "CallerDeparture";
        if (retained.Value.Found != storedExpected || retained.Value.IsNotFound) { throw new InvalidDataException("Source outcomes produced an incorrect retained value or absence marker."); }

        return completed.Length;
    }

    private long JoinCount() => this.observations.Snapshot().Counters.GetValueOrDefault(CacheObservationKind.Join) ?? 0;

    /// <summary>Closes the owned host after the full cohort and all actual source callbacks have ended.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();
}