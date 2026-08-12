// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime;
using System.Runtime.InteropServices;

/// <summary>
/// Captures one provider-neutral snapshot from the current process and runtime.
/// </summary>
/// <remarks>
/// The probe owns only the bounded previous-sample state required for node-local rates. It does
/// not schedule captures, mutate sessions, persist data, or trigger garbage collection.
/// </remarks>
/// <example><code>var result = await probe.CaptureAsync(request, cancellationToken);</code></example>
public sealed class ProfilingSnapshotProbe : IProfilingSnapshotProbe
{
    private readonly object sync = new();
    private readonly TimeProvider timeProvider;
    private readonly IProfilingRuntimeSnapshotSource source;
    private readonly ProfilingGcObservationState gcState = new();
    private ProfilingRateSample previous;

    /// <summary>Creates a probe backed by the system clock and runtime APIs.</summary>
    /// <example><code>var probe = new ProfilingSnapshotProbe();</code></example>
    public ProfilingSnapshotProbe()
        : this(TimeProvider.System, new SystemProfilingRuntimeSnapshotSource()) { }

    /// <summary>Creates a probe backed by the supplied monotonic clock and system runtime APIs.</summary>
    /// <param name="timeProvider">The clock used for UTC and monotonic capture timing.</param>
    /// <example><code>var probe = new ProfilingSnapshotProbe(timeProvider);</code></example>
    public ProfilingSnapshotProbe(TimeProvider timeProvider)
        : this(timeProvider, new SystemProfilingRuntimeSnapshotSource()) { }

    /// <summary>
    /// Creates a probe backed by a caller-supplied monotonic clock and runtime snapshot source.
    /// </summary>
    /// <param name="timeProvider">The clock used for UTC and monotonic capture timing.</param>
    /// <param name="source">The source that captures raw runtime values.</param>
    /// <example><code>var probe = new ProfilingSnapshotProbe(timeProvider, source);</code></example>
    public ProfilingSnapshotProbe(TimeProvider timeProvider, IProfilingRuntimeSnapshotSource source)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <inheritdoc />
    public Task<Result<ProfilingSnapshot>> CaptureAsync(
        ProfilingCaptureRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = Validate(request);
        if (validation is not null)
        {
            return Task.FromResult(Result<ProfilingSnapshot>.Failure().WithError(validation));
        }

        var timestampUtc = this.timeProvider.GetUtcNow().ToUniversalTime();
        var captureTimestamp = this.timeProvider.GetTimestamp();
        var sample = this.CaptureRuntimeValues();
        var captureCompletedTimestamp = this.timeProvider.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();

        ProfilingDerivedValues derived;
        lock (this.sync)
        {
            var key = new ProfilingRateKey(request.Session.Identity.Id, request.Node.Identity.Id);
            var elapsed =
                this.previous is { Key: { } previousKey }
                && previousKey == key
                && captureTimestamp > this.previous.Timestamp
                    ? (TimeSpan?)
                        this.timeProvider.GetElapsedTime(this.previous.Timestamp, captureTimestamp)
                    : null;

            if (this.previous?.Key != key)
            {
                this.gcState.Reset();
            }

            var gc = this.gcState.Observe(
                sample.LatestGc,
                sample.LatestGen2Gc,
                sample.TotalGcPauseDuration,
                elapsed
            );
            derived = new ProfilingDerivedValues(
                CalculateCpuUsage(
                    this.previous?.ProcessCpuDuration,
                    sample.ProcessCpuDuration,
                    sample.LogicalProcessorCount,
                    elapsed
                ),
                CalculateRate(
                    this.previous?.TotalAllocatedBytes,
                    sample.TotalAllocatedBytes,
                    elapsed
                ),
                CalculatePercent(sample.FragmentedBytes, sample.ManagedHeapSizeBytes),
                CalculatePercent(
                    sample.LargeObjectHeapFragmentedBytes,
                    sample.LargeObjectHeapBytes
                ),
                CalculatePercent(sample.MemoryLoadBytes, sample.HighMemoryLoadThresholdBytes),
                gc
            );
            this.previous = new ProfilingRateSample(
                key,
                captureTimestamp,
                sample.ProcessCpuDuration,
                sample.TotalAllocatedBytes
            );
        }

        var snapshot = CreateSnapshot(
            request,
            timestampUtc,
            this.timeProvider.GetElapsedTime(captureTimestamp, captureCompletedTimestamp),
            sample,
            derived
        );
        return Task.FromResult(Result<ProfilingSnapshot>.Success(snapshot));
    }

