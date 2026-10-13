// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>Identifies one stored profiling session.</summary>
/// <example><code>var identity = ProfilingIdentityFactory.CreateRuntimeSession();</code></example>
public readonly record struct RuntimeProfilingSessionIdentity
{
    /// <summary>Creates a validated session identity.</summary>
    /// <param name="id">The internal persistence identifier.</param>
    /// <param name="key">The public readable key.</param>
    /// <example><code>var identity = new RuntimeProfilingSessionIdentity(Guid.NewGuid(), "a1b2c3d4");</code></example>
    public RuntimeProfilingSessionIdentity(Guid id, string key)
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

}

/// <summary>Identifies one application-process node.</summary>
/// <example><code>var identity = ProfilingIdentityFactory.CreateNode();</code></example>
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

}

/// <summary>Identifies one immutable runtime snapshot.</summary>
/// <example><code>var identity = ProfilingIdentityFactory.CreateRuntimeSnapshot();</code></example>
public readonly record struct RuntimeProfilingSnapshotIdentity
{
    /// <summary>Creates a validated snapshot identity.</summary>
    /// <param name="id">The internal persistence identifier.</param>
    /// <param name="key">The public readable key.</param>
    /// <example><code>var identity = new RuntimeProfilingSnapshotIdentity(Guid.NewGuid(), "i9j0k1l2");</code></example>
    public RuntimeProfilingSnapshotIdentity(Guid id, string key)
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

}

/// <summary>References one session through its public key.</summary>
/// <param name="Key">The eight-character session key.</param>
/// <example><code>var reference = new RuntimeProfilingSessionReference("a1b2c3d4");</code></example>
public sealed record RuntimeProfilingSessionReference(string Key);

/// <summary>References one node through its public key.</summary>
/// <param name="Key">The eight-character node key.</param>
/// <example><code>var reference = new ProfilingNodeReference("e5f6g7h8");</code></example>
public sealed record ProfilingNodeReference(string Key);

/// <summary>References one snapshot through its public key.</summary>
/// <param name="Key">The eight-character snapshot key.</param>
/// <example><code>var reference = new RuntimeProfilingSnapshotReference("i9j0k1l2");</code></example>
public sealed record RuntimeProfilingSnapshotReference(string Key);

/// <summary>
/// Correlates one private Broadcast registration to a stable profiling node.
/// </summary>
/// <param name="BroadcastNodeIdentity">The private Broadcast node identity.</param>
/// <param name="ProcessStartedUtc">The registered process start timestamp.</param>
/// <remarks>This persistence correlation must not be exposed by application-facing APIs.</remarks>
/// <example><code>var correlation = new RuntimeProfilingNodeCorrelation(identity, processStartedUtc);</code></example>
public sealed record RuntimeProfilingNodeCorrelation(
    string BroadcastNodeIdentity,
    DateTimeOffset ProcessStartedUtc
);

/// <summary>Describes the lifecycle state of a profiling session.</summary>
/// <example><code>var state = RuntimeProfilingSessionState.Running;</code></example>
public enum RuntimeProfilingSessionState
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
/// <example><code>var role = RuntimeProfilingNodeRole.ExpectedParticipant;</code></example>
public enum RuntimeProfilingNodeRole
{
    /// <summary>The node accepted the start command within the participation deadline.</summary>
    ExpectedParticipant,

    /// <summary>The node contributed through a later manual snapshot.</summary>
    AdHocContributor,
}

/// <summary>Describes node-local collection progress.</summary>
/// <example><code>var state = RuntimeProfilingParticipationState.Collecting;</code></example>
public enum RuntimeProfilingParticipationState
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
public sealed record RuntimeProfilingSession
{
    /// <summary>Gets the session identity.</summary>
    public RuntimeProfilingSessionIdentity Identity { get; init; }

    /// <summary>Gets the optional display name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the current lifecycle state.</summary>
    public RuntimeProfilingSessionState State { get; init; }

