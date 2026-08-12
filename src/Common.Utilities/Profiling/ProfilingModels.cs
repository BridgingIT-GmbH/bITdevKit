// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>Identifies one stored profiling session.</summary>
/// <example><code>var identity = ProfilingSessionIdentity.Create();</code></example>
public readonly record struct ProfilingSessionIdentity
{
    /// <summary>Creates a validated session identity.</summary>
    /// <param name="id">The internal persistence identifier.</param>
    /// <param name="key">The public readable key.</param>
    /// <example><code>var identity = new ProfilingSessionIdentity(Guid.NewGuid(), "a1b2c3d4");</code></example>
    public ProfilingSessionIdentity(Guid id, string key)
    {
        this.Id = ProfilingIdentityGuard.ValidateId(id, nameof(id));
        this.Key = ProfilingIdentityGuard.ValidateKey(key, nameof(key));
    }

    /// <summary>Gets the internal persistence identifier.</summary>
    /// <example><code>var id = identity.Id;</code></example>
    [JsonIgnore]
    public Guid Id { get; }

    /// <summary>Gets the immutable public readable key.</summary>
    /// <example><code>var key = identity.Key;</code></example>
    public string Key { get; }

    /// <summary>Creates a new session identity.</summary>
    /// <returns>A new internal identifier and eight-character public key.</returns>
    /// <example><code>var identity = ProfilingSessionIdentity.Create();</code></example>
    public static ProfilingSessionIdentity Create() =>
        new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));
}

/// <summary>Identifies one application-process node.</summary>
/// <example><code>var identity = ProfilingNodeIdentity.Create();</code></example>
public readonly record struct ProfilingNodeIdentity
{
    /// <summary>Creates a validated node identity.</summary>
    /// <param name="id">The internal persistence identifier.</param>
    /// <param name="key">The public readable key.</param>
    /// <example><code>var identity = new ProfilingNodeIdentity(Guid.NewGuid(), "e5f6g7h8");</code></example>
    public ProfilingNodeIdentity(Guid id, string key)
    {
        this.Id = ProfilingIdentityGuard.ValidateId(id, nameof(id));
        this.Key = ProfilingIdentityGuard.ValidateKey(key, nameof(key));
    }

    /// <summary>Gets the internal persistence identifier.</summary>
    /// <example><code>var id = identity.Id;</code></example>
    [JsonIgnore]
    public Guid Id { get; }

    /// <summary>Gets the immutable public readable key.</summary>
    /// <example><code>var key = identity.Key;</code></example>
    public string Key { get; }

    /// <summary>Creates a new node identity.</summary>
    /// <returns>A new internal identifier and eight-character public key.</returns>
    /// <example><code>var identity = ProfilingNodeIdentity.Create();</code></example>
    public static ProfilingNodeIdentity Create() =>
        new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));
}

/// <summary>Identifies one immutable runtime snapshot.</summary>
/// <example><code>var identity = ProfilingSnapshotIdentity.Create();</code></example>
public readonly record struct ProfilingSnapshotIdentity
{
    /// <summary>Creates a validated snapshot identity.</summary>
    /// <param name="id">The internal persistence identifier.</param>
    /// <param name="key">The public readable key.</param>
    /// <example><code>var identity = new ProfilingSnapshotIdentity(Guid.NewGuid(), "i9j0k1l2");</code></example>
    public ProfilingSnapshotIdentity(Guid id, string key)
    {
        this.Id = ProfilingIdentityGuard.ValidateId(id, nameof(id));
        this.Key = ProfilingIdentityGuard.ValidateKey(key, nameof(key));
    }

    /// <summary>Gets the internal persistence identifier.</summary>
    /// <example><code>var id = identity.Id;</code></example>
    [JsonIgnore]
    public Guid Id { get; }

    /// <summary>Gets the immutable public readable key.</summary>
    /// <example><code>var key = identity.Key;</code></example>
    public string Key { get; }

    /// <summary>Creates a new snapshot identity.</summary>
    /// <returns>A new internal identifier and eight-character public key.</returns>
    /// <example><code>var identity = ProfilingSnapshotIdentity.Create();</code></example>
    public static ProfilingSnapshotIdentity Create() =>
        new(Guid.NewGuid(), KeyGenerator.CreateLowercase(8));
}

/// <summary>References one session through its public key.</summary>
/// <param name="Key">The eight-character session key.</param>
/// <example><code>var reference = new ProfilingSessionReference("a1b2c3d4");</code></example>
public sealed record ProfilingSessionReference(string Key);

/// <summary>References one node through its public key.</summary>
/// <param name="Key">The eight-character node key.</param>
/// <example><code>var reference = new ProfilingNodeReference("e5f6g7h8");</code></example>
public sealed record ProfilingNodeReference(string Key);

/// <summary>References one snapshot through its public key.</summary>
/// <param name="Key">The eight-character snapshot key.</param>
/// <example><code>var reference = new ProfilingSnapshotReference("i9j0k1l2");</code></example>
public sealed record ProfilingSnapshotReference(string Key);

