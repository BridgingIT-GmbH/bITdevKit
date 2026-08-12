// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents one immutable node-local profiling snapshot.</summary>
/// <example><code>public DbSet&lt;ProfilingSnapshotEntity&gt; ProfilingSnapshots { get; set; }</code></example>
[Table("__Profiling_Snapshots")]
[Index(nameof(Key), IsUnique = true)]
[Index(nameof(SessionId), nameof(NodeId), nameof(Sequence), IsUnique = true)]
[Index(nameof(SessionId), nameof(NodeId), nameof(TimestampUtc))]
public sealed class ProfilingSnapshotEntity
{
    /// <summary>Gets or sets the snapshot identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the readable snapshot key.</summary>
    [Required]
    [MaxLength(8)]
    public string Key { get; set; }

    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the capture timestamp.</summary>
    [Required]
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>Gets or sets hostname metadata.</summary>
    [MaxLength(256)]
    public string HostName { get; set; }

    /// <summary>Gets or sets process identifier metadata.</summary>
    [Required]
    public int ProcessId { get; set; }

    /// <summary>Gets or sets node-local successful sequence.</summary>
    [Required]
    public long Sequence { get; set; }

    /// <summary>Gets or sets scheduled monotonic elapsed duration.</summary>
    [Required]
    public TimeSpan ScheduledElapsed { get; set; }

    /// <summary>Gets or sets capture-start monotonic elapsed duration.</summary>
    [Required]
    public TimeSpan CaptureStartedElapsed { get; set; }

    /// <summary>Gets or sets monotonic capture duration.</summary>
    [Required]
    public TimeSpan CaptureDuration { get; set; }

    /// <summary>Gets or sets cumulative skipped captures.</summary>
    [Required]
    public long SkippedCaptureCount { get; set; }

    /// <summary>Gets or sets cumulative failed captures.</summary>
    [Required]
    public long FailedCaptureCount { get; set; }

    /// <summary>Gets or sets CPU usage percent.</summary>
    public double? CpuUsagePercent { get; set; }

    /// <summary>Gets or sets cumulative process CPU duration.</summary>
    public TimeSpan? ProcessCpuDuration { get; set; }

    /// <summary>Gets or sets logical processor count.</summary>
    public int? LogicalProcessorCount { get; set; }

    /// <summary>Gets or sets working-set bytes.</summary>
    public long? WorkingSetBytes { get; set; }

    /// <summary>Gets or sets private-memory bytes.</summary>
    public long? PrivateMemoryBytes { get; set; }

    /// <summary>Gets or sets managed-memory bytes.</summary>
    public long? ManagedMemoryBytes { get; set; }

    /// <summary>Gets or sets total physical-memory bytes.</summary>
    public long? TotalPhysicalMemoryBytes { get; set; }

    /// <summary>Gets or sets available physical-memory bytes.</summary>
    public long? AvailablePhysicalMemoryBytes { get; set; }

    /// <summary>Gets or sets used physical-memory bytes.</summary>
    public long? UsedPhysicalMemoryBytes { get; set; }

    /// <summary>Gets or sets managed-heap bytes.</summary>
    public long? ManagedHeapSizeBytes { get; set; }

    /// <summary>Gets or sets fragmented managed-heap bytes.</summary>
    public long? FragmentedBytes { get; set; }

    /// <summary>Gets or sets managed-heap fragmentation percent.</summary>
    public double? HeapFragmentationPercent { get; set; }

    /// <summary>Gets or sets runtime memory-load bytes.</summary>
    public long? MemoryLoadBytes { get; set; }

    /// <summary>Gets or sets runtime total-available-memory bytes.</summary>
    public long? TotalAvailableMemoryBytes { get; set; }

    /// <summary>Gets or sets high-memory-load threshold bytes.</summary>
    public long? HighMemoryLoadThresholdBytes { get; set; }

    /// <summary>Gets or sets total committed bytes.</summary>
    public long? TotalCommittedBytes { get; set; }

    /// <summary>Gets or sets total allocated bytes.</summary>
    public long? TotalAllocatedBytes { get; set; }

    /// <summary>Gets or sets allocation rate in bytes per second.</summary>
    public double? AllocationRateBytesPerSecond { get; set; }

    /// <summary>Gets or sets memory pressure percent.</summary>
    public double? MemoryPressurePercent { get; set; }

    /// <summary>Gets or sets Gen0 collection count.</summary>
    public long? Gen0CollectionCount { get; set; }

    /// <summary>Gets or sets Gen1 collection count.</summary>
    public long? Gen1CollectionCount { get; set; }

