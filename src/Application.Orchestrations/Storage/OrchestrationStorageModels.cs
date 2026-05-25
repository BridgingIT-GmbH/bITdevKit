// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents the durable orchestration instance snapshot.
/// </summary>
public record OrchestrationInstanceSnapshot
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; init; }

    /// <summary>
    /// Gets or sets the current lifecycle status.
    /// </summary>
    public OrchestrationStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the current business state name.
    /// </summary>
    public string CurrentState { get; init; }

    /// <summary>
    /// Gets or sets the current activity name.
    /// </summary>
    public string CurrentActivity { get; init; }

    /// <summary>
    /// Gets or sets the orchestration correlation identifier.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the optional concurrency key.
    /// </summary>
    public string ConcurrencyKey { get; init; }

    /// <summary>
    /// Gets or sets the orchestration start timestamp.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// Gets or sets the orchestration completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// Gets or sets the orchestration data type identifier.
    /// </summary>
    public string ContextType { get; init; }

    /// <summary>
    /// Gets or sets the serialized durable context snapshot.
    /// </summary>
    public string SerializedContext { get; init; }

    /// <summary>
    /// Gets or sets the optimistic concurrency version.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the orchestration has been archived.
    /// </summary>
    public bool IsArchived { get; init; }

    /// <summary>
    /// Gets or sets the UTC archive timestamp when the orchestration has been archived.
    /// </summary>
    public DateTimeOffset? ArchivedUtc { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    public string CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    public string UpdatedBy { get; init; }
}

/// <summary>
/// Represents the durable orchestration context payload that is serialized inside an instance snapshot.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public record OrchestrationContextSnapshot<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Gets or sets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; init; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration correlation identifier.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration data.
    /// </summary>
    public TData Data { get; init; }

    /// <summary>
    /// Gets or sets the serialized execution-scoped properties.
    /// </summary>
    public Dictionary<string, OrchestrationContextPropertySnapshot> Properties { get; init; } = [];

    /// <summary>
    /// Gets or sets the current lifecycle status.
    /// </summary>
    public OrchestrationStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the current business state name.
    /// </summary>
    public string CurrentState { get; init; }

    /// <summary>
    /// Gets or sets the current activity name.
    /// </summary>
    public string CurrentActivity { get; init; }

    /// <summary>
    /// Gets or sets the orchestration start timestamp.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// Gets or sets the orchestration completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// Gets or sets the last activity outcome.
    /// </summary>
    public OrchestrationOutcome LastOutcome { get; init; }

    /// <summary>
    /// Gets or sets the last failure reason.
    /// </summary>
    public string FailureReason { get; init; }
}

/// <summary>
/// Represents one serialized property captured in a durable orchestration context snapshot.
/// </summary>
public record OrchestrationContextPropertySnapshot
{
    /// <summary>
    /// Gets or sets the serialized property type name.
    /// </summary>
    public string TypeName { get; init; }

    /// <summary>
    /// Gets or sets the serialized property value.
    /// </summary>
    public string SerializedValue { get; init; }
}

/// <summary>
/// Represents a durable, append-only orchestration history record.
/// </summary>
public record OrchestrationHistoryEntry
{
    /// <summary>
    /// Gets or sets the history entry identifier.
    /// </summary>
    public Guid EntryId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets or sets the state name associated with the entry.
    /// </summary>
    public string StateName { get; init; }

    /// <summary>
    /// Gets or sets the activity name associated with the entry.
    /// </summary>
    public string ActivityName { get; init; }

    /// <summary>
    /// Gets or sets additional event details.
    /// </summary>
    public string Details { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the event was recorded.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets or sets the user or worker that recorded the event when available.
    /// </summary>
    public string RecordedBy { get; init; }
}

/// <summary>
/// Represents the durable status of a persisted signal.
/// </summary>
public enum OrchestrationSignalStatus
{
    /// <summary>
    /// The signal is pending processing.
    /// </summary>
    Pending,

    /// <summary>
    /// The signal has been processed.
    /// </summary>
    Processed,

    /// <summary>
    /// The signal was ignored.
    /// </summary>
    Ignored,

