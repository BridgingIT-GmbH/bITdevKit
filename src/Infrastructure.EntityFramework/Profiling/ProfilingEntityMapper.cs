// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using BridgingIT.DevKit.Common;

/// <summary>Maps provider-neutral profiling models to durable Entity Framework rows.</summary>
/// <example><code>var model = ProfilingEntityMapper.ToModel(entity);</code></example>
public static class ProfilingEntityMapper
{
    /// <summary>Maps a session creation request to a durable active-session row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(request);</code></example>
    public static ProfilingSessionEntity ToEntity(ProfilingSessionCreateRequest request) =>
        new()
        {
            Id = request.Identity.Id,
            Key = request.Identity.Key,
            LifecycleKey = EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey,
            Name = NormalizeOptional(request.Name),
            State = ProfilingSessionState.Running,
            StartedUtc = request.StartedUtc,
            EndsUtc = request.StartedUtc.Add(request.Duration),
            SamplingInterval = request.SamplingInterval,
            Duration = request.Duration,
            Tags = ToSessionTags(request.Identity.Id, request.Tags),
        };

    /// <summary>Maps an imported terminal session to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(session);</code></example>
    public static ProfilingSessionEntity ToEntity(ProfilingSession session) =>
        new()
        {
            Id = session.Identity.Id,
            Key = session.Identity.Key,
            LifecycleKey = session.Identity.Id.ToString("N"),
            Name = NormalizeOptional(session.Name),
            State = session.State,
            StartedUtc = session.StartedUtc,
            EndsUtc = session.EndsUtc,
            CompletedUtc = session.CompletedUtc,
            SamplingInterval = session.SamplingInterval,
            Duration = session.Duration,
            IsPinned = session.IsPinned,
            Note = NormalizeOptional(session.Note),
            Tags = ToSessionTags(session.Identity.Id, session.Tags),
        };

    /// <summary>Maps a durable session row to its provider-neutral model.</summary>
    /// <example><code>var session = ProfilingEntityMapper.ToModel(entity);</code></example>
    public static ProfilingSession ToModel(ProfilingSessionEntity entity) =>
        new()
        {
            Identity = new ProfilingSessionIdentity(entity.Id, entity.Key),
            Name = entity.Name,
            State = entity.State,
            StartedUtc = entity.StartedUtc,
            EndsUtc = entity.EndsUtc,
            CompletedUtc = entity.CompletedUtc,
            SamplingInterval = entity.SamplingInterval,
            Duration = entity.Duration,
            IsPinned = entity.IsPinned,
            Tags = entity.Tags.OrderBy(x => x.Position).Select(x => x.Value).ToArray(),
            Note = entity.Note,
        };

    /// <summary>Maps normalized session tags to ordered durable rows.</summary>
    /// <example><code>var tags = ProfilingEntityMapper.ToSessionTags(sessionId, values);</code></example>
    public static ICollection<ProfilingSessionTagEntity> ToSessionTags(
        Guid sessionId,
        IEnumerable<string> values
    ) =>
        NormalizeStrings(values)
            .Select(
                (value, index) =>
                    new ProfilingSessionTagEntity
                    {
                        SessionId = sessionId,
                        Position = index,
                        Value = value,
                    }
            )
            .ToArray();

    /// <summary>Maps a stable profiling node to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(correlation, node);</code></example>
    public static ProfilingNodeEntity ToEntity(
        ProfilingNodeCorrelation correlation,
        ProfilingNode node
    ) =>
        new()
        {
            Id = node.Identity.Id,
            Key = node.Identity.Key,
            BroadcastNodeIdentity = correlation.BroadcastNodeIdentity.Trim(),
            ProcessStartedUtc = correlation.ProcessStartedUtc,
            HostName = node.HostName,
            ProcessId = node.ProcessId,
        };

    /// <summary>Maps a durable node row to its provider-neutral model.</summary>
    /// <example><code>var node = ProfilingEntityMapper.ToModel(entity);</code></example>
    public static ProfilingNode ToModel(ProfilingNodeEntity entity) =>
        new()
        {
            Identity = new ProfilingNodeIdentity(entity.Id, entity.Key),
            Correlation = new ProfilingNodeCorrelation(
                entity.BroadcastNodeIdentity,
                entity.ProcessStartedUtc
            ),
            HostName = entity.HostName,
            ProcessId = entity.ProcessId,
        };