    /// <summary>Gets the logical UTC start time.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets the original logical UTC end time.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset EndsUtc { get; init; }

    /// <summary>Gets the terminal transition timestamp when available.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
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
    public RuntimeProfilingNodeCorrelation Correlation { get; init; }

    /// <summary>Gets the machine or container hostname metadata.</summary>
    public string HostName { get; init; }

    /// <summary>Gets the bounded host-configured node display name.</summary>
    /// <example><code>var display = node.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Gets the cached actual process-start UTC.</summary>
    /// <example><code>var started = node.ProcessStartedUtc;</code></example>
    [JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ProcessStartedUtc { get; init; }

    /// <summary>Gets bounded application version metadata.</summary>
    /// <example><code>var version = node.ApplicationVersion;</code></example>
    public string ApplicationVersion { get; init; }

    /// <summary>Gets the process identifier metadata.</summary>
    public int ProcessId { get; init; }
}

/// <summary>Preserves one node's role, state, and capture totals in a session.</summary>
/// <example><code>var skipped = participation.SkippedCaptureCount;</code></example>
public sealed record RuntimeProfilingNodeParticipation
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
    public RuntimeProfilingNodeRole Role { get; init; }

    /// <summary>Gets the current node-local collection state.</summary>
    public RuntimeProfilingParticipationState State { get; init; }

    /// <summary>Gets when the node accepted or joined the session.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset JoinedUtc { get; init; }

    /// <summary>Gets when local collection ended, when known.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
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
public sealed record RuntimeProfilingContext
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
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ProcessStartedUtc { get; init; }

    /// <summary>Gets whether a debugger was attached when the context was created.</summary>
    public bool DebuggerAttached { get; init; }
}

/// <summary>Contains one immutable node-local runtime snapshot.</summary>
/// <example><code>var cpu = snapshot.CpuUsagePercent;</code></example>
public sealed record RuntimeProfilingSnapshot
{
    /// <summary>Gets the snapshot identity.</summary>
    public RuntimeProfilingSnapshotIdentity Identity { get; init; }

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
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
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

/// <summary>Identifies whether an instantaneous Runtime annotation owns a node reference.</summary>
/// <example><code>var scope = ProfilingMarkerScope.Session;</code></example>
public enum ProfilingMarkerScope
{
    /// <summary>An annotation belongs to the session without an invented node.</summary>
    /// <example><code>var scope = ProfilingMarkerScope.Session;</code></example>
    Session,
    /// <summary>An annotation belongs to one recorded process.</summary>
    /// <example><code>var scope = ProfilingMarkerScope.Node;</code></example>
    Node
}

/// <summary>Describes an immutable instantaneous Runtime annotation.</summary>
/// <example><code>var marker = new ProfilingMarker(Guid.NewGuid(), sessionId, sessionKey, "Baseline", utc);</code></example>
public sealed record ProfilingMarker
{
    /// <summary>Creates a session-wide annotation with no node reference.</summary>
    /// <example><code>var marker = new ProfilingMarker(Guid.NewGuid(), sessionId, sessionKey, "Baseline", utc);</code></example>
    public ProfilingMarker(Guid id, Guid sessionId, string sessionKey, string name, DateTimeOffset timestampUtc, string kind = "Annotation")
    {
        this.Id = id; this.SessionId = sessionId; this.SessionKey = sessionKey;
        this.Name = name; this.TimestampUtc = timestampUtc; this.Kind = kind;
        this.Scope = ProfilingMarkerScope.Session;
    }

    /// <summary>Creates a node-specific annotation.</summary>
    /// <example><code>var marker = new ProfilingMarker(Guid.NewGuid(), sessionId, nodeId, sessionKey, nodeKey, "Manual GC", utc, "GarbageCollection");</code></example>
    public ProfilingMarker(Guid id, Guid sessionId, Guid nodeId, string sessionKey, string nodeKey, string name, DateTimeOffset timestampUtc, string kind = "Action")
        : this(id, sessionId, sessionKey, name, timestampUtc, kind)
    {
        this.Scope = ProfilingMarkerScope.Node; this.NodeId = nodeId; this.NodeKey = nodeKey;
    }