/// <summary>
/// Correlates one private Broadcast registration to a stable profiling node.
/// </summary>
/// <param name="BroadcastNodeIdentity">The private Broadcast node identity.</param>
/// <param name="ProcessStartedUtc">The registered process start timestamp.</param>
/// <remarks>This persistence correlation must not be exposed by application-facing APIs.</remarks>
/// <example><code>var correlation = new ProfilingNodeCorrelation(identity, processStartedUtc);</code></example>
public sealed record ProfilingNodeCorrelation(
    string BroadcastNodeIdentity,
    DateTimeOffset ProcessStartedUtc
);

/// <summary>Describes the lifecycle state of a profiling session.</summary>
/// <example><code>var state = ProfilingSessionState.Running;</code></example>
public enum ProfilingSessionState
{
    /// <summary>The session is actively collecting snapshots.</summary>
    Running,

    /// <summary>The session completed normally.</summary>
    Completed,

    /// <summary>The session completed with incomplete or failed participant evidence.</summary>
    CompletedWithWarnings,

    /// <summary>The session was stopped explicitly.</summary>
    Stopped,

    /// <summary>The session failed.</summary>
    Failed,
}

/// <summary>Describes how a node contributes data to a session.</summary>
/// <example><code>var role = ProfilingNodeRole.ExpectedParticipant;</code></example>
public enum ProfilingNodeRole
{
    /// <summary>The node accepted the start command within the participation deadline.</summary>
    ExpectedParticipant,

    /// <summary>The node contributed through a later manual snapshot.</summary>
    AdHocContributor,
}

/// <summary>Describes node-local collection progress.</summary>
/// <example><code>var state = ProfilingParticipationState.Collecting;</code></example>
public enum ProfilingParticipationState
{
    /// <summary>The node was expected but has not yet confirmed local collection.</summary>
    Accepted,

    /// <summary>The node is collecting snapshots.</summary>
    Collecting,

    /// <summary>The node completed its local collection.</summary>
    Completed,

    /// <summary>The node stopped local collection.</summary>
    Stopped,

    /// <summary>The node failed or remained incomplete.</summary>
    Failed,
}

/// <summary>Describes the outcome of a measured segment.</summary>
/// <example><code>var outcome = ProfilingSegmentOutcome.Success;</code></example>
public enum ProfilingSegmentOutcome
{
    /// <summary>The segment remains open.</summary>
    Open,

    /// <summary>The measured operation completed successfully.</summary>
    Success,

    /// <summary>The measured operation failed.</summary>
    Failure,

    /// <summary>The measured operation was cancelled.</summary>
    Cancellation,

    /// <summary>The owning process or session ended before the segment closed.</summary>
    Interruption,
}

/// <summary>Describes a custom metric observation kind.</summary>
/// <example><code>var kind = ProfilingMetricKind.Counter;</code></example>
public enum ProfilingMetricKind
{
    /// <summary>An incremental or cumulative counter.</summary>
    Counter,

    /// <summary>A current gauge value.</summary>
    Gauge,

    /// <summary>A duration measurement.</summary>
    Duration,
}

/// <summary>Describes one logical profiling collection session.</summary>
/// <example><code>var key = session.Identity.Key;</code></example>
public sealed record ProfilingSession
{
    /// <summary>Gets the session identity.</summary>
    public ProfilingSessionIdentity Identity { get; init; }

    /// <summary>Gets the optional display name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the current lifecycle state.</summary>
    public ProfilingSessionState State { get; init; }

    /// <summary>Gets the logical UTC start time.</summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets the original logical UTC end time.</summary>
    public DateTimeOffset EndsUtc { get; init; }

    /// <summary>Gets the terminal transition timestamp when available.</summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>Gets the configured sampling interval.</summary>
    public TimeSpan SamplingInterval { get; init; }

    /// <summary>Gets the required maximum collection duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets whether the session is excluded from automatic retention.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Gets the plain metadata tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the optional free-text note.</summary>
    public string Note { get; init; }
}

/// <summary>Describes one stable profiling node and its private Broadcast correlation.</summary>
/// <example><code>var nodeKey = node.Identity.Key;</code></example>
public sealed record ProfilingNode
{
    /// <summary>Gets the stable process-lifetime profiling identity.</summary>
    public ProfilingNodeIdentity Identity { get; init; }

    /// <summary>Gets the private registration correlation used by store providers.</summary>
    [JsonIgnore]
    public ProfilingNodeCorrelation Correlation { get; init; }

    /// <summary>Gets the machine or container hostname metadata.</summary>
    public string HostName { get; init; }

    /// <summary>Gets the process identifier metadata.</summary>
    public int ProcessId { get; init; }
}

/// <summary>Preserves one node's role, state, and capture totals in a session.</summary>
/// <example><code>var skipped = participation.SkippedCaptureCount;</code></example>
public sealed record ProfilingNodeParticipation
{
    /// <summary>Gets the internal session identifier.</summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>Gets the internal node identifier.</summary>
    [JsonIgnore]
    public Guid NodeId { get; init; }

    /// <summary>Gets the public session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets whether the node is expected or an ad-hoc contributor.</summary>
    public ProfilingNodeRole Role { get; init; }

    /// <summary>Gets the current node-local collection state.</summary>
    public ProfilingParticipationState State { get; init; }

    /// <summary>Gets when the node accepted or joined the session.</summary>
    public DateTimeOffset JoinedUtc { get; init; }

