// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Profiling;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Runs isolated open-loop HTTP workloads with the real recorder, optional pipeline behavior, writer and dashboard reads.</summary>
/// <example>Call RunAsync once per provider, scenario and repetition; each EF database belongs only to this fixture.</example>
public static class ProfilingLoadHarness
{
    private const int SEED = 1729;
    private const int MAX_PENDING = 4096;
    private static readonly IPipelineStepDefinition step = new PipelineStepDefinitionModel("compute", PipelineStepSourceKind.Type, null, null, null, null);

    /// <summary>Measures one fresh local Kestrel host; never creates or deletes application databases.</summary>
    /// <example>await ProfilingLoadHarness.RunAsync("memory", "nested", 1, null, false);</example>
    public static async Task<ProfilingLoadResult> RunAsync(string providerName, string scenario, int repetition,
        Action<DbContextOptionsBuilder> configureDatabase, bool smoke, double probability = 1)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var trace = scenario == "nested-trace";
        var logger = new CountingLoggerProvider(trace);
        builder.Logging.AddProvider(logger);
        if (trace)
        {
            builder.Logging.AddFilter("BridgingIT.DevKit.Common.OperationProfiler", LogLevel.Trace);
            builder.Logging.AddFilter("BridgingIT.DevKit.Common.ProfilingSegmentScope", LogLevel.Trace);
        }

        var omitted = scenario == "omitted";
        var disabled = scenario == "disabled";
        if (configureDatabase is not null) { builder.Services.AddDbContext<ProfilingProviderDbContext>(configureDatabase); }

        if (!omitted)
        {
            builder.Services.AddSingleton<IRequestProfilingRandomSource>(new SeededRandom());
            var setup = builder.Services.AddProfiling(options => options.Enabled(!disabled))
                .WithOperationProfiling(options => options.Configure(value =>
                {
                    value.MaxRecordBytes = 4096;
                    if (scenario == "saturated") { value.QueueCapacity = 8; value.BatchSize = 8; value.FlushInterval = TimeSpan.FromSeconds(10); }
                }))
                .WithRequestProfiling(options =>
                {
                    // The canonical four-segment, 4 KiB trial measures the general operation core.
                    // HTTP metadata has a separate correctness suite and a larger default payload bound.
                    options.Blacklist("/admin/**", "/work");
                    if (probability < 1) { options.WithSampling(sampling => sampling.Probability(probability)); }
                });
            if (configureDatabase is null) { setup.WithInMemoryProvider(); }
            else { setup.WithEntityFrameworkProvider<ProfilingProviderDbContext>(); }
        }

        // Installation and actual execution are preserved even when Profiling registration is omitted.
        builder.Services.AddSingleton<PipelineProfilingBehavior>();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin"));
        await using var app = builder.Build();
        var created = false;
        try
        {
            if (configureDatabase is not null && !omitted)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>().Database.EnsureCreatedAsync();
                created = true;
            }

            var profiler = app.Services.GetService<IOperationProfiler>();
            var behavior = app.Services.GetRequiredService<PipelineProfilingBehavior>();
            var healthSource = app.Services.GetService<IOperationProfilingHealthSource>();
            var sampling = app.Services.GetService<RequestProfilingRuntime>();
            app.UseRequestProfiling();
            app.UseRequestProfilingExceptionObserver();
            app.UseRouting();
            app.MapEndpoints();
            app.MapGet("/work", async (HttpContext context) =>
            {
                var index = int.Parse(context.Request.Query["index"].ToString(), CultureInfo.InvariantCulture);
                using var boundary = profiler.BeginSafeExecutionBoundary();
                var selected = sampling?.Enabled == true && sampling.Decide(null).Capture ? profiler : null;
                await selected.RunOperationAsync("benchmark", OperationProfilingKind.Service, async (operation, _) =>
                {
                    if (index % 2 == 0 && scenario.StartsWith("nested", StringComparison.Ordinal))
                    {
                        await Task.WhenAll(RunBranchAsync(operation, profiler, behavior, index, scenario != "empty"),
                            RunBranchAsync(operation, profiler, behavior, index, scenario != "empty"));
                    }
                    else
                    {
                        await RunBranchAsync(operation, profiler, behavior, index, scenario != "empty");
                        await RunBranchAsync(operation, profiler, behavior, index, scenario != "empty");
                    }
                });

                return Microsoft.AspNetCore.Http.Results.Text("ok");
            });
            await app.StartAsync();
            if (healthSource is not null && !disabled)
            {
                using var activation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (!healthSource.GetSnapshot().WriterActive) { await Task.Delay(10, activation.Token); }
            }