    private static IResultError Validate(ProfilingCaptureRequest request)
    {
        if (
            request is null
            || request.Session is null
            || request.Node is null
            || request.Session.Identity.Id == Guid.Empty
            || request.Node.Identity.Id == Guid.Empty
            || request.Sequence <= 0
            || request.ScheduledElapsed < TimeSpan.Zero
            || request.CaptureStartedElapsed < TimeSpan.Zero
            || request.SkippedCaptureCount < 0
            || request.FailedCaptureCount < 0
        )
        {
            return new ProfilingValidationError(
                "A valid session, node, sequence, timing, and capture totals are required."
            );
        }

        return null;
    }

    private ProfilingRuntimeSample CaptureRuntimeValues()
    {
        try
        {
            return this.source.Capture() ?? ProfilingRuntimeSample.Empty;
        }
        catch
        {
            return ProfilingRuntimeSample.Empty;
        }
    }

    private static ProfilingSnapshot CreateSnapshot(
        ProfilingCaptureRequest request,
        DateTimeOffset timestampUtc,
        TimeSpan captureDuration,
        ProfilingRuntimeSample sample,
        ProfilingDerivedValues derived
    ) =>
        new()
        {
            Identity = ProfilingSnapshotIdentity.Create(),
            SessionId = request.Session.Identity.Id,
            NodeId = request.Node.Identity.Id,
            SessionKey = request.Session.Identity.Key,
            NodeKey = request.Node.Identity.Key,
            TimestampUtc = timestampUtc,
            HostName = request.Node.HostName,
            ProcessId = request.Node.ProcessId,
            Sequence = request.Sequence,
            ScheduledElapsed = request.ScheduledElapsed,
            CaptureStartedElapsed = request.CaptureStartedElapsed,
            CaptureDuration = captureDuration,
            SkippedCaptureCount = request.SkippedCaptureCount,
            FailedCaptureCount = request.FailedCaptureCount,
            CpuUsagePercent = derived.CpuUsagePercent,
            ProcessCpuDuration = sample.ProcessCpuDuration,
            LogicalProcessorCount = sample.LogicalProcessorCount,
            WorkingSetBytes = sample.WorkingSetBytes,
            PrivateMemoryBytes = sample.PrivateMemoryBytes,
            ManagedMemoryBytes = sample.ManagedMemoryBytes,
            TotalPhysicalMemoryBytes = sample.TotalPhysicalMemoryBytes,
            AvailablePhysicalMemoryBytes = sample.AvailablePhysicalMemoryBytes,
            UsedPhysicalMemoryBytes = Subtract(
                sample.TotalPhysicalMemoryBytes,
                sample.AvailablePhysicalMemoryBytes
            ),
            ManagedHeapSizeBytes = sample.ManagedHeapSizeBytes,
            FragmentedBytes = sample.FragmentedBytes,
            HeapFragmentationPercent = derived.HeapFragmentationPercent,
            MemoryLoadBytes = sample.MemoryLoadBytes,
            TotalAvailableMemoryBytes = sample.TotalAvailableMemoryBytes,
            HighMemoryLoadThresholdBytes = sample.HighMemoryLoadThresholdBytes,
            TotalCommittedBytes = sample.TotalCommittedBytes,
            TotalAllocatedBytes = sample.TotalAllocatedBytes,
            AllocationRateBytesPerSecond = derived.AllocationRateBytesPerSecond,
            MemoryPressurePercent = derived.MemoryPressurePercent,
            Gen0CollectionCount = sample.Gen0CollectionCount,
            Gen1CollectionCount = sample.Gen1CollectionCount,
            Gen2CollectionCount = sample.Gen2CollectionCount,
            LatestGcIndex = derived.Gc.Latest?.Index,
            LatestGcGeneration = derived.Gc.Latest?.Generation,
            LatestGcManagedHeapBytes = derived.Gc.Latest?.ManagedHeapBytes,
            LatestGcLargeObjectHeapBytes = derived.Gc.Latest?.LargeObjectHeapBytes,
            LatestGcCompacting = derived.Gc.Latest?.Compacting,
            LatestGcConcurrent = derived.Gc.Latest?.Concurrent,
            LatestGen2GcIndex = derived.Gc.LatestGen2?.Index,
            LatestGen2ManagedHeapBytes = derived.Gc.LatestGen2?.ManagedHeapBytes,
            LatestGen2LargeObjectHeapBytes = derived.Gc.LatestGen2?.LargeObjectHeapBytes,
            LatestGen2GcCompacting = derived.Gc.LatestGen2?.Compacting,
            LatestGen2GcConcurrent = derived.Gc.LatestGen2?.Concurrent,
            CumulativeGcPauseDuration = derived.Gc.CumulativePauseDuration,
            GcPausePercent = derived.Gc.PausePercent,
            PinnedObjectCount = sample.PinnedObjectCount,
            FinalizationPendingCount = sample.FinalizationPendingCount,
            LargeObjectHeapBytes = sample.LargeObjectHeapBytes,
            LargeObjectHeapFragmentedBytes = sample.LargeObjectHeapFragmentedBytes,
            LargeObjectHeapFragmentationPercent = derived.LargeObjectHeapFragmentationPercent,
            ServerGarbageCollection = sample.ServerGarbageCollection,
            GarbageCollectionLatencyMode = sample.GarbageCollectionLatencyMode,
            ProcessHandleCount = sample.ProcessHandleCount,
            ProcessThreadCount = sample.ProcessThreadCount,
            ThreadPoolThreadCount = sample.ThreadPoolThreadCount,
            ThreadPoolCompletedWorkItemCount = sample.ThreadPoolCompletedWorkItemCount,
            ThreadPoolPendingWorkItemCount = sample.ThreadPoolPendingWorkItemCount,
            ThreadPoolAvailableWorkerThreadCount = sample.ThreadPoolAvailableWorkerThreadCount,
            ThreadPoolAvailableCompletionPortThreadCount =
                sample.ThreadPoolAvailableCompletionPortThreadCount,
            ActiveTcpConnectionCount = sample.ActiveTcpConnectionCount,
            TcpListenerCount = sample.TcpListenerCount,
            UdpListenerCount = sample.UdpListenerCount,
            TotalUsedSocketCount = sample.TotalUsedSocketCount,
        };

