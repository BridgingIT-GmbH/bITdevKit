// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using System.Diagnostics;

/// <summary>Captures optional bounded benchmark diagnostics using existing cache duration windows and Common runtime profiling samples.</summary>
/// <remarks>Enable BDK_CACHE_BENCH_CAPTURE_METRICS=1 for a separate diagnostic cohort. These samples include pilot, warm-up and measured calls after setup, plus harness and sampler costs.</remarks>
/// <param name="resources">The actual cache accounts sampled without native provider queries.</param>
/// <example><code>measurements.RecordDuration("StreamAcquire", startedTimestamp);</code></example>
public sealed class CacheBenchmarkMeasurements(CacheRuntimeResources resources)
{
    private static readonly string[] Phases = ["StreamAcquire", "StreamFirstByte", "StreamComplete", "CallerWait", "Source", "EngineRead", "DirectNativeRead", "TypedHit"];
    private readonly object gate = new();
    private readonly SystemRuntimeProfilingRuntimeSnapshotSource runtime = new();
    private readonly Dictionary<string, CacheObservationSlot> durations = new(StringComparer.Ordinal);
    private readonly bool enabled = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_CAPTURE_METRICS") == "1";
    private RuntimeProfilingSample before;
    private Timer sampler;
    private bool started;
    private int sampling;
    private long samples;
    private long sampleFailures;
    private long allocatedBefore;
    private long residentPeak;
    private long transientPeak;
    private long hotPeak;
    private long? workingSetPeak;
    private long? privateMemoryPeak;
    private long? managedMemoryPeak;
    private long? managedHeapPeak;
    private DateTimeOffset startedAt;

    /// <summary>Gets whether the separately labelled diagnostic cohort was explicitly enabled.</summary>
    public bool Enabled => this.enabled;

