// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>Collects reproducible capacity evidence separately from ordinary correctness tests.</summary>
/// <param name="fixture">Isolated disposable database containers used by existing provider tests.</param>
/// <param name="output">Progress output and measured totals.</param>
/// <example>BITDEVKIT_PROFILING_PERF=1 dotnet test --filter FullyQualifiedName~ProfilingPerformanceEvidenceTests</example>
[IntegrationTest("Infrastructure")]
[Collection(nameof(JobsTestEnvironmentCollection))]
public sealed class ProfilingPerformanceEvidenceTests(JobsTestEnvironmentFixture fixture, ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Dictionary<string, ProfilingLoadResult> checkpoint = new(StringComparer.Ordinal);

    /// <summary>Runs three 60-second trials after 10-second warm-up per case/provider and tests explicit sampling when writers fall behind.</summary>
    [ProfilingPerformanceFact]
    public async Task Kestrel_AllProviders_ProducesRawCapacityAndOnOffEvidence()
    {
        var smoke = Environment.GetEnvironmentVariable("BITDEVKIT_PROFILING_PERF_SMOKE") == "1";
        var directory = ResolveEvidenceDirectory(smoke);
        Directory.CreateDirectory(directory);
        var providers = SelectProviders(Environment.GetEnvironmentVariable("BITDEVKIT_PROFILING_PERF_PROVIDERS"));
        var metadata = new
        {
            capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), smoke,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            framework = RuntimeInformation.FrameworkDescription, processors = Environment.ProcessorCount,
            serverGc = System.Runtime.GCSettings.IsServerGC, availableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            engines = await EngineVersionsAsync(), providers, seed = 1729, offeredRate = smoke ? 100 : 1000,
            repetitions = smoke ? 1 : 3, warmupSeconds = smoke ? 0.25 : 10, measuredSeconds = smoke ? 1 : 60,
            scope = "synthetic loopback Kestrel/client/test process; serial trials; four bounded nested invocations; no recorder-only or universal overhead claim",
            disabled = "master flag off with optional pipeline behavior still executing",
            empty = "generic root capture only; same native compute/optional behavior work without segment instrumentation",
            adjustment = "if default nested capture drops or ends above one batch, repeat at seeded 0.1 probability, then 0.01 if still necessary",
        };
        var environment = JsonSerializer.SerializeToElement(metadata, json);
        if (Environment.GetEnvironmentVariable("BITDEVKIT_PROFILING_PERF_RESUME") == "1")
        {
            var previous = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "environment.json")));
            using (previous) { ValidateEnvironment(previous.RootElement, environment); }

            var retained = JsonSerializer.Deserialize<List<ProfilingLoadResult>>(await File.ReadAllTextAsync(Path.Combine(directory, "results.json")), json);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var result in retained)
            {
                var stem = $"{result.Provider}-{result.Scenario}-{result.Repetition:00}";
                ValidateTrial(result, result.Configuration.GetProperty("smoke").GetBoolean(), result.Configuration.GetProperty("probability").GetDouble());
                await using var file = File.OpenRead(Path.Combine(directory, stem + ".latencies.json.gz"));
                await using var gzip = new GZipStream(file, CompressionMode.Decompress);
                var latencies = await JsonSerializer.DeserializeAsync<long[]>(gzip);
                latencies.Length.ShouldBe((int)result.Metrics["offeredCount"]);
                latencies.Count(value => value > 0).ShouldBe((int)result.Metrics["completedCount"]);
                this.checkpoint.Add(stem, result);
                foreach (var suffix in new[] { ".json", ".latencies.json.gz" })
                {
                    var name = stem + suffix;
                    hashes.Add(name, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, name)))));
                }
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "resume-environment-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + ".json"),
                JsonSerializer.Serialize(new { environment, reusedTrials = this.checkpoint.Count, preservedArtifactsSha256 = hashes }, json));
            output.WriteLine("Validated {0} completed trials for reuse; interrupted trials will run again.", this.checkpoint.Count);
        }
        else
        {
            if (File.Exists(Path.Combine(directory, "results.json"))) { throw new InvalidOperationException("Existing evidence requires BITDEVKIT_PROFILING_PERF_RESUME=1 or a new output directory."); }

            await File.WriteAllTextAsync(Path.Combine(directory, "environment.json"), JsonSerializer.Serialize(metadata, json));
        }

        var results = new List<ProfilingLoadResult>();
        foreach (var provider in providers)
        {
            foreach (var scenario in new[] { "omitted", "disabled", "empty", "nested", "nested-trace", "saturated" })
            {
                for (var repetition = 1; repetition <= (smoke ? 1 : 3); repetition++)
                {
                    var result = await TrialAsync(provider, scenario, repetition, smoke, 1, directory);
                    results.Add(result);
                    await SaveSummaryAsync(directory, results);
                }
            }

            var defaults = results.Where(result => result.Provider == provider && result.Scenario == "nested").ToArray();
            if (defaults.Any(NeedsAdjustment))
            {
                var adjusted = new List<ProfilingLoadResult>();
                for (var repetition = 1; repetition <= (smoke ? 1 : 3); repetition++)
                {
                    var result = await TrialAsync(provider, "nested", repetition, smoke, 0.1, directory);
                    adjusted.Add(result); results.Add(result);
                    await SaveSummaryAsync(directory, results);
                }

                if (adjusted.Any(NeedsAdjustment))
                {
                    for (var repetition = 1; repetition <= (smoke ? 1 : 3); repetition++)
                    {
                        results.Add(await TrialAsync(provider, "nested", repetition, smoke, 0.01, directory));
                        await SaveSummaryAsync(directory, results);
                    }
                }
            }
        }

        results.Count.ShouldBeGreaterThanOrEqualTo(providers.Length * 6 * (smoke ? 1 : 3));
        File.Exists(Path.Combine(directory, "summary.md")).ShouldBeTrue();
        await File.WriteAllTextAsync(Path.Combine(directory, "progress.txt"), "Completed " + results.Count + " trials " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>Rejects checkpoint reuse when the measured environment or workload changes.</summary>
    [Fact]
    public void ResumeEnvironment_ChangedWorkload_IsRejected()
    {
        var original = JsonSerializer.SerializeToElement(new { seed = 1729, capturedUtc = "previous", engines = new { sqlite = "same" } });
        var compatible = JsonSerializer.SerializeToElement(new { seed = 1729, capturedUtc = "later", engines = new { sqlite = "same" } });
        ValidateEnvironment(original, compatible);
        var changed = JsonSerializer.SerializeToElement(new { seed = 1, capturedUtc = "later", engines = new { sqlite = "same" } });
        Should.Throw<InvalidOperationException>(() => ValidateEnvironment(original, changed));
    }

    /// <summary>Checks explicit provider selection without silently omitting unknown engines.</summary>
    /// <example>BITDEVKIT_PROFILING_PERF_PROVIDERS=memory reruns the affected provider matrix.</example>
    [Fact]
    public void ProviderSelection_ExplicitSubset_IsValidated()
    {
        SelectProviders(null).Length.ShouldBe(4);
        SelectProviders("memory,memory, postgres").ShouldBe(["memory", "postgres"]);
        Should.Throw<InvalidOperationException>(() => SelectProviders("unknown"));
        Should.Throw<InvalidOperationException>(() => SelectProviders(" , "));
    }

    private static string[] SelectProviders(string supplied)
    {
        var known = new[] { "memory", "sqlite", "sqlserver", "postgres" };
        if (supplied is null) { return known; }

        var selected = supplied.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0 || selected.Any(value => !known.Contains(value, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Select at least one supported profiling performance provider: memory, sqlite, sqlserver, postgres.");
        }

        return selected;
    }

    private static void ValidateEnvironment(JsonElement previous, JsonElement current)
    {
        foreach (var property in previous.EnumerateObject())
        {
            if (property.Name is "capturedUtc" or "availableMemoryBytes") { continue; }

            if (!current.TryGetProperty(property.Name, out var value) || property.Value.GetRawText() != value.GetRawText())
            {
                throw new InvalidOperationException("Cannot reuse performance evidence after an environment change: " + property.Name);
            }
        }
    }

    private static bool NeedsAdjustment(ProfilingLoadResult value) => value.Metrics["droppedWithinWindow"] > 0 || value.Metrics["endQueueRecords"] > 512;

    private async Task<ProfilingLoadResult> TrialAsync(string provider, string scenario, int repetition, bool smoke, double probability, string directory)
    {
        var label = probability < 1 ? scenario + (probability == 0.1 ? "-p010" : "-p001") : scenario;
        var stem = $"{provider}-{label}-{repetition:00}";
        if (this.checkpoint.TryGetValue(stem, out var retained))
        {
            ValidateTrial(retained, smoke, probability);
            output.WriteLine("Reusing validated {0}", stem);
            return retained;
        }

        await File.WriteAllTextAsync(Path.Combine(directory, "progress.txt"), "Starting " + stem + " " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        output.WriteLine("Starting {0}", stem);
        var result = await ProfilingLoadHarness.RunAsync(provider, scenario, repetition, Configure(provider), smoke, probability);
        result = result with { Scenario = label };
        await File.WriteAllTextAsync(Path.Combine(directory, stem + ".json"), JsonSerializer.Serialize(result, json));
        await using (var file = File.Create(Path.Combine(directory, stem + ".latencies.json.gz")))
        {
            await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            await JsonSerializer.SerializeAsync(gzip, result.LatencyTicks);
        }

        ValidateTrial(result, smoke, probability);
        output.WriteLine("{0}: achieved={1:F1}/s p95={2:F3}ms persisted={3:F1}/s dropped={4} queue={5}", stem,
            result.Metrics["achievedPerSecond"], result.Metrics["latencyP95Ms"], result.Metrics["persistedPerSecond"],
            result.Metrics["droppedWithinWindow"], result.Metrics["endQueueRecords"]);
        return result;
    }

    private static void ValidateTrial(ProfilingLoadResult result, bool smoke, double probability)
    {
        result.Configuration.GetProperty("smoke").GetBoolean().ShouldBe(smoke);
        result.Configuration.GetProperty("probability").GetDouble().ShouldBe(probability);
        result.Configuration.GetProperty("seed").GetInt32().ShouldBe(1729);
        result.Configuration.GetProperty("rate").GetInt32().ShouldBe(smoke ? 100 : 1000);
        result.Configuration.GetProperty("measurementSeconds").GetDouble().ShouldBe(smoke ? 1 : 60);
        result.Configuration.GetProperty("warmupSeconds").GetDouble().ShouldBe(smoke ? 0.25 : 10);
        result.Configuration.GetProperty("maxRecordBytes").GetInt32().ShouldBe(4096);
        result.Metrics["failedRequests"].ShouldBe(0);
        result.Metrics["schedulerShed"].ShouldBe(0);
        result.Metrics["captureFaults"].ShouldBe(0);
        if (probability == 1 && result.Scenario != "omitted" && result.Scenario != "disabled") { result.HealthPostDrain.AdmittedOperations.ShouldBeGreaterThan(0); }

        result.Metrics["exemplarPayloadBytes"].ShouldBeLessThanOrEqualTo(4096);
        if (result.Metrics["exemplarPayloadBytes"] > 0 && result.Scenario != "empty") { result.Metrics["exemplarSegmentInvocations"].ShouldBe(4); }

        if (result.Scenario == "saturated") { result.HealthPostDrain.DroppedOperations.ShouldBeGreaterThan(0); }

        if (result.Scenario == "nested-trace") { result.Metrics["traceEventsWithinWindow"].ShouldBeGreaterThan(0); }
        else { result.Metrics["traceEventsWithinWindow"].ShouldBe(0); }
    }

    private Action<DbContextOptionsBuilder> Configure(string provider)
    {
        var id = "ProfilingPerf_" + Guid.NewGuid().ToString("N");
        return provider switch
        {
            "sqlite" => options => options.UseSqlite("Data Source=" + Path.Combine(Path.GetTempPath(), id + ".db")),
            "sqlserver" => options => options.UseSqlServer(new SqlConnectionStringBuilder(fixture.SqlConnectionString) { InitialCatalog = id }.ConnectionString),
            "postgres" => options => options.UseNpgsql(new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = id }.ConnectionString),
            _ => null,
        };
    }

    private async Task<Dictionary<string, string>> EngineVersionsAsync()
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT sqlite_version()";
            versions["sqlite"] = (await command.ExecuteScalarAsync()).ToString();
        }

        await using (var connection = new SqlConnection(fixture.SqlConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(128))";
            versions["sqlserver"] = (await command.ExecuteScalarAsync()).ToString();
        }

        await using (var connection = new NpgsqlConnection(fixture.PostgresConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = "SHOW server_version";
            versions["postgres"] = (await command.ExecuteScalarAsync()).ToString();
        }

        return versions;
    }

    private static string ResolveEvidenceDirectory(bool smoke)
    {
        var supplied = Environment.GetEnvironmentVariable("BITDEVKIT_PROFILING_PERF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(supplied)) { return Path.GetFullPath(supplied); }

        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bITdevKit.slnx"))) { directory = directory.Parent; }

        if (directory is null) { throw new InvalidOperationException("Set BITDEVKIT_PROFILING_PERF_OUTPUT outside the repository."); }

        return Path.Combine(directory.FullName, "plan/evidence/profiling-runtime-and-operations-1/phase-12", smoke ? "performance-smoke" : "performance");
    }

    private static async Task SaveSummaryAsync(string directory, IReadOnlyList<ProfilingLoadResult> results)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, json));
        var text = new StringBuilder("# Synthetic profiling capacity evidence\n\nThree measured repetitions per canonical case; smoke trials are setup checks only. Endpoint latency and allocation include the collocated Kestrel/client/test process. Queue/persistence counters are node local. Retention and periodic dashboard reads remain enabled. Trace uses a formatting/counting sink without disk I/O. See environment.json and per-trial configuration for effective bounds.\n\n");
        text.AppendLine("| Provider | Case | Trials | p50 ms | p95 ms | Achieved/s | Allocated KiB/completion | Peak queue | Persisted/s | Dropped | Unknown | Retention removals | p95 delta % | Allocation delta % |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var group in results.GroupBy(value => (value.Provider, value.Scenario)))
        {
            var baseline = results.Where(value => value.Provider == group.Key.Provider && value.Scenario == "omitted").ToArray();
            double Median(string metric) => group.Select(value => value.Metrics[metric]).Order().ElementAt(group.Count() / 2);
            double Delta(string metric)
            {
                var original = baseline.Select(value => value.Metrics[metric]).Order().ElementAt(baseline.Length / 2);
                return original == 0 ? 0 : (Median(metric) / original - 1) * 100;
            }

            text.AppendLine(FormattableString.Invariant($"| {group.Key.Provider} | {group.Key.Scenario} | {group.Count()} | {Median("latencyP50Ms"):F3} | {Median("latencyP95Ms"):F3} | {Median("achievedPerSecond"):F1} | {Median("allocatedBytesPerCompletion") / 1024:F2} | {group.Max(value => value.Metrics["peakQueueRecords"]):F0} | {Median("persistedPerSecond"):F1} | {group.Sum(value => value.Metrics["droppedWithinWindow"]):F0} | {group.Sum(value => value.Metrics["unknownWithinWindow"]):F0} | {group.Sum(value => value.Metrics["retentionRemovedWithinWindow"]):F0} | {Delta("latencyP95Ms"):F1} | {Delta("allocatedBytesPerCompletion"):F1} |"));
        }

        text.AppendLine("\nRaw latency arrays contain Stopwatch ticks; zero entries represent unsuccessful or shed offers and remain in the raw artifacts. Aggregate percentiles use observed successful endpoint responses with nearest-rank selection. Persisted/s is measured before unmeasured draining. Warm-up history and default retention remain in force. Low probability trials test a concrete capture adjustment; these do not extrapolate dashboard populations. No nominal scheduler ceiling is described as observed throughput.");
        await File.WriteAllTextAsync(Path.Combine(directory, "summary.md"), text.ToString());
    }
}

/// <summary>Explicitly skips costly capacity evidence unless the caller enables it.</summary>
/// <example>Set BITDEVKIT_PROFILING_PERF=1 for canonical evidence; SMOKE=1 only validates setup.</example>
public sealed class ProfilingPerformanceFactAttribute : FactAttribute
{
    /// <summary>Declares the opt-in prerequisite during discovery.</summary>
    /// <example>Used by ProfilingPerformanceEvidenceTests.</example>
    public ProfilingPerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BITDEVKIT_PROFILING_PERF") != "1")
        {
            this.Skip = "Set BITDEVKIT_PROFILING_PERF=1 to run the 1000 operations/s, 60-second, three-repetition capacity harness.";
        }
    }
}