    private static double? CalculateCpuUsage(
        TimeSpan? previous,
        TimeSpan? current,
        int? processorCount,
        TimeSpan? elapsed
    )
    {
        if (
            previous is null
            || current is null
            || current < previous
            || processorCount is null or <= 0
            || elapsed is null
            || elapsed <= TimeSpan.Zero
        )
        {
            return null;
        }

        return Math.Clamp(
            (current.Value - previous.Value).TotalSeconds
                / (elapsed.Value.TotalSeconds * processorCount.Value)
                * 100d,
            0d,
            100d
        );
    }

    private static double? CalculateRate(long? previous, long? current, TimeSpan? elapsed)
    {
        if (
            previous is null
            || current is null
            || current < previous
            || elapsed is null
            || elapsed <= TimeSpan.Zero
        )
        {
            return null;
        }

        return (current.Value - previous.Value) / elapsed.Value.TotalSeconds;
    }

    private static double? CalculatePercent(long? numerator, long? denominator) =>
        numerator is >= 0 && denominator is > 0
            ? Math.Clamp(numerator.Value / (double)denominator.Value * 100d, 0d, 100d)
            : null;

    private static long? Subtract(long? total, long? available) =>
        total is >= 0 && available is >= 0 && total >= available ? total - available : null;

    private sealed record ProfilingRateKey(Guid SessionId, Guid NodeId);