    /// <summary>Gets when local collection ended, when known.</summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>Gets the latest successful capture total.</summary>
    public long SuccessfulCaptureCount { get; init; }

    /// <summary>Gets the latest skipped-opportunity total.</summary>
    public long SkippedCaptureCount { get; init; }

    /// <summary>Gets the latest failed-capture total.</summary>
    public long FailedCaptureCount { get; init; }

    /// <summary>Gets an optional safe failure description.</summary>
    public string Failure { get; init; }
}

/// <summary>Contains immutable non-sensitive runtime context for one session node.</summary>
/// <example><code>var runtime = context.RuntimeDescription;</code></example>
public sealed record ProfilingRuntimeContext
{
    /// <summary>Gets the internal session identifier.</summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>Gets the internal node identifier.</summary>
    [JsonIgnore]
    public Guid NodeId { get; init; }

    /// <summary>Gets the public session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets the application name when available.</summary>
    public string ApplicationName { get; init; }

    /// <summary>Gets the entry assembly informational version when available.</summary>
    public string ApplicationVersion { get; init; }

    /// <summary>Gets the .NET runtime description when available.</summary>
    public string RuntimeDescription { get; init; }

    /// <summary>Gets the .NET runtime version when available.</summary>
    public string RuntimeVersion { get; init; }

    /// <summary>Gets the operating-system description when available.</summary>
    public string OperatingSystemDescription { get; init; }

    /// <summary>Gets the operating-system architecture when available.</summary>
    public string OperatingSystemArchitecture { get; init; }

    /// <summary>Gets the process architecture when available.</summary>
    public string ProcessArchitecture { get; init; }

    /// <summary>Gets whether server GC was enabled when available.</summary>
    public bool? ServerGarbageCollection { get; init; }

    /// <summary>Gets the logical processor count when available.</summary>
    public int? LogicalProcessorCount { get; init; }

    /// <summary>Gets the process start timestamp in UTC.</summary>
    public DateTimeOffset ProcessStartedUtc { get; init; }

    /// <summary>Gets whether a debugger was attached when the context was created.</summary>
    public bool DebuggerAttached { get; init; }
}

/// <summary>Contains one immutable node-local runtime snapshot.</summary>
/// <example><code>var cpu = snapshot.CpuUsagePercent;</code></example>
public sealed record ProfilingSnapshot
{
    /// <summary>Gets the snapshot identity.</summary>
    public ProfilingSnapshotIdentity Identity { get; init; }

    /// <summary>Gets the internal session identifier.</summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>Gets the internal node identifier.</summary>
    [JsonIgnore]
    public Guid NodeId { get; init; }

    /// <summary>Gets the public session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets the UTC capture timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Gets the hostname metadata.</summary>
    public string HostName { get; init; }

    /// <summary>Gets the process identifier metadata.</summary>
    public int ProcessId { get; init; }

    /// <summary>Gets the node-local successful snapshot sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the scheduled monotonic elapsed duration.</summary>
    public TimeSpan ScheduledElapsed { get; init; }

    /// <summary>Gets the capture-start monotonic elapsed duration.</summary>
    public TimeSpan CaptureStartedElapsed { get; init; }

    /// <summary>Gets the monotonic capture duration.</summary>
    public TimeSpan CaptureDuration { get; init; }

    /// <summary>Gets the cumulative skipped-opportunity count.</summary>
    public long SkippedCaptureCount { get; init; }

    /// <summary>Gets the cumulative failed-capture count.</summary>
    public long FailedCaptureCount { get; init; }

    /// <summary>Gets CPU usage percent when available.</summary>
    public double? CpuUsagePercent { get; init; }

    /// <summary>Gets cumulative process CPU duration when available.</summary>
    public TimeSpan? ProcessCpuDuration { get; init; }

    /// <summary>Gets the logical processor count when available.</summary>
    public int? LogicalProcessorCount { get; init; }

    /// <summary>Gets working-set bytes when available.</summary>
    public long? WorkingSetBytes { get; init; }

    /// <summary>Gets private-memory bytes when available.</summary>
    public long? PrivateMemoryBytes { get; init; }

    /// <summary>Gets managed-memory bytes when available.</summary>
    public long? ManagedMemoryBytes { get; init; }

    /// <summary>Gets total physical-memory bytes when available.</summary>
    public long? TotalPhysicalMemoryBytes { get; init; }

    /// <summary>Gets available physical-memory bytes when available.</summary>
    public long? AvailablePhysicalMemoryBytes { get; init; }

    /// <summary>Gets used physical-memory bytes when available.</summary>
    public long? UsedPhysicalMemoryBytes { get; init; }

    /// <summary>Gets managed-heap bytes when available.</summary>
    public long? ManagedHeapSizeBytes { get; init; }

    /// <summary>Gets fragmented managed-heap bytes when available.</summary>
    public long? FragmentedBytes { get; init; }

    /// <summary>Gets managed-heap fragmentation percent when available.</summary>
    public double? HeapFragmentationPercent { get; init; }

    /// <summary>Gets runtime memory-load bytes when available.</summary>
    public long? MemoryLoadBytes { get; init; }

    /// <summary>Gets runtime total-available-memory bytes when available.</summary>
    public long? TotalAvailableMemoryBytes { get; init; }