    /// <summary>Resets observations after seeding and starts a 250-ms process/resource sampler without forcing garbage collection.</summary>
    /// <example><code>environment.Measurements.Start();</code></example>
    public void Start()
    {
        if (!this.enabled) { return; }

        lock (this.gate)
        {
            this.sampler?.Dispose();
            this.durations.Clear();
            foreach (var phase in Phases)
            {
                this.durations.Add(phase, new(phase, "Benchmark", "Diagnostic", new() { LatencySampleCapacity = 4096, LatencySampleWindow = TimeSpan.FromHours(1) }, TimeProvider.System, new()));
            }

            this.samples = this.sampleFailures = this.residentPeak = this.transientPeak = this.hotPeak = 0;
            this.workingSetPeak = this.privateMemoryPeak = this.managedMemoryPeak = this.managedHeapPeak = null;
            this.startedAt = DateTimeOffset.UtcNow;
            this.before = this.runtime.Capture();
            this.allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            this.started = true;
            this.RecordSample(this.before);
            this.sampler = new(static state => ((CacheBenchmarkMeasurements)state).Sample(), this, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        }
    }

    /// <summary>Records one real elapsed duration in a fixed bounded phase window, including unsuccessful caller/source outcomes.</summary>
    /// <param name="phase">One declared stream, caller, source or native-baseline phase.</param>
    /// <param name="startedTimestamp">The Stopwatch timestamp captured before the actual operation began.</param>
    /// <example><code>measurements.RecordDuration("Source", started);</code></example>
    public void RecordDuration(string phase, long startedTimestamp)
    {
        if (!this.enabled) { return; }

        lock (this.gate)
        {
            if (!this.started) { return; }

            this.durations[phase].RecordDuration(CacheLatencyKind.Operation, Stopwatch.GetElapsedTime(startedTimestamp));
            this.SampleResourceCharges();
        }
    }

    /// <summary>Observes each actual caller completion separately, preserving its original result or cancellation.</summary>
    /// <typeparam name="T">The original result type.</typeparam>
    /// <param name="startedTimestamp">The timestamp captured before submitting this caller.</param>
    /// <param name="operation">The actual caller operation, never a replacement or synthetic task.</param>
    /// <returns>The same result after its elapsed wait is recorded when diagnostics are enabled.</returns>
    /// <example><code>var task = measurements.ObserveCallerAsync(Stopwatch.GetTimestamp(), collection.GetAsync("key"));</code></example>
    public Task<T> ObserveCallerAsync<T>(long startedTimestamp, ValueTask<T> operation) => this.enabled ? this.ObserveAsync(startedTimestamp, operation) : operation.AsTask();

    /// <summary>Samples currently held cache resources at an actual streaming boundary.</summary>
    /// <example><code>measurements.ObserveResourceUsage();</code></example>
    public void ObserveResourceUsage()
    {
        if (!this.enabled) { return; }

        lock (this.gate) { if (this.started) { this.SampleResourceCharges(); } }
    }

    /// <summary>Stops sampling and returns actual bounded durations, all-thread process allocation/GC deltas, and explicitly sampled peaks.</summary>
    /// <returns>A serializable diagnostic observation; null measurements remain unavailable.</returns>
    /// <example><code>Console.WriteLine(JsonSerializer.Serialize(measurements.Finish()));</code></example>
    public object Finish()
    {
        if (!this.enabled) { return new { Enabled = false }; }

        lock (this.gate)
        {
            this.sampler?.Dispose();
            if (!this.started) { return new { Enabled = true, Started = false }; }

            var allocated = GC.GetTotalAllocatedBytes(precise: true) - this.allocatedBefore;
            var after = this.runtime.Capture();
            this.RecordSample(after);
            this.started = false;
            return new
            {
                Enabled = true,
                Scope = "SameProcessAllThreadsAfterSetupIncludingPilotWarmupAndMeasurementHarnessAndSampler",
                StartedAt = this.startedAt,
                EndedAt = DateTimeOffset.UtcNow,
                ProcessAllocatedBytes = allocated,
                Gen0Collections = after.Gen0CollectionCount - this.before.Gen0CollectionCount,
                Gen1Collections = after.Gen1CollectionCount - this.before.Gen1CollectionCount,
                Gen2Collections = after.Gen2CollectionCount - this.before.Gen2CollectionCount,
                GcPauseDuration = after.TotalGcPauseDuration - this.before.TotalGcPauseDuration,
                SampleIntervalMilliseconds = 250,
                Samples = this.samples,
                SampleFailures = Interlocked.Read(ref this.sampleFailures),
                SampledPeakWorkingSetBytes = this.workingSetPeak,
                SampledPeakPrivateMemoryBytes = this.privateMemoryPeak,
                SampledPeakManagedMemoryBytes = this.managedMemoryPeak,
                SampledPeakLastGcHeapBytes = this.managedHeapPeak,
                ObservedPeakResidentCharges = this.residentPeak,
                ObservedPeakTransientCharges = this.transientPeak,
                ObservedPeakHotCharges = this.hotPeak,
                PeakAccuracy = "ObservedSamplesMayMissShorterPeaksNotHardMemoryCeilings",
                DurationsIncludeWarmupAndMeasurement = true,
                Durations = this.durations.ToDictionary(pair => pair.Key, pair => pair.Value.Snapshot().Latencies[CacheLatencyKind.Operation])
            };
        }
    }

    private void Sample()
    {
        if (Interlocked.Exchange(ref this.sampling, 1) != 0) { return; }

        try
        {
            var sample = this.runtime.Capture();
            lock (this.gate) { if (this.started) { this.RecordSample(sample); } }
        }
        catch (Exception) { Interlocked.Increment(ref this.sampleFailures); }
        finally { Volatile.Write(ref this.sampling, 0); }
    }

    private async Task<T> ObserveAsync<T>(long startedTimestamp, ValueTask<T> operation)
    {
        try { return await operation.ConfigureAwait(false); }
        finally { this.RecordDuration("CallerWait", startedTimestamp); }
    }

    private void RecordSample(RuntimeProfilingSample sample)
    {
        this.samples++;
        this.workingSetPeak = Max(this.workingSetPeak, sample.WorkingSetBytes);
        this.privateMemoryPeak = Max(this.privateMemoryPeak, sample.PrivateMemoryBytes);
        this.managedMemoryPeak = Max(this.managedMemoryPeak, sample.ManagedMemoryBytes);
        this.managedHeapPeak = Max(this.managedHeapPeak, sample.ManagedHeapSizeBytes);
        this.SampleResourceCharges();
    }

    private void SampleResourceCharges()
    {
        this.residentPeak = Math.Max(this.residentPeak, resources.Resident.Used);
        this.transientPeak = Math.Max(this.transientPeak, resources.Transient.Used);
        this.hotPeak = Math.Max(this.hotPeak, resources.HotResident.Used);
    }

    private static long? Max(long? left, long? right) => left.HasValue && right.HasValue ? Math.Max(left.Value, right.Value) : left ?? right;
}