            using var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 256, UseProxy = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
            var rate = smoke ? 100 : 1000;
            var warmup = smoke ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(10);
            var duration = smoke ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(60);
            await OfferAsync(client, warmup, rate, null);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var retainedBefore = GC.GetTotalMemory(false);
            var before = healthSource?.GetSnapshot();
            var samples = new ConcurrentQueue<ProfilingLoadSample>();
            using var pollingCancellation = new CancellationTokenSource();
            var pollFailures = 0;
            var polls = 0;
            var polling = Task.Run(async () =>
            {
                try
                {
                    while (!pollingCancellation.IsCancellationRequested)
                    {
                        var state = healthSource?.GetSnapshot();
                        var watch = Stopwatch.StartNew();
                        using var response = await client.GetAsync("/admin/profiling/operations/api/groups?key=benchmark&groupBy=workload&limit=20", pollingCancellation.Token);
                        await response.Content.ReadAsByteArrayAsync(pollingCancellation.Token);
                        Interlocked.Increment(ref polls);
                        if (!response.IsSuccessStatusCode) { Interlocked.Increment(ref pollFailures); }

                        samples.Enqueue(new(UtcNow(), state?.QueueRecords ?? 0, state?.QueuePayloadBytes ?? 0,
                            state?.QueueOldestAge.TotalMilliseconds ?? 0, state?.PersistedOperations ?? 0,
                            state?.DroppedOperations ?? 0, state?.UnknownCommits ?? 0, watch.Elapsed.TotalMilliseconds,
                            (int)response.StatusCode));
                        await Task.Delay(TimeSpan.FromSeconds(1), pollingCancellation.Token);
                    }
                }
                catch (OperationCanceledException) when (pollingCancellation.IsCancellationRequested) { }
            });
            var logsBefore = logger.Events;
            var allocatedBefore = GC.GetTotalAllocatedBytes(true);
            var started = UtcNow();
            var offered = await OfferAsync(client, duration, rate, null);
            var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
            var after = healthSource?.GetSnapshot();
            pollingCancellation.Cancel();
            await polling;
            var retainedAfter = GC.GetTotalMemory(true);
            // Drain outside the measured window. Reads never force a flush; the hosted writer remains periodic.
            if (healthSource is not null)
            {
                using var draining = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { while (healthSource.GetSnapshot().QueueRecords > 0) { await Task.Delay(100, draining.Token); } }
                catch (OperationCanceledException) when (draining.IsCancellationRequested) { }
            }

            var postDrain = healthSource?.GetSnapshot();
            OperationProfilingRecord exemplar = null;
            var store = app.Services.GetService<IOperationProfilingStore>();
            if (store is not null && !disabled)
            {
                var selected = await store.QueryAsync(new() { Key = "benchmark", PageSize = 1 });
                if (selected.IsSuccess) { exemplar = selected.Value.Records.FirstOrDefault(); }
            }