    /// <summary>
    /// The signal was rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// The signal failed during processing.
    /// </summary>
    Failed,
}

/// <summary>
/// Represents a persisted orchestration signal.
/// </summary>
public record OrchestrationSignalRecord
{
    /// <summary>
    /// Gets or sets the signal identifier.
    /// </summary>
    public Guid SignalId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the signal name.
    /// </summary>
    public string SignalName { get; init; }

    /// <summary>
    /// Gets or sets the state captured when the signal was accepted.
    /// </summary>
    public string CurrentState { get; init; }

    /// <summary>
    /// Gets or sets the serialized signal payload.
    /// </summary>
    public string Payload { get; init; }

    /// <summary>
    /// Gets or sets the payload type identifier.
    /// </summary>
    public string PayloadType { get; init; }

    /// <summary>
    /// Gets or sets the optional idempotency key.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets or sets the current signal status.
    /// </summary>
    public OrchestrationSignalStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the signal was received.
    /// </summary>
    public DateTimeOffset ReceivedUtc { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the signal was finalized.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; init; }

    /// <summary>
    /// Gets or sets the optional status reason.
    /// </summary>
    public string StatusReason { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    public string CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    public string UpdatedBy { get; init; }
}

/// <summary>
/// Represents the durable status of a persisted timer.
/// </summary>
public enum OrchestrationTimerStatus
{
    /// <summary>
    /// The timer is waiting for consumption.
    /// </summary>
    Pending,

    /// <summary>
    /// The timer has been consumed.
    /// </summary>
    Consumed,

    /// <summary>
    /// The timer was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The timer became obsolete.
    /// </summary>
    Obsolete,
}

/// <summary>
/// Represents a persisted orchestration timer.
/// </summary>
public record OrchestrationTimerRecord
{
    /// <summary>
    /// Gets or sets the timer identifier.
    /// </summary>
    public Guid TimerId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the timer trigger kind.
    /// </summary>
    public string TriggerKind { get; init; }

    /// <summary>
    /// Gets or sets the UTC due time.
    /// </summary>
    public DateTimeOffset DueTimeUtc { get; init; }

    /// <summary>
    /// Gets or sets the optional timer target state.
    /// </summary>
    public string TargetState { get; init; }

    /// <summary>
    /// Gets or sets optional continuation metadata.
    /// </summary>
    public string Continuation { get; init; }

    /// <summary>
    /// Gets or sets the current timer status.
    /// </summary>
    public OrchestrationTimerStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the timer was finalized.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; init; }

    /// <summary>
    /// Gets or sets the optional status reason.
    /// </summary>
    public string StatusReason { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }

