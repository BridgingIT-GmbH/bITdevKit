// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// Measures the repeated profiling snapshot path and its principal runtime dependencies.
/// </summary>
/// <example>
/// <code>
/// dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj --filter *ProfilingRuntimeBenchmarks*
/// </code>
/// </example>
[MemoryDiagnoser]
public class ProfilingRuntimeBenchmarks
{
    private readonly SystemProfilingRuntimeSnapshotSource runtimeSource = new();
    private readonly SystemProfilingRuntimeContextSource contextSource = new();
    private ProfilingSnapshotProbe fixedSourceProbe;
    private ProfilingSnapshotProbe systemProbe;
    private ProfilingRuntimeContextFactory contextFactory;
    private ProfilingCaptureRequest captureRequest;
    private ProfilingSession session;
    private ProfilingNode node;

    /// <summary>Creates stable session and node inputs outside benchmark measurements.</summary>
    /// <example><code>benchmarks.Setup();</code></example>
    [GlobalSetup]
    public void Setup()
    {
        var startedUtc = DateTimeOffset.UtcNow;
        this.session = CreateSession(startedUtc);
        this.node = CreateNode(startedUtc);
        this.captureRequest = new ProfilingCaptureRequest(
            this.session,
            this.node,
            1,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            0,
            0
        );
        this.fixedSourceProbe = new ProfilingSnapshotProbe(
            TimeProvider.System,
            new FixedRuntimeSnapshotSource()
        );
        this.systemProbe = new ProfilingSnapshotProbe();
        this.contextFactory = new ProfilingRuntimeContextFactory(this.contextSource);
    }

    /// <summary>Measures the platform and runtime calls used for one raw sample.</summary>
    /// <returns>The captured raw runtime sample.</returns>
    /// <example><code>var sample = benchmarks.CaptureSystemRuntimeSample();</code></example>
    [Benchmark]
    public ProfilingRuntimeSample CaptureSystemRuntimeSample() => this.runtimeSource.Capture();

    /// <summary>Measures probe orchestration and model creation without platform-call cost.</summary>
    /// <returns>The completed snapshot result.</returns>
    /// <example><code>var result = benchmarks.CaptureFixedSourceSnapshot();</code></example>
    [Benchmark]
    public Result<ProfilingSnapshot> CaptureFixedSourceSnapshot() =>
        this
            .fixedSourceProbe.CaptureAsync(this.captureRequest, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    /// <summary>Measures a complete system-backed profiling snapshot capture.</summary>
    /// <returns>The completed snapshot result.</returns>
    /// <example><code>var result = benchmarks.CaptureSystemSnapshot();</code></example>
    [Benchmark]
    public Result<ProfilingSnapshot> CaptureSystemSnapshot() =>
        this
            .systemProbe.CaptureAsync(this.captureRequest, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    /// <summary>Measures the one-time runtime-context capture for a session node.</summary>
    /// <returns>The immutable runtime context.</returns>
    /// <example><code>var context = benchmarks.CaptureSystemRuntimeContext();</code></example>
    [Benchmark]
    public ProfilingRuntimeContext CaptureSystemRuntimeContext() =>
        this.contextFactory.Create(this.session, this.node);

    private static ProfilingSession CreateSession(DateTimeOffset startedUtc) =>
        new()
        {
            Identity = ProfilingSessionIdentity.Create(),
            State = ProfilingSessionState.Running,
            StartedUtc = startedUtc,
            EndsUtc = startedUtc.AddMinutes(1),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromMinutes(1),
        };

    private static ProfilingNode CreateNode(DateTimeOffset processStartedUtc) =>
        new()
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = new ProfilingNodeCorrelation("profiling-benchmark", processStartedUtc),
            HostName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
        };

    private sealed class FixedRuntimeSnapshotSource : IProfilingRuntimeSnapshotSource
    {
        private static readonly ProfilingGcObservation LatestGc = new(
            10,
            2,
            32 * 1024 * 1024,
            4 * 1024 * 1024,
            true,
            false,
            TimeSpan.FromMilliseconds(2)
        );

        public ProfilingRuntimeSample Capture() =>
            new()
            {
                ProcessCpuDuration = TimeSpan.FromSeconds(10),
                LogicalProcessorCount = Environment.ProcessorCount,
                WorkingSetBytes = 64 * 1024 * 1024,
                PrivateMemoryBytes = 72 * 1024 * 1024,
                ManagedMemoryBytes = 24 * 1024 * 1024,
                ManagedHeapSizeBytes = 32 * 1024 * 1024,
                FragmentedBytes = 2 * 1024 * 1024,
                MemoryLoadBytes = 4L * 1024 * 1024 * 1024,
                HighMemoryLoadThresholdBytes = 8L * 1024 * 1024 * 1024,
                TotalAllocatedBytes = 256 * 1024 * 1024,
                Gen0CollectionCount = 20,
                Gen1CollectionCount = 5,
                Gen2CollectionCount = 2,
                LatestGc = LatestGc,
                LatestGen2Gc = LatestGc,
                TotalGcPauseDuration = TimeSpan.FromMilliseconds(20),
                ThreadPoolThreadCount = 4,
                ThreadPoolCompletedWorkItemCount = 100,
                ThreadPoolPendingWorkItemCount = 0,
            };
    }
}

/// <summary>Measures the bounded state used to derive GC pause evidence.</summary>
/// <example>
/// <code>
/// dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj --filter *ProfilingGcObservationBenchmarks*
/// </code>
/// </example>
[MemoryDiagnoser]
public class ProfilingGcObservationBenchmarks
{
    private readonly ProfilingGcObservationState state = new();
    private readonly ProfilingGcObservation latest = new(
        10,
        2,
        32 * 1024 * 1024,
        4 * 1024 * 1024,
        true,
        false,
        TimeSpan.FromMilliseconds(2)
    );