    /// <summary>Creates a marker for JSON materialization.</summary>
    /// <example><code>var marker = new ProfilingMarker { Scope = ProfilingMarkerScope.Session };</code></example>
    [JsonConstructor]
    public ProfilingMarker() { }

    /// <summary>Gets the internal marker identifier.</summary>
    /// <example><code>var id = marker.Id;</code></example>
    [JsonIgnore]
    public Guid Id { get; init; }
    /// <summary>Gets the internal owning session identifier.</summary>
    /// <example><code>var id = marker.SessionId;</code></example>
    [JsonIgnore]
    public Guid SessionId { get; init; }
    /// <summary>Gets the internal node identifier only for Node scope.</summary>
    /// <example><code>var id = marker.NodeId;</code></example>
    [JsonIgnore]
    public Guid? NodeId { get; init; }
    /// <summary>Gets the readable session key.</summary>
    /// <example><code>var key = marker.SessionKey;</code></example>
    public string SessionKey { get; init; }
    /// <summary>Gets the readable node key only for Node scope.</summary>
    /// <example><code>var key = marker.NodeKey;</code></example>
    public string NodeKey { get; init; }
    /// <summary>Gets explicit Session or Node scope.</summary>
    /// <example><code>var scope = marker.Scope;</code></example>
    public ProfilingMarkerScope Scope { get; init; }
    /// <summary>Gets a bounded stable annotation kind.</summary>
    /// <example><code>var kind = marker.Kind;</code></example>
    public string Kind { get; init; }
    /// <summary>Gets the plain display label.</summary>
    /// <example><code>var label = marker.Name;</code></example>
    public string Name { get; init; }
    /// <summary>Gets instantaneous observation UTC.</summary>
    /// <example><code>var utc = marker.TimestampUtc;</code></example>
    [JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset TimestampUtc { get; init; }
}

/// <summary>Separates an open Runtime interval from its terminal outcome.</summary>
/// <example><code>var state = RuntimeProfilingSegmentState.Open;</code></example>
public enum RuntimeProfilingSegmentState
{
    /// <summary>The interval has no observed terminal outcome.</summary>
    /// <example><code>var state = RuntimeProfilingSegmentState.Open;</code></example>
    Open,
    /// <summary>The interval has ended.</summary>
    /// <example><code>var state = RuntimeProfilingSegmentState.Closed;</code></example>
    Closed
}

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
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets the UTC end timestamp when known.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? EndedUtc { get; init; }

    /// <summary>Gets the elapsed duration when known.</summary>
    public TimeSpan? Elapsed { get; init; }

    /// <summary>Gets the segment outcome.</summary>
    public ProfilingSegmentOutcome? Outcome { get; init; }