    /// <summary>
    /// Gets or sets the creator identifier when available.
    /// </summary>
    public string CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets the updater identifier when available.
    /// </summary>
    public string UpdatedBy { get; init; }
}

/// <summary>
/// Represents a persisted orchestration instance lease.
/// </summary>
public record OrchestrationLease
{
    /// <summary>
    /// Gets or sets the lease identifier.
    /// </summary>
    public Guid LeaseId { get; init; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets or sets the lease owner identity.
    /// </summary>
    public string Owner { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the lease was acquired.
    /// </summary>
    public DateTimeOffset AcquiredUtc { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the lease expires.
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>
/// Represents query criteria for orchestration instance inspection.
/// </summary>
public record OrchestrationInstanceQuery
{
    /// <summary>
    /// Gets or sets the orchestration definition name filter.
    /// </summary>
    public string OrchestrationName { get; init; }

    /// <summary>
    /// Gets or sets the lifecycle status filters. Null or empty means no status filter.
    /// </summary>
    public IReadOnlyList<OrchestrationStatus> Statuses { get; init; } = [];

    /// <summary>
    /// Gets or sets the current state filters. Null or empty means no state filter.
    /// </summary>
    public IReadOnlyList<string> States { get; init; } = [];

    /// <summary>
    /// Gets or sets the correlation identifier filter.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the concurrency key filter.
    /// </summary>
    public string ConcurrencyKey { get; init; }

    /// <summary>
    /// Gets or sets the minimum start timestamp filter.
    /// </summary>
    public DateTimeOffset? StartedFromUtc { get; init; }

    /// <summary>
    /// Gets or sets the maximum start timestamp filter.
    /// </summary>
    public DateTimeOffset? StartedToUtc { get; init; }

    /// <summary>
    /// Gets or sets the minimum completion timestamp filter.
    /// </summary>
    public DateTimeOffset? CompletedFromUtc { get; init; }

    /// <summary>
    /// Gets or sets the maximum completion timestamp filter.
    /// </summary>
    public DateTimeOffset? CompletedToUtc { get; init; }

    /// <summary>
    /// Gets or sets the number of items to skip.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of items to take.
    /// </summary>
    public int Take { get; init; } = 100;
}

/// <summary>
/// Represents a paged orchestration instance query result.
/// </summary>
public record OrchestrationInstanceQueryResult
{
    /// <summary>
    /// Gets or sets the total matching item count before paging.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the paged orchestration snapshots.
    /// </summary>
    public IReadOnlyCollection<OrchestrationInstanceSnapshot> Items { get; init; } = [];
}

/// <summary>
/// Represents purge criteria for retained orchestration data.
/// </summary>
public record OrchestrationPurgeCriteria
{
    /// <summary>
    /// Gets or sets the optional upper age filter.
    /// </summary>
    public DateTimeOffset? OlderThan { get; init; }

    /// <summary>
    /// Gets or sets the optional orchestration status filters.
    /// </summary>
    public IReadOnlyCollection<OrchestrationStatus> Statuses { get; init; } = [];

    /// <summary>
    /// Gets or sets the optional archive-state filter.
    /// </summary>
    public bool? IsArchived { get; init; }
}

/// <summary>
/// Represents the outcome of a purge operation over retained orchestration data.
/// </summary>
public record OrchestrationPurgeResult
{
    /// <summary>
    /// Gets or sets the number of orchestration instances removed.
    /// </summary>
    public int PurgedInstanceCount { get; init; }

    /// <summary>
    /// Gets or sets the number of history entries removed.
    /// </summary>
    public int PurgedHistoryCount { get; init; }

    /// <summary>
    /// Gets or sets the number of signal records removed.
    /// </summary>
    public int PurgedSignalCount { get; init; }

    /// <summary>
    /// Gets or sets the number of timer records removed.
    /// </summary>
    public int PurgedTimerCount { get; init; }
}

/// <summary>
/// Represents basic metrics derived from persisted orchestration state.
/// </summary>
public record OrchestrationMetricsSnapshot
{
    /// <summary>
    /// Gets or sets the total persisted instance count.
    /// </summary>
    public int TotalInstances { get; init; }

    /// <summary>
    /// Gets or sets the running instance count.
    /// </summary>
    public int RunningInstances { get; init; }

    /// <summary>
    /// Gets or sets the waiting instance count.
    /// </summary>
    public int WaitingInstances { get; init; }

    /// <summary>
    /// Gets or sets the paused instance count.
    /// </summary>
    public int PausedInstances { get; init; }

    /// <summary>
    /// Gets or sets the completed instance count.
    /// </summary>
    public int CompletedInstances { get; init; }

    /// <summary>
    /// Gets or sets the cancelled instance count.
    /// </summary>
    public int CancelledInstances { get; init; }

    /// <summary>
    /// Gets or sets the failed instance count.
    /// </summary>
    public int FailedInstances { get; init; }

    /// <summary>
    /// Gets or sets the terminated instance count.
    /// </summary>
    public int TerminatedInstances { get; init; }

    /// <summary>
    /// Gets or sets the oldest waiting instance start timestamp when available.
    /// </summary>
    public DateTimeOffset? OldestWaitingStartedUtc { get; init; }

    /// <summary>
    /// Gets or sets persisted instance counts grouped by orchestration definition name.
    /// </summary>
    public IReadOnlyDictionary<string, int> InstanceCountsByOrchestration { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets the total durable history record count.
    /// </summary>
    public int HistoryCount { get; init; }

    /// <summary>
    /// Gets or sets the total persisted signal count.
    /// </summary>
    public int SignalCount { get; init; }

    /// <summary>
    /// Gets or sets the total persisted timer count.
    /// </summary>
    public int TimerCount { get; init; }
}