    /// <summary>Gets the high-memory-load threshold in bytes when available.</summary>
    public long? HighMemoryLoadThresholdBytes { get; init; }

    /// <summary>Gets total committed bytes when available.</summary>
    public long? TotalCommittedBytes { get; init; }

    /// <summary>Gets total allocated bytes when available.</summary>
    public long? TotalAllocatedBytes { get; init; }

    /// <summary>Gets allocation rate in bytes per second when available.</summary>
    public double? AllocationRateBytesPerSecond { get; init; }

    /// <summary>Gets memory pressure percent when available.</summary>
    public double? MemoryPressurePercent { get; init; }

    /// <summary>Gets Gen0 collection count when available.</summary>
    public long? Gen0CollectionCount { get; init; }

    /// <summary>Gets Gen1 collection count when available.</summary>
    public long? Gen1CollectionCount { get; init; }

    /// <summary>Gets Gen2 collection count when available.</summary>
    public long? Gen2CollectionCount { get; init; }

    /// <summary>Gets the latest GC sequence or index when available.</summary>
    public long? LatestGcIndex { get; init; }

    /// <summary>Gets the latest collected GC generation when available.</summary>
    public int? LatestGcGeneration { get; init; }

    /// <summary>Gets latest post-GC managed-heap bytes when available.</summary>
    public long? LatestGcManagedHeapBytes { get; init; }

    /// <summary>Gets latest post-GC LOH bytes when available.</summary>
    public long? LatestGcLargeObjectHeapBytes { get; init; }

    /// <summary>Gets whether the latest GC was compacting when available.</summary>
    public bool? LatestGcCompacting { get; init; }

    /// <summary>Gets whether the latest GC was concurrent when available.</summary>
    public bool? LatestGcConcurrent { get; init; }

    /// <summary>Gets the latest Gen2 GC sequence or index when available.</summary>
    public long? LatestGen2GcIndex { get; init; }

    /// <summary>Gets latest post-Gen2 managed-heap bytes when available.</summary>
    public long? LatestGen2ManagedHeapBytes { get; init; }

    /// <summary>Gets latest post-Gen2 LOH bytes when available.</summary>
    public long? LatestGen2LargeObjectHeapBytes { get; init; }

    /// <summary>Gets whether the latest Gen2 GC was compacting when available.</summary>
    public bool? LatestGen2GcCompacting { get; init; }

    /// <summary>Gets whether the latest Gen2 GC was concurrent when available.</summary>
    public bool? LatestGen2GcConcurrent { get; init; }

    /// <summary>Gets cumulative GC pause duration when available.</summary>
    public TimeSpan? CumulativeGcPauseDuration { get; init; }

    /// <summary>Gets GC pause percent when available.</summary>
    public double? GcPausePercent { get; init; }

    /// <summary>Gets pinned-object count when available.</summary>
    public long? PinnedObjectCount { get; init; }

    /// <summary>Gets finalization-pending count when available.</summary>
    public long? FinalizationPendingCount { get; init; }

    /// <summary>Gets LOH size in bytes when available.</summary>
    public long? LargeObjectHeapBytes { get; init; }

    /// <summary>Gets fragmented LOH bytes when available.</summary>
    public long? LargeObjectHeapFragmentedBytes { get; init; }

    /// <summary>Gets LOH fragmentation percent when available.</summary>
    public double? LargeObjectHeapFragmentationPercent { get; init; }

    /// <summary>Gets whether server GC is active when available.</summary>
    public bool? ServerGarbageCollection { get; init; }

    /// <summary>Gets the GC latency-mode name when available.</summary>
    public string GarbageCollectionLatencyMode { get; init; }

    /// <summary>Gets process handle count when available.</summary>
    public int? ProcessHandleCount { get; init; }

    /// <summary>Gets process thread count when available.</summary>
    public int? ProcessThreadCount { get; init; }

    /// <summary>Gets thread-pool thread count when available.</summary>
    public int? ThreadPoolThreadCount { get; init; }

    /// <summary>Gets completed thread-pool work-item count when available.</summary>
    public long? ThreadPoolCompletedWorkItemCount { get; init; }

    /// <summary>Gets pending thread-pool work-item count when available.</summary>
    public long? ThreadPoolPendingWorkItemCount { get; init; }

    /// <summary>Gets available worker-thread count when available.</summary>
    public int? ThreadPoolAvailableWorkerThreadCount { get; init; }

    /// <summary>Gets available completion-port-thread count when available.</summary>
    public int? ThreadPoolAvailableCompletionPortThreadCount { get; init; }

    /// <summary>Gets active TCP connection count when available.</summary>
    public int? ActiveTcpConnectionCount { get; init; }

    /// <summary>Gets TCP listener count when available.</summary>
    public int? TcpListenerCount { get; init; }

    /// <summary>Gets UDP listener count when available.</summary>
    public int? UdpListenerCount { get; init; }

    /// <summary>Gets total used socket count when available.</summary>
    public int? TotalUsedSocketCount { get; init; }
}