    /// <summary>Maps node participation to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(participation);</code></example>
    public static ProfilingParticipationEntity ToEntity(ProfilingNodeParticipation model) =>
        new()
        {
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            Role = model.Role,
            State = model.State,
            JoinedUtc = model.JoinedUtc,
            CompletedUtc = model.CompletedUtc,
            SuccessfulCaptureCount = model.SuccessfulCaptureCount,
            SkippedCaptureCount = model.SkippedCaptureCount,
            FailedCaptureCount = model.FailedCaptureCount,
            Failure = NormalizeOptional(model.Failure),
        };

    /// <summary>Maps a durable participation row and readable keys to its model.</summary>
    /// <example><code>var participation = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingNodeParticipation ToModel(
        ProfilingParticipationEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new()
        {
            SessionId = entity.SessionId,
            SessionKey = sessionKey,
            NodeId = entity.NodeId,
            NodeKey = nodeKey,
            Role = entity.Role,
            State = entity.State,
            JoinedUtc = entity.JoinedUtc,
            CompletedUtc = entity.CompletedUtc,
            SuccessfulCaptureCount = entity.SuccessfulCaptureCount,
            SkippedCaptureCount = entity.SkippedCaptureCount,
            FailedCaptureCount = entity.FailedCaptureCount,
            Failure = entity.Failure,
        };

    /// <summary>Maps immutable runtime context to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(runtimeContext);</code></example>
    public static ProfilingRuntimeContextEntity ToEntity(ProfilingRuntimeContext model) =>
        new()
        {
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            ApplicationName = model.ApplicationName,
            ApplicationVersion = model.ApplicationVersion,
            RuntimeDescription = model.RuntimeDescription,
            RuntimeVersion = model.RuntimeVersion,
            OperatingSystemDescription = model.OperatingSystemDescription,
            OperatingSystemArchitecture = model.OperatingSystemArchitecture,
            ProcessArchitecture = model.ProcessArchitecture,
            ServerGarbageCollection = model.ServerGarbageCollection,
            LogicalProcessorCount = model.LogicalProcessorCount,
            ProcessStartedUtc = model.ProcessStartedUtc,
            DebuggerAttached = model.DebuggerAttached,
        };

    /// <summary>Maps a durable runtime-context row and readable keys to its model.</summary>
    /// <example><code>var context = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingRuntimeContext ToModel(
        ProfilingRuntimeContextEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new()
        {
            SessionId = entity.SessionId,
            SessionKey = sessionKey,
            NodeId = entity.NodeId,
            NodeKey = nodeKey,
            ApplicationName = entity.ApplicationName,
            ApplicationVersion = entity.ApplicationVersion,
            RuntimeDescription = entity.RuntimeDescription,
            RuntimeVersion = entity.RuntimeVersion,
            OperatingSystemDescription = entity.OperatingSystemDescription,
            OperatingSystemArchitecture = entity.OperatingSystemArchitecture,
            ProcessArchitecture = entity.ProcessArchitecture,
            ServerGarbageCollection = entity.ServerGarbageCollection,
            LogicalProcessorCount = entity.LogicalProcessorCount,
            ProcessStartedUtc = entity.ProcessStartedUtc,
            DebuggerAttached = entity.DebuggerAttached,
        };