    /// <summary>Gets interval state separately from the optional terminal outcome.</summary>
    /// <example><code>var state = segment.State;</code></example>
    public RuntimeProfilingSegmentState State => this.EndedUtc.HasValue ? RuntimeProfilingSegmentState.Closed : RuntimeProfilingSegmentState.Open;

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
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
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
/// <example><code>var request = new RuntimeProfilingSessionCreateRequest(identity, name, now, interval, duration, []);</code></example>
public sealed record RuntimeProfilingSessionCreateRequest(
    RuntimeProfilingSessionIdentity Identity,
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
public sealed record RuntimeProfilingSessionResolution(RuntimeProfilingSession Session, bool Created);

/// <summary>Contains the result of a complete profiling-store reset.</summary>
/// <param name="RemovedSessionCount">The removed session count.</param>
/// <param name="RemovedSnapshotCount">The removed snapshot count.</param>
/// <example><code>var removed = result.RemovedSessionCount;</code></example>
public sealed record ProfilingClearResult(int RemovedSessionCount = 0, long RemovedSnapshotCount = 0)
{
    /// <summary>Gets the selected independent history datasets.</summary>
    /// <example><code>var value = record.DataSet;</code></example>
    public ProfilingDataSet DataSet { get; init; }

    /// <summary>Gets the normalized inclusive completion-UTC lower bound.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the normalized exclusive completion-UTC upper bound.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets the provider-coordinated clear occurrence.</summary>
    /// <example><code>var id = result.ClearId;</code></example>
    public Guid ClearId { get; init; }
    /// <summary>Gets roots removed from Operation history.</summary>
    /// <example><code>var count = result.RemovedOperationCount;</code></example>
    public long RemovedOperationCount { get; init; }
    /// <summary>Gets operation summaries removed with their owning roots.</summary>
    /// <example><code>var count = result.RemovedSegmentSummaryCount;</code></example>
    public long RemovedSegmentSummaryCount { get; init; }
    /// <summary>Gets the bounded maintenance lifecycle state.</summary>
    /// <example><code>var state = result.State;</code></example>
    public ProfilingClearState State { get; init; }
    /// <summary>Gets provider-authoritative preparation UTC.</summary>
    /// <example><code>var utc = result.PreparedUtc;</code></example>
    [JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset PreparedUtc { get; init; }
    /// <summary>Gets provider-authoritative sealing UTC when a fence is durable.</summary>
    /// <example><code>var utc = result.SealedUtc;</code></example>
    [JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? SealedUtc { get; init; }
    /// <summary>Gets completion UTC when deletion and late-write fencing are complete.</summary>
    /// <example><code>var utc = result.CompletedUtc;</code></example>
    [JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? CompletedUtc { get; init; }
    /// <summary>Gets the provider scope in which history was cleared.</summary>
    /// <example><code>var scope = result.ProviderScope;</code></example>
    public string ProviderScope { get; init; }
    /// <summary>Gets whether additional bounded deletion remains.</summary>
    /// <example><code>var pending = result.HasRemainingWork;</code></example>
    public bool HasRemainingWork { get; init; }
}

/// <summary>Defines a start or restart request using validated collection settings.</summary>
/// <param name="Name">The optional session name.</param>
/// <param name="SamplingInterval">The optional sampling-interval override.</param>
/// <param name="Duration">The optional required-duration override.</param>
/// <param name="Tags">Optional plain session tags.</param>
/// <example><code>var request = new RuntimeProfilingStartRequest("warm-up", duration: TimeSpan.FromSeconds(30));</code></example>
public sealed record RuntimeProfilingStartRequest(
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
/// <example><code>var update = new RuntimeProfilingSessionMetadata("warm-up", ["local"], null, true);</code></example>
public sealed record RuntimeProfilingSessionMetadata(
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
/// <example><code>var request = new RuntimeProfilingCaptureRequest(session, node, 1, elapsed, elapsed, 0, 0);</code></example>
public sealed record RuntimeProfilingCaptureRequest(
    RuntimeProfilingSession Session,
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
/// <example><code>var accepted = result.NodeOutcomes.Count(x => x.Outcome == RuntimeProfilingDeliveryOutcome.Accepted);</code></example>
public sealed record RuntimeProfilingControlResult(
    RuntimeProfilingSession Session,
    bool Created,
    IReadOnlyList<RuntimeProfilingNodeOutcome> NodeOutcomes
);

/// <summary>Describes an immediate delivery outcome using the public profiling node key.</summary>
/// <param name="NodeKey">The public profiling node key.</param>
/// <param name="Outcome">The immediate Broadcast delivery outcome.</param>
/// <param name="Detail">An optional safe description.</param>
/// <param name="Duration">The optional delivery duration.</param>
/// <example><code>var accepted = outcome.Outcome == RuntimeProfilingDeliveryOutcome.Accepted;</code></example>
public sealed record RuntimeProfilingNodeOutcome(
    string NodeKey,
    RuntimeProfilingDeliveryOutcome Outcome,
    string Detail = null,
    TimeSpan? Duration = null
);

/// <summary>Contains feature availability and current logical-session status.</summary>
/// <param name="Enabled">Whether profiling collection is enabled.</param>
/// <param name="Available">Whether required infrastructure is available.</param>
/// <param name="Session">The active session when one exists.</param>
/// <param name="Participations">The active session's node states.</param>
/// <example><code>var running = status.Session?.State == RuntimeProfilingSessionState.Running;</code></example>
public sealed record RuntimeProfilingStatus(
    bool Enabled,
    bool Available,
    RuntimeProfilingSession Session,
    IReadOnlyList<RuntimeProfilingNodeParticipation> Participations
);

/// <summary>Contains the complete stored records needed to render one session.</summary>
/// <example><code>var snapshots = data.Snapshots;</code></example>
public sealed record RuntimeProfilingSessionData
{
    /// <summary>Gets the selected session.</summary>
    public RuntimeProfilingSession Session { get; init; }

    /// <summary>Gets expected and ad-hoc node participations.</summary>
    public IReadOnlyList<RuntimeProfilingNodeParticipation> Participations { get; init; } = [];

    /// <summary>Gets contributing nodes.</summary>
    public IReadOnlyList<ProfilingNode> Nodes { get; init; } = [];

    /// <summary>Gets immutable node runtime contexts.</summary>
    public IReadOnlyList<RuntimeProfilingContext> RuntimeContexts { get; init; } = [];

    /// <summary>Gets immutable runtime snapshots.</summary>
    public IReadOnlyList<RuntimeProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets scoped instantaneous markers.</summary>
    public IReadOnlyList<ProfilingMarker> Markers { get; init; } = [];

    /// <summary>Gets node-owned measured segments.</summary>
    public IReadOnlyList<ProfilingSegment> Segments { get; init; } = [];

    /// <summary>Gets custom metric observations.</summary>
    public IReadOnlyList<ProfilingMetricObservation> MetricObservations { get; init; } = [];
}

/// <summary>Describes the supported deterministic evaluation mode.</summary>
/// <example><code>var mode = RuntimeProfilingEvaluationMode.NodeSession;</code></example>
public enum RuntimeProfilingEvaluationMode
{
    /// <summary>Evaluates exactly two ordered snapshots.</summary>
    TwoSnapshots,

    /// <summary>Evaluates the complete available node timeline.</summary>
    NodeSession,
}

/// <summary>Describes deterministic analysis sufficiency.</summary>
/// <example><code>var state = RuntimeProfilingDataSufficiency.Sufficient;</code></example>
public enum RuntimeProfilingDataSufficiency
{
    /// <summary>Too little valid data exists for interpretive signals.</summary>
    Collecting,

    /// <summary>Enough valid data exists for interpretive signals.</summary>
    Sufficient,

    /// <summary>Material data-quality limitations affect interpretation.</summary>
    Limited,
}

/// <summary>Describes a deterministic signal label.</summary>
/// <example><code>var label = RuntimeProfilingSignalLabel.Notable;</code></example>
public enum RuntimeProfilingSignalLabel
{
    /// <summary>The evidence is worth noting.</summary>
    Notable,

    /// <summary>The evidence warrants focused investigation.</summary>
    Investigate,
}

/// <summary>Describes deterministic evidence confidence.</summary>
/// <example><code>var confidence = RuntimeProfilingSignalConfidence.Medium;</code></example>
public enum RuntimeProfilingSignalConfidence
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
/// <example><code>var request = new RuntimeProfilingEvaluationRequest(sessionKey, nodeKey);</code></example>
public sealed record RuntimeProfilingEvaluationRequest(
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
public sealed record RuntimeProfilingEvaluationScope(
    RuntimeProfilingEvaluationMode Mode,
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
public sealed record RuntimeProfilingEvaluationDataQuality
{
    /// <summary>Gets the sufficiency state.</summary>
    public RuntimeProfilingDataSufficiency Sufficiency { get; init; }

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
/// <example><code>var kpi = new RuntimeProfilingKpi("cpu-average", 42.5, "percent");</code></example>
public sealed record RuntimeProfilingKpi(string Identifier, double? Value, string Unit);

/// <summary>Contains one raw value or threshold supporting a signal.</summary>
/// <param name="Identifier">The stable evidence identifier.</param>
/// <param name="Value">The raw evidence value.</param>
/// <param name="Unit">The value unit.</param>
/// <example><code>var evidence = new RuntimeProfilingSignalEvidence("cpu-average", 85, "percent");</code></example>
public sealed record RuntimeProfilingSignalEvidence(string Identifier, double Value, string Unit);

/// <summary>Contains one deterministic evidence-backed interpretation.</summary>
/// <param name="Identifier">The stable lowercase kebab-case signal identifier.</param>
/// <param name="Label">The fixed signal label.</param>
/// <param name="Explanation">The short deterministic explanation.</param>
/// <param name="Evidence">The raw values and thresholds that caused the signal.</param>
/// <param name="Confidence">The deterministic confidence.</param>
/// <param name="SuggestedAction">The one short fixed action.</param>
/// <example><code>var id = signal.Identifier;</code></example>
public sealed record RuntimeProfilingSignal(
    string Identifier,
    RuntimeProfilingSignalLabel Label,
    string Explanation,
    IReadOnlyList<RuntimeProfilingSignalEvidence> Evidence,
    RuntimeProfilingSignalConfidence Confidence,
    string SuggestedAction
);

/// <summary>Contains a complete unpersisted deterministic evaluation result.</summary>
/// <param name="Scope">The evaluated scope.</param>
/// <param name="DataQuality">The data-quality evidence.</param>
/// <param name="KPIs">The independently calculated KPI values.</param>
/// <param name="Signals">The evidence-backed signals.</param>
/// <param name="Limitations">The deterministic limitations.</param>
/// <example><code>var signals = result.Signals;</code></example>
public sealed record RuntimeProfilingEvaluationResult(
    RuntimeProfilingEvaluationScope Scope,
    RuntimeProfilingEvaluationDataQuality DataQuality,
    IReadOnlyList<RuntimeProfilingKpi> KPIs,
    IReadOnlyList<RuntimeProfilingSignal> Signals,
    IReadOnlyList<string> Limitations
);

/// <summary>Configures one bounded host-local profiling stress workload.</summary>
/// <example><code>var request = RuntimeProfilingStressRequest.Default with { DurationSeconds = 10 };</code></example>
public sealed record RuntimeProfilingStressRequest
{
    private const long MinimumDefaultRetainedBytes = 32L * 1024 * 1024;
    private const long FallbackDefaultRetainedBytes = 64L * 1024 * 1024;
    private const long MaximumDefaultRetainedBytes = 128L * 1024 * 1024;

    /// <summary>Gets a fresh request containing the dashboard workload defaults.</summary>
    /// <example><code>var request = RuntimeProfilingStressRequest.Default;</code></example>
    public static RuntimeProfilingStressRequest Default => new();

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
public sealed record RuntimeProfilingStressResult(
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

/// <summary>Describes immediate Runtime control delivery without a transport dependency.</summary>
/// <example><code>var outcome = RuntimeProfilingDeliveryOutcome.Accepted;</code></example>
public enum RuntimeProfilingDeliveryOutcome
{
    /// <summary>Reports Accepted delivery.</summary>
    Accepted,
    /// <summary>Reports AlreadyProcessed delivery.</summary>
    AlreadyProcessed,
    /// <summary>Reports Expired delivery.</summary>
    Expired,
    /// <summary>Reports Unsupported delivery.</summary>
    Unsupported,
    /// <summary>Reports Rejected delivery.</summary>
    Rejected,
    /// <summary>Reports Failed delivery.</summary>
    Failed,
    /// <summary>Reports Unreachable delivery.</summary>
    Unreachable,
    /// <summary>Reports TimedOut delivery.</summary>
    TimedOut,
}