            var latencies = offered.Latencies.Where(value => value > 0).Order().ToArray();
            var metrics = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["offeredRate"] = rate, ["offeredCount"] = offered.Offered, ["completedCount"] = offered.Completed,
                ["completedWithinWindow"] = offered.CompletedWithinWindow, ["failedRequests"] = offered.Failed,
                ["schedulerShed"] = offered.Shed, ["measurementSeconds"] = duration.TotalSeconds,
                ["achievedPerSecond"] = offered.CompletedWithinWindow / duration.TotalSeconds,
                ["latencyP50Ms"] = Percentile(latencies, 0.5), ["latencyP95Ms"] = Percentile(latencies, 0.95),
                ["allocatedBytes"] = allocated, ["allocatedBytesPerCompletion"] = allocated / (double)Math.Max(1, offered.Completed),
                ["retainedHeapBeforeBytes"] = retainedBefore, ["retainedHeapAfterBytes"] = retainedAfter,
                ["retainedHeapChangeBytes"] = retainedAfter - retainedBefore,
                ["peakQueueRecords"] = samples.Select(value => value.QueueRecords).DefaultIfEmpty().Max(),
                ["peakQueueBytes"] = samples.Select(value => value.QueueBytes).DefaultIfEmpty().Max(),
                ["peakQueueAgeMs"] = samples.Select(value => value.QueueAgeMs).DefaultIfEmpty().Max(),
                ["endQueueRecords"] = after?.QueueRecords ?? 0, ["postDrainQueueRecords"] = postDrain?.QueueRecords ?? 0,
                ["persistedWithinWindow"] = (after?.PersistedOperations ?? 0) - (before?.PersistedOperations ?? 0),
                ["persistedPerSecond"] = ((after?.PersistedOperations ?? 0) - (before?.PersistedOperations ?? 0)) / duration.TotalSeconds,
                ["selectedWithinWindow"] = (after?.SelectedRequests ?? 0) - (before?.SelectedRequests ?? 0),
                ["droppedWithinWindow"] = (after?.DroppedOperations ?? 0) - (before?.DroppedOperations ?? 0),
                ["unknownWithinWindow"] = (after?.UnknownCommits ?? 0) - (before?.UnknownCommits ?? 0),
                ["retentionRemovedWithinWindow"] = (after?.RetentionRemovals ?? 0) - (before?.RetentionRemovals ?? 0),
                ["captureFaults"] = (after?.CaptureFaults ?? 0) - (before?.CaptureFaults ?? 0),
                ["traceEventsWithinWindow"] = logger.Events - logsBefore, ["dashboardPolls"] = polls, ["dashboardPollFailures"] = pollFailures,
                ["exemplarPayloadBytes"] = exemplar?.EstimatedPayloadBytes ?? 0,
                ["exemplarSegmentInvocations"] = exemplar?.Segments.Sum(value => value.Statistics.Count) ?? 0,
            };
            return new()
            {
                Provider = providerName, Scenario = scenario, Repetition = repetition, StartedUtc = started,
                Metrics = metrics, Samples = samples.ToArray(), LatencyTicks = offered.Latencies,
                HealthBefore = before, HealthAfter = after, HealthPostDrain = postDrain,
                Configuration = JsonSerializer.SerializeToElement(new
                {
                    seed = SEED, rate, warmupSeconds = warmup.TotalSeconds, measurementSeconds = duration.TotalSeconds,
                    smoke, probability, maxPending = MAX_PENDING, maxRecordBytes = 4096, clockFrequency = Stopwatch.Frequency,
                    queueCapacity = scenario == "saturated" ? 8 : 8192, batchSize = scenario == "saturated" ? 8 : 512,
                    flushSeconds = scenario == "saturated" ? 10 : 1,
                    logging = trace ? "Trace with bounded formatting sink" : "Trace filtered",
                    transport = "loopback HTTP/1.1", workload = "generic operation owner, two Work/Compute branches, four invocations; even nested offers run in parallel; optional pipeline behavior executes under suppression in every branch",
                    captureScope = "general operation core in an independent execution boundary, not the larger HTTP metadata projection; original /work path excluded by adapter; prepared singleton HTTP sampler used at generic owner admission",
                    allocationScope = "entire collocated test/server/client process; not recorder-only allocation",
                }),
            };
        }
        finally
        {
            await app.StopAsync();
            if (created)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ProfilingProviderDbContext>().Database.EnsureDeletedAsync();
            }
        }
    }

    private static async Task RunBranchAsync(IProfilingOperationScope operation, IOperationProfiler profiler, PipelineProfilingBehavior behavior, int index, bool segments)
    {
        using var work = segments ? operation.BeginSegment("Work") : null;
        using var compute = work?.BeginSegment("Compute");
        // Keep the real optional behavior/delegate path in all cases without adding extra, larger adapter metadata.
        using (profiler?.Suppress()) { await RunBehaviorAsync(behavior, index); }

        compute?.Complete();
        work?.Complete();
    }

    private static async ValueTask<Result> RunBehaviorAsync(PipelineProfilingBehavior behavior, int index)
    {
        var context = new LoadContext();
        context.Pipeline.Name = "benchmark";
        context.Pipeline.ExecutionId = Guid.NewGuid();
        context.Pipeline.CorrelationId = "synthetic";
        return await behavior.ExecuteAsync(context, async () =>
        {
            var control = await behavior.ExecuteStepAsync(context, step, Result.Success(), async () =>
            {
                await WorkAsync(index);
                return PipelineControl.Continue(Result.Success());
            }, default);
            return control.Result;
        }, default);
    }

    private static async Task WorkAsync(int index)
    {
        var checksum = index ^ SEED;
        for (var iteration = 0; iteration < 32; iteration++) { checksum = unchecked(checksum * 1664525 + 1013904223); }

        await Task.Yield();
        GC.KeepAlive(checksum);
    }

    private static async Task<OfferedResult> OfferAsync(HttpClient client, TimeSpan duration, int rate, Action<int> completed)
    {
        var offered = (int)(duration.TotalSeconds * rate);
        var latencies = new long[offered];
        var pending = new ConcurrentDictionary<int, Task>();
        var count = 0;
        var failures = 0;
        var shed = 0;
        var within = 0;
        var started = Stopwatch.GetTimestamp();
        var end = started + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        for (var index = 0; index < offered; index++)
        {
            var due = started + index * Stopwatch.Frequency / rate;
            var remaining = (due - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
            if (remaining > 0) { await Task.Delay(TimeSpan.FromSeconds(remaining)); }

            foreach (var item in pending.Where(value => value.Value.IsCompleted)) { pending.TryRemove(item.Key, out _); }

            if (pending.Count >= MAX_PENDING) { shed++; continue; }

            var selectedIndex = index;
            pending[selectedIndex] = SendAsync(selectedIndex);
        }

        await Task.WhenAll(pending.Values);
        return new(offered, count, within, failures, shed, latencies);

        async Task SendAsync(int index)
        {
            var stamp = Stopwatch.GetTimestamp();
            try
            {
                using var response = await client.GetAsync("/work?index=" + index.ToString(CultureInfo.InvariantCulture), HttpCompletionOption.ResponseHeadersRead);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode || body != "ok") { Interlocked.Increment(ref failures); return; }

                latencies[index] = Stopwatch.GetTimestamp() - stamp;
                Interlocked.Increment(ref count);
                if (Stopwatch.GetTimestamp() <= end) { Interlocked.Increment(ref within); }

                completed?.Invoke(index);
            }
            catch (Exception) { Interlocked.Increment(ref failures); }
        }
    }

    private static double Percentile(long[] ordered, double fraction) => ordered.Length == 0 ? 0
        : ordered[Math.Clamp((int)Math.Ceiling(ordered.Length * fraction) - 1, 0, ordered.Length - 1)] * 1000d / Stopwatch.Frequency;
    private static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private sealed record OfferedResult(int Offered, int Completed, int CompletedWithinWindow, int Failed, int Shed, long[] Latencies);
    private sealed class LoadContext : PipelineContextBase;

    private sealed class SeededRandom : IRequestProfilingRandomSource
    {
        private readonly Random random = new(SEED);
        /// <inheritdoc />
        public double NextDouble() { lock (this.random) { return this.random.NextDouble(); } }
    }

    private sealed class CountingLoggerProvider(bool trace) : ILoggerProvider
    {
        private long events;
        /// <summary>Gets formatted lifecycle events without retaining their payloads.</summary>
        public long Events => Interlocked.Read(ref this.events);
        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new CountingLogger(this, trace &&
            (categoryName.EndsWith(".OperationProfiler", StringComparison.Ordinal) || categoryName.EndsWith(".ProfilingSegmentScope", StringComparison.Ordinal)));
        /// <inheritdoc />
        public void Dispose() { }
        private sealed class CountingLogger(CountingLoggerProvider owner, bool enabled) : ILogger
        {
            /// <inheritdoc />
            public IDisposable BeginScope<TState>(TState state) => null;
            /// <inheritdoc />
            public bool IsEnabled(LogLevel level) => enabled && level == LogLevel.Trace;
            /// <inheritdoc />
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!this.IsEnabled(level)) { return; }

                GC.KeepAlive(formatter(state, exception));
                Interlocked.Increment(ref owner.events);
            }
        }
    }
}