    /// <summary>Maps an immutable runtime snapshot to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(snapshot);</code></example>
    public static ProfilingSnapshotEntity ToEntity(ProfilingSnapshot model) =>
        new()
        {
            Id = model.Identity.Id,
            Key = model.Identity.Key,
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            TimestampUtc = model.TimestampUtc,
            HostName = model.HostName,
            ProcessId = model.ProcessId,
            Sequence = model.Sequence,
            ScheduledElapsed = model.ScheduledElapsed,
            CaptureStartedElapsed = model.CaptureStartedElapsed,
            CaptureDuration = model.CaptureDuration,
            SkippedCaptureCount = model.SkippedCaptureCount,
            FailedCaptureCount = model.FailedCaptureCount,
            CpuUsagePercent = model.CpuUsagePercent,
            ProcessCpuDuration = model.ProcessCpuDuration,
            LogicalProcessorCount = model.LogicalProcessorCount,
            WorkingSetBytes = model.WorkingSetBytes,
            PrivateMemoryBytes = model.PrivateMemoryBytes,
            ManagedMemoryBytes = model.ManagedMemoryBytes,
            TotalPhysicalMemoryBytes = model.TotalPhysicalMemoryBytes,
            AvailablePhysicalMemoryBytes = model.AvailablePhysicalMemoryBytes,
            UsedPhysicalMemoryBytes = model.UsedPhysicalMemoryBytes,
            ManagedHeapSizeBytes = model.ManagedHeapSizeBytes,
            FragmentedBytes = model.FragmentedBytes,
            HeapFragmentationPercent = model.HeapFragmentationPercent,
            MemoryLoadBytes = model.MemoryLoadBytes,
            TotalAvailableMemoryBytes = model.TotalAvailableMemoryBytes,
            HighMemoryLoadThresholdBytes = model.HighMemoryLoadThresholdBytes,
            TotalCommittedBytes = model.TotalCommittedBytes,
            TotalAllocatedBytes = model.TotalAllocatedBytes,
            AllocationRateBytesPerSecond = model.AllocationRateBytesPerSecond,
            MemoryPressurePercent = model.MemoryPressurePercent,
            Gen0CollectionCount = model.Gen0CollectionCount,
            Gen1CollectionCount = model.Gen1CollectionCount,
            Gen2CollectionCount = model.Gen2CollectionCount,
            LatestGcIndex = model.LatestGcIndex,
            LatestGcGeneration = model.LatestGcGeneration,
            LatestGcManagedHeapBytes = model.LatestGcManagedHeapBytes,
            LatestGcLargeObjectHeapBytes = model.LatestGcLargeObjectHeapBytes,
            LatestGcCompacting = model.LatestGcCompacting,
            LatestGcConcurrent = model.LatestGcConcurrent,
            LatestGen2GcIndex = model.LatestGen2GcIndex,
            LatestGen2ManagedHeapBytes = model.LatestGen2ManagedHeapBytes,
            LatestGen2LargeObjectHeapBytes = model.LatestGen2LargeObjectHeapBytes,
            LatestGen2GcCompacting = model.LatestGen2GcCompacting,
            LatestGen2GcConcurrent = model.LatestGen2GcConcurrent,
            CumulativeGcPauseDuration = model.CumulativeGcPauseDuration,
            GcPausePercent = model.GcPausePercent,
            PinnedObjectCount = model.PinnedObjectCount,
            FinalizationPendingCount = model.FinalizationPendingCount,
            LargeObjectHeapBytes = model.LargeObjectHeapBytes,
            LargeObjectHeapFragmentedBytes = model.LargeObjectHeapFragmentedBytes,
            LargeObjectHeapFragmentationPercent = model.LargeObjectHeapFragmentationPercent,
            ServerGarbageCollection = model.ServerGarbageCollection,
            GarbageCollectionLatencyMode = model.GarbageCollectionLatencyMode,
            ProcessHandleCount = model.ProcessHandleCount,
            ProcessThreadCount = model.ProcessThreadCount,
            ThreadPoolThreadCount = model.ThreadPoolThreadCount,
            ThreadPoolCompletedWorkItemCount = model.ThreadPoolCompletedWorkItemCount,
            ThreadPoolPendingWorkItemCount = model.ThreadPoolPendingWorkItemCount,
            ThreadPoolAvailableWorkerThreadCount = model.ThreadPoolAvailableWorkerThreadCount,
            ThreadPoolAvailableCompletionPortThreadCount =
                model.ThreadPoolAvailableCompletionPortThreadCount,
            ActiveTcpConnectionCount = model.ActiveTcpConnectionCount,
            TcpListenerCount = model.TcpListenerCount,
            UdpListenerCount = model.UdpListenerCount,
            TotalUsedSocketCount = model.TotalUsedSocketCount,
        };