    private sealed record ProfilingRateSample(
        ProfilingRateKey Key,
        long Timestamp,
        TimeSpan? ProcessCpuDuration,
        long? TotalAllocatedBytes
    );

    private sealed record ProfilingDerivedValues(
        double? CpuUsagePercent,
        double? AllocationRateBytesPerSecond,
        double? HeapFragmentationPercent,
        double? LargeObjectHeapFragmentationPercent,
        double? MemoryPressurePercent,
        ProfilingGcObservationResult Gc
    );
}

/// <summary>Supplies one raw process and runtime sample to the profiling snapshot probe.</summary>
/// <example><code>var sample = source.Capture();</code></example>
public interface IProfilingRuntimeSnapshotSource
{
    /// <summary>Captures the currently available raw runtime values.</summary>
    /// <returns>A raw runtime sample.</returns>
    /// <example><code>var sample = source.Capture();</code></example>
    ProfilingRuntimeSample Capture();
}

/// <summary>Contains the raw process and runtime values captured at one instant.</summary>
/// <example><code>var sample = new ProfilingRuntimeSample { WorkingSetBytes = bytes };</code></example>
public sealed record ProfilingRuntimeSample
{
    /// <summary>Gets an empty sample for runtimes where metrics are unavailable.</summary>
    public static ProfilingRuntimeSample Empty { get; } = new();

    /// <summary>Gets the cumulative processor time used by the process.</summary>
    public TimeSpan? ProcessCpuDuration { get; init; }

    /// <summary>Gets the logical processor count available to the process.</summary>
    public int? LogicalProcessorCount { get; init; }

    /// <summary>Gets the process working-set size in bytes.</summary>
    public long? WorkingSetBytes { get; init; }

    /// <summary>Gets the process private-memory size in bytes.</summary>
    public long? PrivateMemoryBytes { get; init; }

    /// <summary>Gets the managed memory size in bytes.</summary>
    public long? ManagedMemoryBytes { get; init; }

    /// <summary>Gets total physical memory in bytes.</summary>
    public long? TotalPhysicalMemoryBytes { get; init; }

    /// <summary>Gets available physical memory in bytes.</summary>
    public long? AvailablePhysicalMemoryBytes { get; init; }

    /// <summary>Gets the managed heap size in bytes.</summary>
    public long? ManagedHeapSizeBytes { get; init; }

    /// <summary>Gets fragmented managed memory in bytes.</summary>
    public long? FragmentedBytes { get; init; }

    /// <summary>Gets the runtime memory load in bytes.</summary>
    public long? MemoryLoadBytes { get; init; }

    /// <summary>Gets total memory available to the runtime in bytes.</summary>
    public long? TotalAvailableMemoryBytes { get; init; }

    /// <summary>Gets the runtime high-memory-load threshold in bytes.</summary>
    public long? HighMemoryLoadThresholdBytes { get; init; }

    /// <summary>Gets total memory committed by the runtime in bytes.</summary>
    public long? TotalCommittedBytes { get; init; }

    /// <summary>Gets cumulative allocated managed bytes.</summary>
    public long? TotalAllocatedBytes { get; init; }

    /// <summary>Gets the cumulative generation 0 collection count.</summary>
    public long? Gen0CollectionCount { get; init; }

    /// <summary>Gets the cumulative generation 1 collection count.</summary>
    public long? Gen1CollectionCount { get; init; }

    /// <summary>Gets the cumulative generation 2 collection count.</summary>
    public long? Gen2CollectionCount { get; init; }

    /// <summary>Gets direct evidence for the latest collection.</summary>
    public ProfilingGcObservation LatestGc { get; init; }

    /// <summary>Gets direct evidence for the latest generation 2 collection.</summary>
    public ProfilingGcObservation LatestGen2Gc { get; init; }

    /// <summary>Gets the runtime's cumulative GC pause duration.</summary>
    public TimeSpan? TotalGcPauseDuration { get; init; }

    /// <summary>Gets the pinned object count after the latest collection.</summary>
    public long? PinnedObjectCount { get; init; }