/// <summary>Describes an immutable shared session phase marker.</summary>
/// <param name="Id">The internal marker identifier.</param>
/// <param name="SessionId">The internal session identifier.</param>
/// <param name="SessionKey">The public session key.</param>
/// <param name="Name">The trimmed marker label.</param>
/// <param name="TimestampUtc">The UTC marker timestamp.</param>
/// <example><code>var label = marker.Name;</code></example>
public sealed record ProfilingPhaseMarker(
    [property: JsonIgnore] Guid Id,
    [property: JsonIgnore] Guid SessionId,
    string SessionKey,
    string Name,
    DateTimeOffset TimestampUtc
);

/// <summary>Describes an immutable node-local action marker.</summary>
/// <param name="Id">The internal marker identifier.</param>
/// <param name="SessionId">The internal session identifier.</param>
/// <param name="NodeId">The internal node identifier.</param>
/// <param name="SessionKey">The public session key.</param>
/// <param name="NodeKey">The public node key.</param>
/// <param name="Name">The action name.</param>
/// <param name="TimestampUtc">The UTC action timestamp.</param>
/// <example><code>var action = marker.Name;</code></example>
public sealed record ProfilingActionMarker(
    [property: JsonIgnore] Guid Id,
    [property: JsonIgnore] Guid SessionId,
    [property: JsonIgnore] Guid NodeId,
    string SessionKey,
    string NodeKey,
    string Name,
    DateTimeOffset TimestampUtc
);

/// <summary>Describes one node-owned measured segment.</summary>
/// <example><code>var elapsed = segment.Elapsed;</code></example>
public sealed record ProfilingSegment
{
    /// <summary>Gets the internal segment identifier.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Gets the internal session identifier.</summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>Gets the internal owning-node identifier.</summary>
    [JsonIgnore]
    public Guid NodeId { get; init; }

    /// <summary>Gets the public session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets the segment name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the UTC start timestamp.</summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets the UTC end timestamp when known.</summary>
    public DateTimeOffset? EndedUtc { get; init; }

    /// <summary>Gets the elapsed duration when known.</summary>
    public TimeSpan? Elapsed { get; init; }

    /// <summary>Gets the segment outcome.</summary>
    public ProfilingSegmentOutcome Outcome { get; init; }

    /// <summary>Gets the safe exception type for a failed operation.</summary>
    public string ExceptionType { get; init; }

    /// <summary>Gets the safe exception message for a failed operation.</summary>
    public string ExceptionMessage { get; init; }

    /// <summary>Gets optional plain tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets an optional note.</summary>
    public string Note { get; init; }

    /// <summary>Gets an optional correlation or trace identifier.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets an optional internal parent-segment identifier.</summary>
    [JsonIgnore]
    public Guid? ParentSegmentId { get; init; }

    /// <summary>Gets whether collection ended before the measured operation.</summary>
    public bool CollectionEndedBeforeOperation { get; init; }
}

/// <summary>Describes one immutable observation from the existing DevKit meter.</summary>
/// <example><code>var metric = observation.MetricIdentifier;</code></example>
public sealed record ProfilingMetricObservation
{
    /// <summary>Gets the internal observation identifier.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Gets the internal session identifier.</summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>Gets the internal producing-node identifier.</summary>
    [JsonIgnore]
    public Guid NodeId { get; init; }

    /// <summary>Gets the optional ambient segment identifier.</summary>
    [JsonIgnore]
    public Guid? SegmentId { get; init; }

    /// <summary>Gets the public session key.</summary>
    public string SessionKey { get; init; }

    /// <summary>Gets the public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets the stable metric identifier.</summary>
    public string MetricIdentifier { get; init; }

    /// <summary>Gets the metric kind.</summary>
    public ProfilingMetricKind Kind { get; init; }

    /// <summary>Gets the observed numeric value.</summary>
    public double Value { get; init; }

    /// <summary>Gets the optional existing metric unit.</summary>
    public string Unit { get; init; }

    /// <summary>Gets the UTC observation timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; init; }
}

/// <summary>Describes provider capabilities used before distributed control mutates state.</summary>
/// <param name="SupportsMultiNode">Whether independent processes can share the provider.</param>
/// <example><code>if (!store.Capabilities.SupportsMultiNode) { /* require one target */ }</code></example>
public sealed record ProfilingStoreCapabilities(bool SupportsMultiNode);

/// <summary>Supplies the values for atomic session creation.</summary>
/// <param name="Identity">The proposed new session identity.</param>
/// <param name="Name">The optional session name.</param>
/// <param name="StartedUtc">The logical UTC start.</param>
/// <param name="SamplingInterval">The sampling interval.</param>
/// <param name="Duration">The required maximum duration.</param>
/// <param name="Tags">The copied or supplied tags.</param>
/// <example><code>var request = new ProfilingSessionCreateRequest(identity, name, now, interval, duration, []);</code></example>
public sealed record ProfilingSessionCreateRequest(
    ProfilingSessionIdentity Identity,
    string Name,
    DateTimeOffset StartedUtc,
    TimeSpan SamplingInterval,
    TimeSpan Duration,
    IReadOnlyList<string> Tags
);

/// <summary>Contains the result of atomic active-session resolution.</summary>
/// <param name="Session">The created or existing active session.</param>
/// <param name="Created">Whether this call created the session.</param>
/// <example><code>if (result.Created) { /* publish start once */ }</code></example>
public sealed record ProfilingSessionResolution(ProfilingSession Session, bool Created);