    /// <summary>Gets or sets Gen2 collection count.</summary>
    public long? Gen2CollectionCount { get; set; }

    /// <summary>Gets or sets latest GC index.</summary>
    public long? LatestGcIndex { get; set; }

    /// <summary>Gets or sets latest collected generation.</summary>
    public int? LatestGcGeneration { get; set; }

    /// <summary>Gets or sets latest post-GC managed-heap bytes.</summary>
    public long? LatestGcManagedHeapBytes { get; set; }

    /// <summary>Gets or sets latest post-GC LOH bytes.</summary>
    public long? LatestGcLargeObjectHeapBytes { get; set; }

    /// <summary>Gets or sets whether latest GC compacted.</summary>
    public bool? LatestGcCompacting { get; set; }

    /// <summary>Gets or sets whether latest GC was concurrent.</summary>
    public bool? LatestGcConcurrent { get; set; }

    /// <summary>Gets or sets latest Gen2 GC index.</summary>
    public long? LatestGen2GcIndex { get; set; }

    /// <summary>Gets or sets latest post-Gen2 managed-heap bytes.</summary>
    public long? LatestGen2ManagedHeapBytes { get; set; }

    /// <summary>Gets or sets latest post-Gen2 LOH bytes.</summary>
    public long? LatestGen2LargeObjectHeapBytes { get; set; }

    /// <summary>Gets or sets whether latest Gen2 GC compacted.</summary>
    public bool? LatestGen2GcCompacting { get; set; }

    /// <summary>Gets or sets whether latest Gen2 GC was concurrent.</summary>
    public bool? LatestGen2GcConcurrent { get; set; }

    /// <summary>Gets or sets cumulative GC pause duration.</summary>
    public TimeSpan? CumulativeGcPauseDuration { get; set; }

    /// <summary>Gets or sets GC pause percent.</summary>
    public double? GcPausePercent { get; set; }

    /// <summary>Gets or sets pinned-object count.</summary>
    public long? PinnedObjectCount { get; set; }

    /// <summary>Gets or sets finalization-pending count.</summary>
    public long? FinalizationPendingCount { get; set; }

    /// <summary>Gets or sets LOH size in bytes.</summary>
    public long? LargeObjectHeapBytes { get; set; }

    /// <summary>Gets or sets fragmented LOH bytes.</summary>
    public long? LargeObjectHeapFragmentedBytes { get; set; }

    /// <summary>Gets or sets LOH fragmentation percent.</summary>
    public double? LargeObjectHeapFragmentationPercent { get; set; }

    /// <summary>Gets or sets whether server GC is active.</summary>
    public bool? ServerGarbageCollection { get; set; }

    /// <summary>Gets or sets GC latency-mode name.</summary>
    [MaxLength(64)]
    public string GarbageCollectionLatencyMode { get; set; }

    /// <summary>Gets or sets process handle count.</summary>
    public int? ProcessHandleCount { get; set; }

    /// <summary>Gets or sets process thread count.</summary>
    public int? ProcessThreadCount { get; set; }

    /// <summary>Gets or sets thread-pool thread count.</summary>
    public int? ThreadPoolThreadCount { get; set; }

    /// <summary>Gets or sets completed thread-pool work-item count.</summary>
    public long? ThreadPoolCompletedWorkItemCount { get; set; }

    /// <summary>Gets or sets pending thread-pool work-item count.</summary>
    public long? ThreadPoolPendingWorkItemCount { get; set; }

    /// <summary>Gets or sets available worker-thread count.</summary>
    public int? ThreadPoolAvailableWorkerThreadCount { get; set; }

    /// <summary>Gets or sets available completion-port-thread count.</summary>
    public int? ThreadPoolAvailableCompletionPortThreadCount { get; set; }

    /// <summary>Gets or sets active TCP connection count.</summary>
    public int? ActiveTcpConnectionCount { get; set; }

    /// <summary>Gets or sets TCP listener count.</summary>
    public int? TcpListenerCount { get; set; }

    /// <summary>Gets or sets UDP listener count.</summary>
    public int? UdpListenerCount { get; set; }

    /// <summary>Gets or sets total used socket count.</summary>
    public int? TotalUsedSocketCount { get; set; }

    /// <summary>Gets or sets the owning session.</summary>
    [Required]
    [ForeignKey(nameof(SessionId))]
    public ProfilingSessionEntity Session { get; set; }

    /// <summary>Gets or sets the producing node.</summary>
    [Required]
    [ForeignKey(nameof(NodeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProfilingNodeEntity Node { get; set; }
}