    /// <summary>Gets the finalization-pending object count after the latest collection.</summary>
    public long? FinalizationPendingCount { get; init; }

    /// <summary>Gets the large object heap size in bytes.</summary>
    public long? LargeObjectHeapBytes { get; init; }

    /// <summary>Gets fragmented large object heap memory in bytes.</summary>
    public long? LargeObjectHeapFragmentedBytes { get; init; }

    /// <summary>Gets whether server garbage collection is enabled.</summary>
    public bool? ServerGarbageCollection { get; init; }

    /// <summary>Gets the garbage collection latency mode.</summary>
    public string GarbageCollectionLatencyMode { get; init; }

    /// <summary>Gets the process handle count.</summary>
    public int? ProcessHandleCount { get; init; }

    /// <summary>Gets the process thread count.</summary>
    public int? ProcessThreadCount { get; init; }

    /// <summary>Gets the thread-pool thread count.</summary>
    public int? ThreadPoolThreadCount { get; init; }

    /// <summary>Gets the cumulative completed thread-pool work-item count.</summary>
    public long? ThreadPoolCompletedWorkItemCount { get; init; }

    /// <summary>Gets the pending thread-pool work-item count.</summary>
    public long? ThreadPoolPendingWorkItemCount { get; init; }

    /// <summary>Gets the available thread-pool worker-thread count.</summary>
    public int? ThreadPoolAvailableWorkerThreadCount { get; init; }

    /// <summary>Gets the available thread-pool completion-port-thread count.</summary>
    public int? ThreadPoolAvailableCompletionPortThreadCount { get; init; }

    /// <summary>Gets the active TCP connection count.</summary>
    public int? ActiveTcpConnectionCount { get; init; }

    /// <summary>Gets the TCP listener count.</summary>
    public int? TcpListenerCount { get; init; }

    /// <summary>Gets the UDP listener count.</summary>
    public int? UdpListenerCount { get; init; }

    /// <summary>Gets the combined used-socket count.</summary>
    public int? TotalUsedSocketCount { get; init; }
}

/// <summary>Captures profiling snapshot values from the current process and runtime.</summary>
/// <example><code>var sample = new SystemProfilingRuntimeSnapshotSource().Capture();</code></example>
public sealed class SystemProfilingRuntimeSnapshotSource : IProfilingRuntimeSnapshotSource
{
    /// <inheritdoc />
    public ProfilingRuntimeSample Capture()
    {
        var process = CaptureProcess();
        var garbageCollection = CaptureGarbageCollection();
        var physicalMemory = CapturePhysicalMemory();
        var threadPool = CaptureThreadPool();
        var sockets = CaptureSockets();

        return new ProfilingRuntimeSample
        {
            ProcessCpuDuration = process.ProcessCpuDuration,
            LogicalProcessorCount = TryGet(() => (int?)Environment.ProcessorCount),
            WorkingSetBytes = process.WorkingSetBytes,
            PrivateMemoryBytes = process.PrivateMemoryBytes,
            ManagedMemoryBytes = TryGet(() => (long?)GC.GetTotalMemory(false)),
            TotalPhysicalMemoryBytes = physicalMemory.TotalBytes,
            AvailablePhysicalMemoryBytes = physicalMemory.AvailableBytes,
            ManagedHeapSizeBytes = garbageCollection.ManagedHeapSizeBytes,
            FragmentedBytes = garbageCollection.FragmentedBytes,
            MemoryLoadBytes = garbageCollection.MemoryLoadBytes,
            TotalAvailableMemoryBytes = garbageCollection.TotalAvailableMemoryBytes,
            HighMemoryLoadThresholdBytes = garbageCollection.HighMemoryLoadThresholdBytes,
            TotalCommittedBytes = garbageCollection.TotalCommittedBytes,
            TotalAllocatedBytes = TryGet(() => (long?)GC.GetTotalAllocatedBytes(false)),
            Gen0CollectionCount = TryGet(() => (long?)GC.CollectionCount(0)),
            Gen1CollectionCount = TryGet(() => (long?)GC.CollectionCount(1)),
            Gen2CollectionCount = TryGet(() => (long?)GC.CollectionCount(2)),
            LatestGc = garbageCollection.Latest,
            LatestGen2Gc = garbageCollection.LatestGen2,
            TotalGcPauseDuration = TryGet(() => (TimeSpan?)GC.GetTotalPauseDuration()),
            PinnedObjectCount = garbageCollection.PinnedObjectCount,
            FinalizationPendingCount = garbageCollection.FinalizationPendingCount,
            LargeObjectHeapBytes = garbageCollection.LargeObjectHeapBytes,
            LargeObjectHeapFragmentedBytes = garbageCollection.LargeObjectHeapFragmentedBytes,
            ServerGarbageCollection = TryGet(() => (bool?)GCSettings.IsServerGC),
            GarbageCollectionLatencyMode = TryGet(() => GCSettings.LatencyMode.ToString()),
            ProcessHandleCount = process.HandleCount,
            ProcessThreadCount = process.ThreadCount,
            ThreadPoolThreadCount = threadPool.ThreadCount,
            ThreadPoolCompletedWorkItemCount = threadPool.CompletedWorkItemCount,
            ThreadPoolPendingWorkItemCount = threadPool.PendingWorkItemCount,
            ThreadPoolAvailableWorkerThreadCount = threadPool.AvailableWorkerThreads,
            ThreadPoolAvailableCompletionPortThreadCount =
                threadPool.AvailableCompletionPortThreads,
            ActiveTcpConnectionCount = sockets.ActiveTcpConnectionCount,
            TcpListenerCount = sockets.TcpListenerCount,
            UdpListenerCount = sockets.UdpListenerCount,
            TotalUsedSocketCount = sockets.TotalUsedSocketCount,
        };
    }