/// <summary>Contains the result of a complete profiling-store reset.</summary>
/// <param name="RemovedSessionCount">The removed session count.</param>
/// <param name="RemovedSnapshotCount">The removed snapshot count.</param>
/// <example><code>var removed = result.RemovedSessionCount;</code></example>
public sealed record ProfilingClearResult(int RemovedSessionCount, long RemovedSnapshotCount);

/// <summary>Defines a start or restart request using validated collection settings.</summary>
/// <param name="Name">The optional session name.</param>
/// <param name="SamplingInterval">The optional sampling-interval override.</param>
/// <param name="Duration">The optional required-duration override.</param>
/// <param name="Tags">Optional plain session tags.</param>
/// <example><code>var request = new ProfilingStartRequest("warm-up", duration: TimeSpan.FromSeconds(30));</code></example>
public sealed record ProfilingStartRequest(
    string Name = null,
    TimeSpan? SamplingInterval = null,
    TimeSpan? Duration = null,
    IReadOnlyList<string> Tags = null
);

/// <summary>Defines editable descriptive session metadata.</summary>
/// <param name="Name">The optional display name.</param>
/// <param name="Tags">The plain tags.</param>
/// <param name="Note">The optional free-text note.</param>
/// <param name="IsPinned">Whether automatic retention excludes the session.</param>
/// <example><code>var update = new ProfilingSessionMetadata("warm-up", ["local"], null, true);</code></example>
public sealed record ProfilingSessionMetadata(
    string Name,
    IReadOnlyList<string> Tags,
    string Note,
    bool IsPinned
);

/// <summary>Defines one local probe capture request.</summary>
/// <param name="Session">The active session.</param>
/// <param name="Node">The producing node.</param>
/// <param name="Sequence">The next successful sequence.</param>
/// <param name="ScheduledElapsed">The scheduled monotonic elapsed duration.</param>
/// <param name="CaptureStartedElapsed">The capture-start monotonic elapsed duration.</param>
/// <param name="SkippedCaptureCount">The cumulative skipped count.</param>
/// <param name="FailedCaptureCount">The cumulative failed count.</param>
/// <example><code>var request = new ProfilingCaptureRequest(session, node, 1, elapsed, elapsed, 0, 0);</code></example>
public sealed record ProfilingCaptureRequest(
    ProfilingSession Session,
    ProfilingNode Node,
    long Sequence,
    TimeSpan ScheduledElapsed,
    TimeSpan CaptureStartedElapsed,
    long SkippedCaptureCount,
    long FailedCaptureCount
);

/// <summary>Contains the immediate result of a profiling control operation.</summary>
/// <param name="Session">The affected session when one exists.</param>
/// <param name="Created">Whether this operation created a new logical session.</param>
/// <param name="NodeOutcomes">Immediate per-node delivery outcomes.</param>
/// <example><code>var accepted = result.NodeOutcomes.Count(x => x.Outcome == BroadcastDeliveryOutcome.Accepted);</code></example>
public sealed record ProfilingControlResult(
    ProfilingSession Session,
    bool Created,
    IReadOnlyList<ProfilingNodeOutcome> NodeOutcomes
);

/// <summary>Describes an immediate delivery outcome using the public profiling node key.</summary>
/// <param name="NodeKey">The public profiling node key.</param>
/// <param name="Outcome">The immediate Broadcast delivery outcome.</param>
/// <param name="Detail">An optional safe description.</param>
/// <param name="Duration">The optional delivery duration.</param>
/// <example><code>var accepted = outcome.Outcome == BroadcastDeliveryOutcome.Accepted;</code></example>
public sealed record ProfilingNodeOutcome(
    string NodeKey,
    BroadcastDeliveryOutcome Outcome,
    string Detail = null,
    TimeSpan? Duration = null
);

/// <summary>Contains feature availability and current logical-session status.</summary>
/// <param name="Enabled">Whether profiling collection is enabled.</param>
/// <param name="Available">Whether required infrastructure is available.</param>
/// <param name="Session">The active session when one exists.</param>
/// <param name="Participations">The active session's node states.</param>
/// <example><code>var running = status.Session?.State == ProfilingSessionState.Running;</code></example>
public sealed record ProfilingStatus(
    bool Enabled,
    bool Available,
    ProfilingSession Session,
    IReadOnlyList<ProfilingNodeParticipation> Participations
);

/// <summary>Contains the complete stored records needed to render one session.</summary>
/// <example><code>var snapshots = data.Snapshots;</code></example>
public sealed record ProfilingSessionData
{
    /// <summary>Gets the selected session.</summary>
    public ProfilingSession Session { get; init; }

    /// <summary>Gets expected and ad-hoc node participations.</summary>
    public IReadOnlyList<ProfilingNodeParticipation> Participations { get; init; } = [];

    /// <summary>Gets contributing nodes.</summary>
    public IReadOnlyList<ProfilingNode> Nodes { get; init; } = [];

    /// <summary>Gets immutable node runtime contexts.</summary>
    public IReadOnlyList<ProfilingRuntimeContext> RuntimeContexts { get; init; } = [];