/// <summary>Stores a bounded periodic health and dashboard-cost observation.</summary>
/// <param name="TimestampUtc">UTC timestamp ending with Z.</param>
/// <param name="QueueRecords">Queued plus in-flight roots.</param>
/// <param name="QueueBytes">Charged queue bytes.</param>
/// <param name="QueueAgeMs">Oldest queued age in milliseconds.</param>
/// <param name="Persisted">Cumulative persisted roots.</param>
/// <param name="Dropped">Cumulative confirmed drops.</param>
/// <param name="Unknown">Cumulative unresolved commits.</param>
/// <param name="PollMs">Dashboard query/transfer duration.</param>
/// <param name="PollStatus">Actual dashboard response status.</param>
/// <example>Serialized as a per-second sample in each measured trial.</example>
public sealed record ProfilingLoadSample(string TimestampUtc, long QueueRecords, long QueueBytes, double QueueAgeMs,
    long Persisted, long Dropped, long Unknown, double PollMs, int PollStatus);

/// <summary>Contains reproducible measurements for one isolated workload trial.</summary>
/// <example>Serialize summary JSON and separately compress LatencyTicks.</example>
public sealed record ProfilingLoadResult
{
    /// <summary>Gets the backend label.</summary>
    /// <example>result.Provider</example>
    public string Provider { get; init; }
    /// <summary>Gets the capture/logging scenario.</summary>
    /// <example>result.Scenario</example>
    public string Scenario { get; init; }
    /// <summary>Gets the measured repetition number.</summary>
    /// <example>result.Repetition</example>
    public int Repetition { get; init; }
    /// <summary>Gets the UTC measurement start ending with Z.</summary>
    /// <example>result.StartedUtc</example>
    public string StartedUtc { get; init; }
    /// <summary>Gets named counters and derived units, without extrapolated request populations.</summary>
    /// <example>result.Metrics["latencyP95Ms"]</example>
    public IReadOnlyDictionary<string, double> Metrics { get; init; }
    /// <summary>Gets effective workload, bounds, transport and allocation scope.</summary>
    /// <example>result.Configuration</example>
    public JsonElement Configuration { get; init; }
    /// <summary>Gets bounded periodic observations.</summary>
    /// <example>result.Samples</example>
    public IReadOnlyList<ProfilingLoadSample> Samples { get; init; }
    /// <summary>Gets raw endpoint latency ticks, compressed into a separate test artifact.</summary>
    /// <example>result.LatencyTicks</example>
    [JsonIgnore]
    public IReadOnlyList<long> LatencyTicks { get; init; }
    /// <summary>Gets health immediately before measurement.</summary>
    /// <example>result.HealthBefore</example>
    public OperationProfilingHealth HealthBefore { get; init; }
    /// <summary>Gets health at the measured boundary before periodic draining.</summary>
    /// <example>result.HealthAfter</example>
    public OperationProfilingHealth HealthAfter { get; init; }
    /// <summary>Gets health after the bounded, unmeasured periodic drain.</summary>
    /// <example>result.HealthPostDrain</example>
    public OperationProfilingHealth HealthPostDrain { get; init; }
}