    private static ProcessValues CaptureProcess()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            return new ProcessValues(
                TryGet(() => (TimeSpan?)process.TotalProcessorTime),
                TryGet(() => (long?)process.WorkingSet64),
                TryGet(() => (long?)process.PrivateMemorySize64),
                TryGet(() => (int?)process.HandleCount),
                TryGet(() => (int?)process.Threads.Count)
            );
        }
        catch
        {
            return new ProcessValues(null, null, null, null, null);
        }
    }

    private static GarbageCollectionValues CaptureGarbageCollection()
    {
        try
        {
            var latestInfo = GC.GetGCMemoryInfo();
            var latest = ToObservation(latestInfo);
            var latestGen2 = SelectLatestGen2(
                TryGet(() => (GCMemoryInfo?)GC.GetGCMemoryInfo(GCKind.FullBlocking)),
                TryGet(() => (GCMemoryInfo?)GC.GetGCMemoryInfo(GCKind.Background)),
                latestInfo.Generation == 2 ? latestInfo : null
            );
            var generationInfo = latestInfo.GenerationInfo;
            var lohBytes =
                generationInfo.Length > 3 ? (long?)generationInfo[3].SizeAfterBytes : null;
            var lohFragmentedBytes =
                generationInfo.Length > 3 ? (long?)generationInfo[3].FragmentationAfterBytes : null;

            return new GarbageCollectionValues(
                latestInfo.HeapSizeBytes,
                latestInfo.FragmentedBytes,
                latestInfo.MemoryLoadBytes,
                latestInfo.TotalAvailableMemoryBytes,
                latestInfo.HighMemoryLoadThresholdBytes,
                latestInfo.TotalCommittedBytes,
                latest,
                ToObservation(latestGen2),
                latestInfo.PinnedObjectsCount,
                latestInfo.FinalizationPendingCount,
                lohBytes,
                lohFragmentedBytes
            );
        }
        catch
        {
            return GarbageCollectionValues.Empty;
        }
    }

    private static GCMemoryInfo? SelectLatestGen2(
        GCMemoryInfo? fullBlocking,
        GCMemoryInfo? background,
        GCMemoryInfo? latest
    )
    {
        var result = SelectNewerGen2(null, fullBlocking);
        result = SelectNewerGen2(result, background);
        return SelectNewerGen2(result, latest);
    }

    private static GCMemoryInfo? SelectNewerGen2(GCMemoryInfo? current, GCMemoryInfo? candidate) =>
        candidate is { Index: > 0, Generation: 2 }
        && (current is null || candidate.Value.Index > current.Value.Index)
            ? candidate
            : current;

    private static ProfilingGcObservation ToObservation(GCMemoryInfo? value)
    {
        if (value is not { Index: > 0 } info)
        {
            return null;
        }

        var generationInfo = info.GenerationInfo;
        var largeObjectHeapBytes =
            generationInfo.Length > 3 ? (long?)generationInfo[3].SizeAfterBytes : null;
        var pauseDuration = TimeSpan.Zero;
        foreach (var pause in info.PauseDurations)
        {
            pauseDuration += pause;
        }

        return new ProfilingGcObservation(
            info.Index,
            info.Generation,
            info.HeapSizeBytes,
            largeObjectHeapBytes,
            info.Compacted,
            info.Concurrent,
            pauseDuration
        );
    }

    private static ThreadPoolValues CaptureThreadPool()
    {
        try
        {
            ThreadPool.GetAvailableThreads(
                out var availableWorkerThreads,
                out var availableCompletionPortThreads
            );
            return new ThreadPoolValues(
                TryGet(() => (int?)ThreadPool.ThreadCount),
                TryGet(() => (long?)ThreadPool.CompletedWorkItemCount),
                TryGet(() => (long?)ThreadPool.PendingWorkItemCount),
                availableWorkerThreads,
                availableCompletionPortThreads
            );
        }
        catch
        {
            return new ThreadPoolValues(null, null, null, null, null);
        }
    }

    private static PhysicalMemoryValues CapturePhysicalMemory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new PhysicalMemoryValues(null, null);
        }

        try
        {
            var status = new MemoryStatus
            {
                Length = checked((uint)Marshal.SizeOf<MemoryStatus>()),
            };
            if (!GlobalMemoryStatusEx(ref status))
            {
                return new PhysicalMemoryValues(null, null);
            }

            return new PhysicalMemoryValues(
                checked((long)status.TotalPhysical),
                checked((long)status.AvailablePhysical)
            );
        }
        catch
        {
            return new PhysicalMemoryValues(null, null);
        }
    }

    private static SocketValues CaptureSockets()
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var active = properties.GetActiveTcpConnections().Length;
            var tcpListeners = properties.GetActiveTcpListeners().Length;
            var udpListeners = properties.GetActiveUdpListeners().Length;
            return new SocketValues(
                active,
                tcpListeners,
                udpListeners,
                active + tcpListeners + udpListeners
            );
        }
        catch
        {
            return new SocketValues(null, null, null, null);
        }
    }

    private static T TryGet<T>(Func<T> accessor)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return default;
        }
    }

    private sealed record ProcessValues(
        TimeSpan? ProcessCpuDuration,
        long? WorkingSetBytes,
        long? PrivateMemoryBytes,
        int? HandleCount,
        int? ThreadCount
    );

    private sealed record GarbageCollectionValues(
        long? ManagedHeapSizeBytes,
        long? FragmentedBytes,
        long? MemoryLoadBytes,
        long? TotalAvailableMemoryBytes,
        long? HighMemoryLoadThresholdBytes,
        long? TotalCommittedBytes,
        ProfilingGcObservation Latest,
        ProfilingGcObservation LatestGen2,
        long? PinnedObjectCount,
        long? FinalizationPendingCount,
        long? LargeObjectHeapBytes,
        long? LargeObjectHeapFragmentedBytes
    )
    {
        public static GarbageCollectionValues Empty { get; } =
            new(null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private sealed record ThreadPoolValues(
        int? ThreadCount,
        long? CompletedWorkItemCount,
        long? PendingWorkItemCount,
        int? AvailableWorkerThreads,
        int? AvailableCompletionPortThreads
    );

    private sealed record PhysicalMemoryValues(long? TotalBytes, long? AvailableBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    private sealed record SocketValues(
        int? ActiveTcpConnectionCount,
        int? TcpListenerCount,
        int? UdpListenerCount,
        int? TotalUsedSocketCount
    );
}