    /// <summary>Gets immutable runtime snapshots.</summary>
    public IReadOnlyList<ProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets shared manual phase markers.</summary>
    public IReadOnlyList<ProfilingPhaseMarker> PhaseMarkers { get; init; } = [];

    /// <summary>Gets node-local action markers.</summary>
    public IReadOnlyList<ProfilingActionMarker> ActionMarkers { get; init; } = [];

    /// <summary>Gets node-owned measured segments.</summary>
    public IReadOnlyList<ProfilingSegment> Segments { get; init; } = [];

    /// <summary>Gets custom metric observations.</summary>
    public IReadOnlyList<ProfilingMetricObservation> MetricObservations { get; init; } = [];
}

/// <summary>Describes the supported deterministic evaluation mode.</summary>
/// <example><code>var mode = ProfilingEvaluationMode.NodeSession;</code></example>
public enum ProfilingEvaluationMode
{
    /// <summary>Evaluates exactly two ordered snapshots.</summary>
    TwoSnapshots,

    /// <summary>Evaluates the complete available node timeline.</summary>
    NodeSession,
}

/// <summary>Describes deterministic analysis sufficiency.</summary>
/// <example><code>var state = ProfilingDataSufficiency.Sufficient;</code></example>
public enum ProfilingDataSufficiency
{
    /// <summary>Too little valid data exists for interpretive signals.</summary>
    Collecting,

    /// <summary>Enough valid data exists for interpretive signals.</summary>
    Sufficient,

    /// <summary>Material data-quality limitations affect interpretation.</summary>
    Limited,
}

/// <summary>Describes a deterministic signal label.</summary>
/// <example><code>var label = ProfilingSignalLabel.Notable;</code></example>
public enum ProfilingSignalLabel
{
    /// <summary>The evidence is worth noting.</summary>
    Notable,

    /// <summary>The evidence warrants focused investigation.</summary>
    Investigate,
}

/// <summary>Describes deterministic evidence confidence.</summary>
/// <example><code>var confidence = ProfilingSignalConfidence.Medium;</code></example>
public enum ProfilingSignalConfidence
{
    /// <summary>Evidence is limited or comes from two snapshots.</summary>
    Low,

    /// <summary>Minimum timeline and primary evidence requirements are met.</summary>
    Medium,

    /// <summary>Sustained evidence and an independent supporting condition are present.</summary>
    High,
}

/// <summary>Defines one evaluation request using public readable keys.</summary>
/// <param name="SessionKey">The selected session key.</param>
/// <param name="NodeKey">The selected node key.</param>
/// <param name="SnapshotAKey">The optional earlier snapshot key.</param>
/// <param name="SnapshotBKey">The optional later snapshot key.</param>
/// <example><code>var request = new ProfilingEvaluationRequest(sessionKey, nodeKey);</code></example>
public sealed record ProfilingEvaluationRequest(
    string SessionKey,
    string NodeKey,
    string SnapshotAKey = null,
    string SnapshotBKey = null
);

/// <summary>Describes the exact scope evaluated.</summary>
/// <param name="Mode">The evaluation mode.</param>
/// <param name="SessionKey">The selected session key.</param>
/// <param name="NodeKey">The selected node key.</param>
/// <param name="SnapshotKeys">The optional ordered snapshot keys.</param>
/// <param name="StartedUtc">The evaluated UTC start.</param>
/// <param name="EndedUtc">The evaluated UTC end.</param>
/// <param name="SnapshotCount">The evaluated snapshot count.</param>
/// <param name="Provisional">Whether collection remains active.</param>
/// <example><code>var count = scope.SnapshotCount;</code></example>
public sealed record ProfilingEvaluationScope(
    ProfilingEvaluationMode Mode,
    string SessionKey,
    string NodeKey,
    IReadOnlyList<string> SnapshotKeys,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? EndedUtc,
    int SnapshotCount,
    bool Provisional
);

/// <summary>Describes the sampling and input quality of an evaluation.</summary>
/// <example><code>var coverage = quality.SamplingCoveragePercent;</code></example>
public sealed record ProfilingEvaluationDataQuality
{
    /// <summary>Gets the sufficiency state.</summary>
    public ProfilingDataSufficiency Sufficiency { get; init; }

    /// <summary>Gets available evaluator inputs.</summary>
    public IReadOnlyList<string> AvailableInputs { get; init; } = [];

    /// <summary>Gets missing evaluator inputs.</summary>
    public IReadOnlyList<string> MissingInputs { get; init; } = [];

    /// <summary>Gets sampling coverage percent when available.</summary>
    public double? SamplingCoveragePercent { get; init; }

    /// <summary>Gets skipped-capture count.</summary>
    public long SkippedCaptureCount { get; init; }

    /// <summary>Gets failed-capture count.</summary>
    public long FailedCaptureCount { get; init; }

    /// <summary>Gets p95 capture duration when available.</summary>
    public TimeSpan? CaptureDurationP95 { get; init; }

    /// <summary>Gets p95 capture overhead as a percentage of the configured interval.</summary>
    /// <example><code>var overhead = quality.CaptureOverheadP95Percent;</code></example>
    public double? CaptureOverheadP95Percent { get; init; }

    /// <summary>Gets p95 sampling delay when available.</summary>
    public TimeSpan? SamplingDelayP95 { get; init; }
}