    /// <summary>Measures one GC-state observation with direct Gen2 evidence.</summary>
    /// <returns>The derived GC observation result.</returns>
    /// <example><code>var result = benchmarks.ObserveGcEvidence();</code></example>
    [Benchmark]
    public ProfilingGcObservationResult ObserveGcEvidence() =>
        this.state.Observe(
            this.latest,
            this.latest,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromSeconds(1)
        );
}

/// <summary>Measures immutable snapshot appends through the process-local store.</summary>
/// <example>
/// <code>
/// dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj --filter *ProfilingInMemoryStoreBenchmarks*
/// </code>
/// </example>
[MemoryDiagnoser]
public class ProfilingInMemoryStoreBenchmarks
{
    private const int SnapshotBatchSize = 512;
    private ProfilingSessionCreateRequest sessionRequest;
    private ProfilingNode node;
    private ProfilingSnapshot[] snapshots;

    /// <summary>Creates a fixed store input and append batch outside benchmark measurements.</summary>
    /// <example><code>benchmarks.Setup();</code></example>
    [GlobalSetup]
    public void Setup()
    {
        var startedUtc = DateTimeOffset.UtcNow;
        var sessionIdentity = ProfilingSessionIdentity.Create();
        this.node = new ProfilingNode
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = new ProfilingNodeCorrelation("profiling-benchmark", startedUtc),
            HostName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
        };
        this.sessionRequest = new ProfilingSessionCreateRequest(
            sessionIdentity,
            "benchmark",
            startedUtc,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            []
        );

        this.snapshots = Enumerable
            .Range(1, SnapshotBatchSize)
            .Select(sequence => new ProfilingSnapshot
            {
                Identity = ProfilingSnapshotIdentity.Create(),
                SessionId = sessionIdentity.Id,
                NodeId = this.node.Identity.Id,
                SessionKey = sessionIdentity.Key,
                NodeKey = this.node.Identity.Key,
                TimestampUtc = startedUtc.AddMilliseconds(sequence),
                HostName = this.node.HostName,
                ProcessId = this.node.ProcessId,
                Sequence = sequence,
                ScheduledElapsed = TimeSpan.FromMilliseconds(sequence),
                CaptureStartedElapsed = TimeSpan.FromMilliseconds(sequence),
                CaptureDuration = TimeSpan.FromMilliseconds(1),
            })
            .ToArray();
    }

    /// <summary>Measures immutable snapshot append throughput without input-construction cost.</summary>
    /// <returns>The number of successfully appended snapshots.</returns>
    /// <example><code>var stored = await benchmarks.AppendSnapshotBatchAsync();</code></example>
    [Benchmark(OperationsPerInvoke = SnapshotBatchSize)]
    public async Task<int> AppendSnapshotBatchAsync()
    {
        var store = new InMemoryProfilingStore();
        await store.GetOrCreateActiveSessionAsync(this.sessionRequest).ConfigureAwait(false);
        await store.GetOrCreateNodeAsync(this.node.Correlation, this.node).ConfigureAwait(false);

        var stored = 0;
        foreach (var snapshot in this.snapshots)
        {
            var result = await store.AddSnapshotAsync(snapshot).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                stored++;
            }
        }

        return stored;
    }
}
