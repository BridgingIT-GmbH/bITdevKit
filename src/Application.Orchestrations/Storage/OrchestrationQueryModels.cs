// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the public persisted-state query surface for orchestration operations, dashboards, and support tooling.
/// </summary>
/// <remarks>
/// All returned data is derived from durable orchestration state rather than worker-local runtime memory.
/// </remarks>
public interface IOrchestrationQueryService
{
    /// <summary>
    /// Loads the current persisted orchestration instance summary.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted orchestration instance model when found.</returns>
    Task<Result<OrchestrationInstanceModel>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the latest persisted orchestration context snapshot.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted context snapshot model when found.</returns>
    Task<Result<OrchestrationContextSnapshotModel>> GetContextAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries persisted orchestration instances using dashboard-oriented filters, sorting, and paging.
    /// </summary>
    /// <param name="request">The optional query request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The paged orchestration instance result.</returns>
    Task<ResultPaged<OrchestrationInstanceModel>> QueryAsync(OrchestrationQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted execution history for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted history models.</returns>
    Task<Result<IReadOnlyList<OrchestrationHistoryModel>>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted signal records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted signal models.</returns>
    Task<Result<IReadOnlyList<OrchestrationSignalModel>>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted timer records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted timer models.</returns>
    Task<Result<IReadOnlyList<OrchestrationTimerModel>>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates dashboard-ready orchestration metrics from persisted state.
    /// </summary>
    /// <param name="request">The optional metrics request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated metrics model.</returns>
    Task<Result<OrchestrationMetricsModel>> GetMetricsAsync(OrchestrationMetricsRequest request = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the request used to query persisted orchestration instances.
/// </summary>
public class OrchestrationQueryRequest
{
    /// <summary>
    /// Gets or sets the orchestration definition name filter.
    /// </summary>
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status filters.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the current business state filters.
    /// </summary>
    public IReadOnlyList<string> States { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier filter.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency key filter.
    /// </summary>
    public string ConcurrencyKey { get; set; }

    /// <summary>
    /// Gets or sets the inclusive lower bound for orchestration start timestamps.
    /// </summary>
    public DateTimeOffset? StartedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound for orchestration start timestamps.
    /// </summary>
    public DateTimeOffset? StartedTo { get; set; }

    /// <summary>
    /// Gets or sets the inclusive lower bound for orchestration completion timestamps.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound for orchestration completion timestamps.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }

    /// <summary>
    /// Gets or sets the number of items to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the number of items to take.
    /// </summary>
    public int Take { get; set; } = 50;

    /// <summary>
    /// Gets or sets the property used for sorting.
    /// </summary>
    public string SortBy { get; set; } = "StartedUtc";

    /// <summary>
    /// Gets or sets a value indicating whether sort order is descending.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Represents the request used to aggregate orchestration metrics from persisted state.
/// </summary>
public class OrchestrationMetricsRequest
{
    /// <summary>
    /// Gets or sets the orchestration definition name filter.
    /// </summary>
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status filters.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the current business state filters.
    /// </summary>
    public IReadOnlyList<string> States { get; set; }

    /// <summary>
    /// Gets or sets the inclusive lower bound for orchestration start timestamps.
    /// </summary>
    public DateTimeOffset? StartedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound for orchestration start timestamps.
    /// </summary>
    public DateTimeOffset? StartedTo { get; set; }

    /// <summary>
    /// Gets or sets the inclusive lower bound for orchestration completion timestamps.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound for orchestration completion timestamps.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }
}

/// <summary>
/// Represents a dashboard-ready orchestration instance summary.
/// </summary>
public class OrchestrationInstanceModel
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the current business state.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the current activity name.
    /// </summary>
    public string CurrentActivity { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the concurrency key.
    /// </summary>
    public string ConcurrencyKey { get; set; }

    /// <summary>
    /// Gets or sets the creator identifier.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier.
    /// </summary>
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the orchestration start timestamp.
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the orchestration completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the effective last-updated timestamp used by operational dashboards.
    /// </summary>
    public DateTimeOffset LastUpdatedUtc { get; set; }
}

/// <summary>
/// Represents the latest persisted orchestration context snapshot.
/// </summary>
public class OrchestrationContextSnapshotModel
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the current business state.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the snapshot was last updated.
    /// </summary>
    public DateTimeOffset SnapshotUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialized orchestration context type name.
    /// </summary>
    public string ContextType { get; set; }

    /// <summary>
    /// Gets or sets the serialized persisted context payload.
    /// </summary>
    public string ContextJson { get; set; }
}

/// <summary>
/// Represents a dashboard-ready orchestration history entry.
/// </summary>
public class OrchestrationHistoryModel
{
    /// <summary>
    /// Gets or sets the history entry identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the actor that created the history entry.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the associated business state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the associated activity name.
    /// </summary>
    public string Activity { get; set; }

    /// <summary>
    /// Gets or sets the human-readable event message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the JSON event payload when the persisted details are structured data.
    /// </summary>
    public string DataJson { get; set; }
}

/// <summary>
/// Represents a dashboard-ready persisted signal record.
/// </summary>
public class OrchestrationSignalModel
{
    /// <summary>
    /// Gets or sets the signal identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the signal name.
    /// </summary>
    public string SignalName { get; set; }

    /// <summary>
    /// Gets or sets the persisted processing status.
    /// </summary>
    public string ProcessingStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the creator identifier.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier.
    /// </summary>
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the signal was received.
    /// </summary>
    public DateTimeOffset ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the signal was processed.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialized persisted payload.
    /// </summary>
    public string PayloadJson { get; set; }
}

/// <summary>
/// Represents a dashboard-ready persisted timer record.
/// </summary>
public class OrchestrationTimerModel
{
    /// <summary>
    /// Gets or sets the timer identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the timer kind.
    /// </summary>
    public string TimerKind { get; set; }

    /// <summary>
    /// Gets or sets the persisted processing status.
    /// </summary>
    public string ProcessingStatus { get; set; }

    /// <summary>
    /// Gets or sets the creator identifier.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updater identifier.
    /// </summary>
    public string UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the due timestamp.
    /// </summary>
    public DateTimeOffset DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the processed timestamp.
    /// </summary>
    public DateTimeOffset? ProcessedUtc { get; set; }

    /// <summary>
    /// Gets or sets the serialized timer metadata payload.
    /// </summary>
    public string MetadataJson { get; set; }
}

/// <summary>
/// Represents aggregated dashboard metrics derived from persisted orchestration state.
/// </summary>
public class OrchestrationMetricsModel
{
    /// <summary>
    /// Gets or sets the total matching orchestration count.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the running orchestration count.
    /// </summary>
    public long RunningCount { get; set; }

    /// <summary>
    /// Gets or sets the waiting orchestration count.
    /// </summary>
    public long WaitingCount { get; set; }

    /// <summary>
    /// Gets or sets the paused orchestration count.
    /// </summary>
    public long PausedCount { get; set; }

    /// <summary>
    /// Gets or sets the completed orchestration count.
    /// </summary>
    public long CompletedCount { get; set; }

    /// <summary>
    /// Gets or sets the failed orchestration count.
    /// </summary>
    public long FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the cancelled orchestration count.
    /// </summary>
    public long CancelledCount { get; set; }

    /// <summary>
    /// Gets or sets the terminated orchestration count.
    /// </summary>
    public long TerminatedCount { get; set; }

    /// <summary>
    /// Gets or sets the average terminal duration in seconds when available.
    /// </summary>
    public double? AverageDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the oldest waiting orchestration start timestamp when available.
    /// </summary>
    public DateTimeOffset? OldestWaitingStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets counts grouped by orchestration definition.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountsByOrchestration { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets counts grouped by current state.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountsByState { get; set; } = new Dictionary<string, long>();
}