    /// <summary>Maps a durable snapshot row and readable keys to its model.</summary>
    /// <example><code>var snapshot = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingSnapshot ToModel(
        ProfilingSnapshotEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new()
        {
            Identity = new ProfilingSnapshotIdentity(entity.Id, entity.Key),
            SessionId = entity.SessionId,
            SessionKey = sessionKey,
            NodeId = entity.NodeId,
            NodeKey = nodeKey,
            TimestampUtc = entity.TimestampUtc,
            HostName = entity.HostName,
            ProcessId = entity.ProcessId,
            Sequence = entity.Sequence,
            ScheduledElapsed = entity.ScheduledElapsed,
            CaptureStartedElapsed = entity.CaptureStartedElapsed,
            CaptureDuration = entity.CaptureDuration,
            SkippedCaptureCount = entity.SkippedCaptureCount,
            FailedCaptureCount = entity.FailedCaptureCount,
            CpuUsagePercent = entity.CpuUsagePercent,
            ProcessCpuDuration = entity.ProcessCpuDuration,
            LogicalProcessorCount = entity.LogicalProcessorCount,
            WorkingSetBytes = entity.WorkingSetBytes,
            PrivateMemoryBytes = entity.PrivateMemoryBytes,
            ManagedMemoryBytes = entity.ManagedMemoryBytes,
            TotalPhysicalMemoryBytes = entity.TotalPhysicalMemoryBytes,
            AvailablePhysicalMemoryBytes = entity.AvailablePhysicalMemoryBytes,
            UsedPhysicalMemoryBytes = entity.UsedPhysicalMemoryBytes,
            ManagedHeapSizeBytes = entity.ManagedHeapSizeBytes,
            FragmentedBytes = entity.FragmentedBytes,
            HeapFragmentationPercent = entity.HeapFragmentationPercent,
            MemoryLoadBytes = entity.MemoryLoadBytes,
            TotalAvailableMemoryBytes = entity.TotalAvailableMemoryBytes,
            HighMemoryLoadThresholdBytes = entity.HighMemoryLoadThresholdBytes,
            TotalCommittedBytes = entity.TotalCommittedBytes,
            TotalAllocatedBytes = entity.TotalAllocatedBytes,
            AllocationRateBytesPerSecond = entity.AllocationRateBytesPerSecond,
            MemoryPressurePercent = entity.MemoryPressurePercent,
            Gen0CollectionCount = entity.Gen0CollectionCount,
            Gen1CollectionCount = entity.Gen1CollectionCount,
            Gen2CollectionCount = entity.Gen2CollectionCount,
            LatestGcIndex = entity.LatestGcIndex,
            LatestGcGeneration = entity.LatestGcGeneration,
            LatestGcManagedHeapBytes = entity.LatestGcManagedHeapBytes,
            LatestGcLargeObjectHeapBytes = entity.LatestGcLargeObjectHeapBytes,
            LatestGcCompacting = entity.LatestGcCompacting,
            LatestGcConcurrent = entity.LatestGcConcurrent,
            LatestGen2GcIndex = entity.LatestGen2GcIndex,
            LatestGen2ManagedHeapBytes = entity.LatestGen2ManagedHeapBytes,
            LatestGen2LargeObjectHeapBytes = entity.LatestGen2LargeObjectHeapBytes,
            LatestGen2GcCompacting = entity.LatestGen2GcCompacting,
            LatestGen2GcConcurrent = entity.LatestGen2GcConcurrent,
            CumulativeGcPauseDuration = entity.CumulativeGcPauseDuration,
            GcPausePercent = entity.GcPausePercent,
            PinnedObjectCount = entity.PinnedObjectCount,
            FinalizationPendingCount = entity.FinalizationPendingCount,
            LargeObjectHeapBytes = entity.LargeObjectHeapBytes,
            LargeObjectHeapFragmentedBytes = entity.LargeObjectHeapFragmentedBytes,
            LargeObjectHeapFragmentationPercent = entity.LargeObjectHeapFragmentationPercent,
            ServerGarbageCollection = entity.ServerGarbageCollection,
            GarbageCollectionLatencyMode = entity.GarbageCollectionLatencyMode,
            ProcessHandleCount = entity.ProcessHandleCount,
            ProcessThreadCount = entity.ProcessThreadCount,
            ThreadPoolThreadCount = entity.ThreadPoolThreadCount,
            ThreadPoolCompletedWorkItemCount = entity.ThreadPoolCompletedWorkItemCount,
            ThreadPoolPendingWorkItemCount = entity.ThreadPoolPendingWorkItemCount,
            ThreadPoolAvailableWorkerThreadCount = entity.ThreadPoolAvailableWorkerThreadCount,
            ThreadPoolAvailableCompletionPortThreadCount =
                entity.ThreadPoolAvailableCompletionPortThreadCount,
            ActiveTcpConnectionCount = entity.ActiveTcpConnectionCount,
            TcpListenerCount = entity.TcpListenerCount,
            UdpListenerCount = entity.UdpListenerCount,
            TotalUsedSocketCount = entity.TotalUsedSocketCount,
        };

    /// <summary>Maps an immutable phase marker to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(marker);</code></example>
    public static ProfilingPhaseMarkerEntity ToEntity(ProfilingPhaseMarker model) =>
        new()
        {
            Id = model.Id,
            SessionId = model.SessionId,
            Name = model.Name,
            TimestampUtc = model.TimestampUtc,
        };

    /// <summary>Maps a durable phase-marker row and readable session key to its model.</summary>
    /// <example><code>var marker = ProfilingEntityMapper.ToModel(entity, sessionKey);</code></example>
    public static ProfilingPhaseMarker ToModel(
        ProfilingPhaseMarkerEntity entity,
        string sessionKey
    ) => new(entity.Id, entity.SessionId, sessionKey, entity.Name, entity.TimestampUtc);

    /// <summary>Maps an immutable node action marker to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(marker);</code></example>
    public static ProfilingActionMarkerEntity ToEntity(ProfilingActionMarker model) =>
        new()
        {
            Id = model.Id,
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            Name = model.Name,
            TimestampUtc = model.TimestampUtc,
        };