/// <summary>Contains one named deterministic KPI value.</summary>
/// <param name="Identifier">The stable KPI identifier.</param>
/// <param name="Value">The calculated value when available.</param>
/// <param name="Unit">The value unit.</param>
/// <example><code>var kpi = new ProfilingKpi("cpu-average", 42.5, "percent");</code></example>
public sealed record ProfilingKpi(string Identifier, double? Value, string Unit);

/// <summary>Contains one raw value or threshold supporting a signal.</summary>
/// <param name="Identifier">The stable evidence identifier.</param>
/// <param name="Value">The raw evidence value.</param>
/// <param name="Unit">The value unit.</param>
/// <example><code>var evidence = new ProfilingSignalEvidence("cpu-average", 85, "percent");</code></example>
public sealed record ProfilingSignalEvidence(string Identifier, double Value, string Unit);

/// <summary>Contains one deterministic evidence-backed interpretation.</summary>
/// <param name="Identifier">The stable lowercase kebab-case signal identifier.</param>
/// <param name="Label">The fixed signal label.</param>
/// <param name="Explanation">The short deterministic explanation.</param>
/// <param name="Evidence">The raw values and thresholds that caused the signal.</param>
/// <param name="Confidence">The deterministic confidence.</param>
/// <param name="SuggestedAction">The one short fixed action.</param>
/// <example><code>var id = signal.Identifier;</code></example>
public sealed record ProfilingSignal(
    string Identifier,
    ProfilingSignalLabel Label,
    string Explanation,
    IReadOnlyList<ProfilingSignalEvidence> Evidence,
    ProfilingSignalConfidence Confidence,
    string SuggestedAction
);

/// <summary>Contains a complete unpersisted deterministic evaluation result.</summary>
/// <param name="Scope">The evaluated scope.</param>
/// <param name="DataQuality">The data-quality evidence.</param>
/// <param name="KPIs">The independently calculated KPI values.</param>
/// <param name="Signals">The evidence-backed signals.</param>
/// <param name="Limitations">The deterministic limitations.</param>
/// <example><code>var signals = result.Signals;</code></example>
public sealed record ProfilingEvaluationResult(
    ProfilingEvaluationScope Scope,
    ProfilingEvaluationDataQuality DataQuality,
    IReadOnlyList<ProfilingKpi> KPIs,
    IReadOnlyList<ProfilingSignal> Signals,
    IReadOnlyList<string> Limitations
);

/// <summary>Configures one bounded host-local profiling stress workload.</summary>
/// <example><code>var request = ProfilingStressRequest.Default with { DurationSeconds = 10 };</code></example>
public sealed record ProfilingStressRequest
{
    private const long MinimumDefaultRetainedBytes = 32L * 1024 * 1024;
    private const long FallbackDefaultRetainedBytes = 64L * 1024 * 1024;
    private const long MaximumDefaultRetainedBytes = 128L * 1024 * 1024;

    /// <summary>Gets a fresh request containing the dashboard workload defaults.</summary>
    /// <example><code>var request = ProfilingStressRequest.Default;</code></example>
    public static ProfilingStressRequest Default => new();

    /// <summary>Gets the bounded workload duration in seconds.</summary>
    /// <example><code>var seconds = request.DurationSeconds;</code></example>
    public int DurationSeconds { get; init; } = 30;

    /// <summary>Gets the number of dedicated CPU workers.</summary>
    /// <example><code>var workers = request.WorkerCount;</code></example>
    public int WorkerCount { get; init; } = Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>Gets the managed memory kept reachable during the workload.</summary>
    /// <example><code>var retainedBytes = request.RetainedMemoryBytes;</code></example>
    public long RetainedMemoryBytes { get; init; } = GetDefaultRetainedMemoryBytes();

    private static long GetDefaultRetainedMemoryBytes()
    {
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return available > 0
            ? Math.Clamp(
                available / 10,
                MinimumDefaultRetainedBytes,
                MaximumDefaultRetainedBytes
            )
            : FallbackDefaultRetainedBytes;
    }
}

/// <summary>Describes the accepted shape of a host-local stress workload.</summary>
/// <param name="Started">Whether this request started the workload.</param>
/// <param name="DurationSeconds">The bounded workload duration in seconds.</param>
/// <param name="WorkerCount">The dedicated CPU worker count.</param>
/// <param name="RetainedMemoryBytes">The managed memory kept reachable during the workload.</param>
/// <example><code>var started = result.Started;</code></example>
public sealed record ProfilingStressResult(
    bool Started,
    int DurationSeconds,
    int WorkerCount,
    long RetainedMemoryBytes
);

file static class ProfilingIdentityGuard
{
    public static Guid ValidateId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A profiling internal identifier cannot be empty.",
                parameterName
            );
        }

        return value;
    }

    public static string ValidateKey(string value, string parameterName)
    {
        if (
            value?.Length != 8
            || value.Any(character =>
                character is not (>= 'a' and <= 'z') && character is not (>= '0' and <= '9')
            )
        )
        {
            throw new ArgumentException(
                "A profiling public key must contain exactly eight lowercase ASCII letters or digits.",
                parameterName
            );
        }

        return value;
    }
}