    /// <summary>Maps a durable action-marker row and readable keys to its model.</summary>
    /// <example><code>var marker = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingActionMarker ToModel(
        ProfilingActionMarkerEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new(
            entity.Id,
            entity.SessionId,
            entity.NodeId,
            sessionKey,
            nodeKey,
            entity.Name,
            entity.TimestampUtc
        );

    /// <summary>Maps a measured segment to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(segment);</code></example>
    public static ProfilingSegmentEntity ToEntity(ProfilingSegment model) =>
        new()
        {
            Id = model.Id,
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            Name = model.Name,
            StartedUtc = model.StartedUtc,
            EndedUtc = model.EndedUtc,
            Elapsed = model.Elapsed,
            Outcome = model.Outcome,
            ExceptionType = model.ExceptionType,
            ExceptionMessage = model.ExceptionMessage,
            Note = model.Note,
            CorrelationId = model.CorrelationId,
            ParentSegmentId = model.ParentSegmentId,
            CollectionEndedBeforeOperation = model.CollectionEndedBeforeOperation,
            Tags = ToSegmentTags(model.Id, model.Tags),
        };

    /// <summary>Maps a durable segment row and readable keys to its model.</summary>
    /// <example><code>var segment = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingSegment ToModel(
        ProfilingSegmentEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new()
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            SessionKey = sessionKey,
            NodeId = entity.NodeId,
            NodeKey = nodeKey,
            Name = entity.Name,
            StartedUtc = entity.StartedUtc,
            EndedUtc = entity.EndedUtc,
            Elapsed = entity.Elapsed,
            Outcome = entity.Outcome,
            ExceptionType = entity.ExceptionType,
            ExceptionMessage = entity.ExceptionMessage,
            Tags = entity.Tags.OrderBy(x => x.Position).Select(x => x.Value).ToArray(),
            Note = entity.Note,
            CorrelationId = entity.CorrelationId,
            ParentSegmentId = entity.ParentSegmentId,
            CollectionEndedBeforeOperation = entity.CollectionEndedBeforeOperation,
        };

    /// <summary>Maps normalized segment tags to ordered durable rows.</summary>
    /// <example><code>var tags = ProfilingEntityMapper.ToSegmentTags(segmentId, values);</code></example>
    public static ICollection<ProfilingSegmentTagEntity> ToSegmentTags(
        Guid segmentId,
        IEnumerable<string> values
    ) =>
        NormalizeStrings(values)
            .Select(
                (value, index) =>
                    new ProfilingSegmentTagEntity
                    {
                        SegmentId = segmentId,
                        Position = index,
                        Value = value,
                    }
            )
            .ToArray();

    /// <summary>Maps an immutable custom metric observation to a durable row.</summary>
    /// <example><code>var entity = ProfilingEntityMapper.ToEntity(observation);</code></example>
    public static ProfilingMetricObservationEntity ToEntity(ProfilingMetricObservation model) =>
        new()
        {
            Id = model.Id,
            SessionId = model.SessionId,
            NodeId = model.NodeId,
            SegmentId = model.SegmentId,
            MetricIdentifier = model.MetricIdentifier,
            Kind = model.Kind,
            Value = model.Value,
            Unit = model.Unit,
            TimestampUtc = model.TimestampUtc,
        };

    /// <summary>Maps a durable metric-observation row and readable keys to its model.</summary>
    /// <example><code>var observation = ProfilingEntityMapper.ToModel(entity, sessionKey, nodeKey);</code></example>
    public static ProfilingMetricObservation ToModel(
        ProfilingMetricObservationEntity entity,
        string sessionKey,
        string nodeKey
    ) =>
        new()
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            SessionKey = sessionKey,
            NodeId = entity.NodeId,
            NodeKey = nodeKey,
            SegmentId = entity.SegmentId,
            MetricIdentifier = entity.MetricIdentifier,
            Kind = entity.Kind,
            Value = entity.Value,
            Unit = entity.Unit,
            TimestampUtc = entity.TimestampUtc,
        };

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] NormalizeStrings(IEnumerable<string> values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? [];
}

/// <summary>Defines durable profiling store coordination constants.</summary>
/// <example><code>var key = EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey;</code></example>
public static class EntityFrameworkProfilingStoreConstants
{
    /// <summary>Gets the unique lifecycle key used by the active session.</summary>
    /// <example><code>entity.LifecycleKey = EntityFrameworkProfilingStoreConstants.ActiveLifecycleKey;</code></example>
    public const string ActiveLifecycleKey = "active";
}
