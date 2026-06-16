// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the public persisted-state query surface for Jobs dashboards, APIs, and support tooling.
/// </summary>
public interface IJobSchedulerQueryService
{
    Task<ResultPaged<JobSchedulerJobModel>> QueryJobsAsync(JobSchedulerJobQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerTriggerModel>> QueryTriggersAsync(JobSchedulerTriggerQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerRecurringTriggerModel>> QueryRecurringTriggersAsync(JobSchedulerRecurringTriggerQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerOccurrenceModel>> QueryOccurrencesAsync(JobSchedulerOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerRetryModel>> QueryRetriesAsync(JobSchedulerRetryQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerBatchModel>> QueryBatchesAsync(JobSchedulerBatchQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerBatchChildOccurrenceModel>> QueryBatchOccurrencesAsync(string batchId, JobSchedulerBatchOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerBatchHistoryModel>> QueryBatchHistoryAsync(string batchId, JobSchedulerBatchHistoryQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerDependencyModel>> QueryDependenciesAsync(JobSchedulerDependencyQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerExecutionModel>> QueryExecutionsAsync(JobSchedulerExecutionQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerExecutionHistoryModel>> QueryExecutionHistoryAsync(JobSchedulerExecutionHistoryQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerLeaseModel>> QueryLeasesAsync(JobSchedulerLeaseQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<ResultPaged<JobSchedulerServerModel>> QueryServersAsync(JobSchedulerServerQueryRequest request = null, CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerMetricsModel>> GetMetricsAsync(JobSchedulerMetricsRequest request = null, CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerDashboardSummaryModel>> GetDashboardSummaryAsync(JobSchedulerDashboardSummaryRequest request = null, CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerDashboardNavigationModel>> GetDashboardNavigationAsync(CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerDashboardOverviewModel>> GetDashboardOverviewAsync(CancellationToken cancellationToken = default);

    Task<Result<JobSchedulerTimelineModel>> GetDashboardTimelineAsync(JobSchedulerTimelineRequest request = null, CancellationToken cancellationToken = default);
}

public abstract class JobSchedulerPagedQueryRequest
{
    public int Skip { get; set; }

    public int Take { get; set; } = 50;

    public string SortBy { get; set; }

    public bool SortDescending { get; set; } = true;
}

public sealed class JobSchedulerJobQueryRequest : JobSchedulerPagedQueryRequest
{
    public string JobName { get; set; }

    public string Group { get; set; }

    public string Module { get; set; }

    public bool? Enabled { get; set; }

    public bool? Paused { get; set; }

    public bool IncludeOrphanedRuntimeState { get; set; }
}

public class JobSchedulerTriggerQueryRequest : JobSchedulerPagedQueryRequest
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public IReadOnlyList<JobTriggerType> TriggerTypes { get; set; }

    public bool? Enabled { get; set; }

    public bool? Paused { get; set; }
}

public sealed class JobSchedulerRecurringTriggerQueryRequest : JobSchedulerTriggerQueryRequest
{
}

public sealed class JobSchedulerOccurrenceQueryRequest : JobSchedulerPagedQueryRequest
{
    public Guid? OccurrenceId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? StartedFrom { get; set; }

    public DateTimeOffset? StartedTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public DateTimeOffset? DueFromUtc
    {
        get => this.DueFrom;
        set => this.DueFrom = value;
    }

    public DateTimeOffset? DueToUtc
    {
        get => this.DueTo;
        set => this.DueTo = value;
    }

    public DateTimeOffset? StartedFromUtc
    {
        get => this.StartedFrom;
        set => this.StartedFrom = value;
    }

    public DateTimeOffset? StartedToUtc
    {
        get => this.StartedTo;
        set => this.StartedTo = value;
    }

    public DateTimeOffset? CompletedFromUtc
    {
        get => this.CompletedFrom;
        set => this.CompletedFrom = value;
    }

    public DateTimeOffset? CompletedToUtc
    {
        get => this.CompletedTo;
        set => this.CompletedTo = value;
    }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }
}

public sealed class JobSchedulerRetryQueryRequest : JobSchedulerPagedQueryRequest
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public string CorrelationId { get; set; }

    public string SchedulerInstanceId { get; set; }

    public bool? HasRemainingAttempts { get; set; }
}

public sealed class JobSchedulerBatchQueryRequest : JobSchedulerPagedQueryRequest
{
    public string BatchId { get; set; }

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public IReadOnlyList<JobBatchStatus> Statuses { get; set; }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }
}

public sealed class JobSchedulerBatchOccurrenceQueryRequest : JobSchedulerPagedQueryRequest
{
    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }
}

public sealed class JobSchedulerBatchHistoryQueryRequest : JobSchedulerPagedQueryRequest
{
    public string EventName { get; set; }

    public IReadOnlyList<JobBatchStatus> BatchStatuses { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? RecordedFromUtc { get; set; }

    public DateTimeOffset? RecordedToUtc { get; set; }
}

public sealed class JobSchedulerDependencyQueryRequest : JobSchedulerPagedQueryRequest
{
    public Guid? DependencyId { get; set; }

    public Guid? OccurrenceId { get; set; }

    public Guid? DependentOccurrenceId { get; set; }

    public Guid? PrerequisiteOccurrenceId { get; set; }

    public IReadOnlyList<JobDependencyStatus> Statuses { get; set; }

    public IReadOnlyList<JobDependencyFailurePolicy> FailurePolicies { get; set; }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }
}

public sealed class JobSchedulerExecutionQueryRequest : JobSchedulerPagedQueryRequest
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public IReadOnlyList<JobExecutionStatus> Statuses { get; set; }

    public string SchedulerInstanceId { get; set; }

    public string CorrelationId { get; set; }

    public string IdempotencyKey { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? StartedFrom { get; set; }

    public DateTimeOffset? StartedTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public DateTimeOffset? StartedFromUtc
    {
        get => this.StartedFrom;
        set => this.StartedFrom = value;
    }

    public DateTimeOffset? StartedToUtc
    {
        get => this.StartedTo;
        set => this.StartedTo = value;
    }

    public DateTimeOffset? CompletedFromUtc
    {
        get => this.CompletedFrom;
        set => this.CompletedFrom = value;
    }

    public DateTimeOffset? CompletedToUtc
    {
        get => this.CompletedTo;
        set => this.CompletedTo = value;
    }
}

public sealed class JobSchedulerExecutionHistoryQueryRequest : JobSchedulerPagedQueryRequest
{
    public Guid? OccurrenceId { get; set; }

    public Guid? ExecutionId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public string SchedulerInstanceId { get; set; }

    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }

    public IReadOnlyList<string> EventNames { get; set; }

    public DateTimeOffset? RecordedFromUtc { get; set; }

    public DateTimeOffset? RecordedToUtc { get; set; }
}

public sealed class JobSchedulerLeaseQueryRequest : JobSchedulerPagedQueryRequest
{
    public string SchedulerInstanceId { get; set; }

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public IReadOnlyList<JobSchedulerLeaseStatus> Statuses { get; set; }

    public DateTimeOffset? ExpiresFromUtc { get; set; }

    public DateTimeOffset? ExpiresToUtc { get; set; }
}

public sealed class JobSchedulerServerQueryRequest : JobSchedulerPagedQueryRequest
{
    public string SchedulerInstanceId { get; set; }

    public IReadOnlyList<JobSchedulerServerStatus> Statuses { get; set; }
}

public sealed class JobSchedulerMetricsRequest
{
    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public JobTriggerType? TriggerType { get; set; }

    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? DueFrom { get; set; }

    public DateTimeOffset? DueTo { get; set; }

    public DateTimeOffset? CompletedFrom { get; set; }

    public DateTimeOffset? CompletedTo { get; set; }

    public DateTimeOffset? FromUtc
    {
        get => this.DueFrom;
        set => this.DueFrom = value;
    }

    public DateTimeOffset? ToUtc
    {
        get => this.DueTo;
        set => this.DueTo = value;
    }
}

public sealed class JobSchedulerDashboardSummaryRequest
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public DateTimeOffset? FromUtc
    {
        get => this.From;
        set => this.From = value;
    }

    public DateTimeOffset? ToUtc
    {
        get => this.To;
        set => this.To = value;
    }
}

public sealed class JobSchedulerTimelineRequest
{
    public JobSchedulerTimelineMode Mode { get; set; } = JobSchedulerTimelineMode.Occurrences;

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public string SchedulerInstanceId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public int Bucket { get; set; } = 60;

    public DateTimeOffset? FromUtc
    {
        get => this.From;
        set => this.From = value;
    }

    public DateTimeOffset? ToUtc
    {
        get => this.To;
        set => this.To = value;
    }

    public int BucketMinutes
    {
        get => this.Bucket;
        set => this.Bucket = value;
    }

    public IReadOnlyList<string> Statuses { get; set; }

    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }
}

public enum JobSchedulerLeaseStatus
{
    Active = 0,
    Expired = 1,
}

public enum JobSchedulerServerStatus
{
    Active = 0,
    Expired = 1,
    Observed = 2,
}

public enum JobSchedulerTimelineMode
{
    Occurrences = 0,
    Executions = 1,
}

public sealed class JobSchedulerQueryCapabilities
{
    public bool SupportsLeaseDiagnostics { get; init; } = true;

    public bool SupportsServerDiagnostics { get; init; } = true;

    public bool SupportsTimeline { get; init; } = true;

    public bool SupportsRuntimeOverlay { get; init; } = true;
}

public sealed class JobSchedulerJobModel
{
    public string JobName { get; init; }

    public string DisplayName { get; init; }

    public string Description { get; init; }

    public string Group { get; init; }

    public string Module { get; init; }

    public string JobType { get; init; }

    public bool RegisteredEnabled { get; init; }

    public bool EffectiveEnabled { get; init; }

    public bool Paused { get; init; }

    public bool IsOrphanedRuntimeState { get; init; }

    public bool HasOrphanedRuntimeState { get; init; }

    public int Priority { get; init; }

    public TimeSpan? Timeout { get; init; }

    public int? ConcurrencyLimit { get; init; }

    public int TriggerCount { get; init; }

    public int RecurringTriggerCount { get; init; }

    public int PendingOccurrenceCount { get; init; }

    public int RunningOccurrenceCount { get; init; }

    public int FailedOccurrenceCount { get; init; }

    public DateTimeOffset? LastOccurrenceUtc { get; init; }

    public DateTimeOffset? LastExecutionUtc { get; init; }

    public JobExecutionStatus? LastExecutionStatus { get; init; }

    public bool HasFailedLatestExecution { get; init; }

    public string DataType { get; init; }

    public IReadOnlyList<string> TargetInstances { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }
}

public class JobSchedulerTriggerModel
{
    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public JobTriggerType TriggerType { get; init; }

    public bool RegisteredEnabled { get; init; }

    public bool EffectiveEnabled { get; init; }

    public bool Paused { get; init; }

    public int? Priority { get; init; }

    public TimeSpan? Timeout { get; init; }

    public int? RetryMaxAttempts { get; init; }

    public bool RetryUsesExponentialBackoff { get; init; }

    public string Schedule { get; init; }

    public DateTimeOffset? DueUtc { get; init; }

    public TimeSpan? Delay { get; init; }

    public DateTimeOffset? NextDueUtc { get; init; }

    public DateTimeOffset? LastMaterializedScheduledUtc { get; init; }

    public bool HasMaterializedOccurrence { get; init; }

    public string TimeZoneId { get; init; }

    public string DataType { get; init; }

    public IReadOnlyList<string> TargetInstances { get; init; }

    public string DataPreview { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }

    public DateTimeOffset? LastOccurrenceUtc { get; init; }

    public JobOccurrenceStatus? LastOccurrenceStatus { get; init; }
}

public sealed class JobSchedulerRecurringTriggerModel : JobSchedulerTriggerModel
{
}

public class JobSchedulerOccurrenceModel
{
    public Guid OccurrenceId { get; init; }

    public string OccurrenceKey { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public JobTriggerType TriggerType { get; init; }

    public JobOccurrenceStatus Status { get; init; }

    public DateTimeOffset DueUtc { get; init; }

    public DateTimeOffset? ScheduledUtc { get; init; }

    public DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset UpdatedDate { get; init; }

    public string CorrelationId { get; init; }

    public string CausationId { get; init; }

    public string IdempotencyKey { get; init; }

    public JobOccurrenceStatus? ResumeStatus { get; init; }

    public string BlockedReason { get; init; }

    public int DependencyCount { get; init; }

    public int PendingDependencyCount { get; init; }

    public int FailedDependencyCount { get; init; }

    public string DataType { get; init; }

    public string DataPreview { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }

    /// <summary>
    /// Gets the occurrence properties as display-ready key/value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    public int AttemptCount { get; init; }

    public JobExecutionStatus? LatestExecutionStatus { get; init; }

    public DateTimeOffset? LatestExecutionStartedUtc { get; init; }

    public DateTimeOffset? LatestExecutionCompletedUtc { get; init; }

    public double? LatestExecutionDurationSeconds { get; init; }

    /// <summary>
    /// Gets the persisted execution messages for the occurrence, newest attempt first.
    /// </summary>
    public IReadOnlyList<string> ExecutionMessages { get; init; } = [];

    public string LeaseOwnerSchedulerInstanceId { get; init; }

    public Guid? BatchInternalId { get; init; }

    public string ExternalBatchId { get; init; }
}

public sealed class JobSchedulerRetryModel
{
    public Guid OccurrenceId { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public string CorrelationId { get; init; }

    public JobOccurrenceStatus OccurrenceStatus { get; init; }

    public int AttemptCount { get; init; }

    public int MaxAttempts { get; init; }

    public bool HasRemainingAttempts { get; init; }

    public int NextAttemptNumber { get; init; }

    public DateTimeOffset RetryDueUtc { get; init; }

    public string LastFailureMessage { get; init; }

    public string SchedulerInstanceId { get; init; }
}

public sealed class JobSchedulerBatchModel
{
    public Guid BatchId { get; init; }

    public string ExternalBatchId { get; init; }

    public string Description { get; init; }

    public JobBatchStatus Status { get; init; }

    public JobBatchCompletionPolicy CompletionPolicy { get; init; }

    public string CorrelationId { get; init; }

    public string CausationId { get; init; }

    public string IdempotencyKey { get; init; }

    public int AcceptedCount { get; init; }

    public int SucceededCount { get; init; }

    public int FailedCount { get; init; }

    public int CancelledCount { get; init; }

    public int ArchivedCount { get; init; }

    public int ChildOccurrenceCount { get; init; }

    public DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset UpdatedDate { get; init; }

    public DateTimeOffset? CompletedDate { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }
}

public sealed class JobSchedulerBatchChildOccurrenceModel : JobSchedulerOccurrenceModel
{
    public Guid BatchId { get; init; }

    public int? Sequence { get; init; }

    public JobOccurrenceStatus ChildStatus { get; init; }
}

public sealed class JobSchedulerBatchHistoryModel
{
    public Guid HistoryId { get; init; }

    public Guid BatchId { get; init; }

    public string ExternalBatchId { get; init; }

    public string EventName { get; init; }

    public JobBatchStatus? BatchStatus { get; init; }

    public string Message { get; init; }

    public string SchedulerInstanceId { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class JobSchedulerDependencyModel
{
    public Guid DependencyId { get; init; }

    public Guid DependentOccurrenceId { get; init; }

    public string DependentJobName { get; init; }

    public string DependentTriggerName { get; init; }

    public JobOccurrenceStatus? DependentStatus { get; init; }

    public Guid PrerequisiteOccurrenceId { get; init; }

    public string PrerequisiteJobName { get; init; }

    public string PrerequisiteTriggerName { get; init; }

    public JobOccurrenceStatus? PrerequisiteStatus { get; init; }

    public IReadOnlyList<JobOccurrenceStatus> RequiredStatuses { get; init; }

    public JobDependencyStatus Status { get; init; }

    public JobDependencyFailurePolicy FailurePolicy { get; init; }

    public string Reason { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }

    public DateTimeOffset CreatedDate { get; init; }

    public DateTimeOffset UpdatedDate { get; init; }
}

public sealed class JobSchedulerExecutionModel
{
    public Guid ExecutionId { get; init; }

    public Guid OccurrenceId { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public int AttemptNumber { get; init; }

    public JobExecutionStatus Status { get; init; }

    public string SchedulerInstanceId { get; init; }

    public DateTimeOffset StartedUtc { get; init; }

    public DateTimeOffset? CompletedUtc { get; init; }

    public double? DurationSeconds { get; init; }

    public string Message { get; init; }

    public string CorrelationId { get; init; }

    public string IdempotencyKey { get; init; }
}

public sealed class JobSchedulerExecutionHistoryModel
{
    public Guid HistoryId { get; init; }

    public Guid OccurrenceId { get; init; }

    public Guid? ExecutionId { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public string SchedulerInstanceId { get; init; }

    public string EventName { get; init; }

    public JobOccurrenceStatus? OccurrenceStatus { get; init; }

    public JobExecutionStatus? ExecutionStatus { get; init; }

    public string Message { get; init; }

    public DateTimeOffset RecordedAt { get; init; }

    public string RecordedBy { get; init; }

    public IReadOnlyList<string> PropertyKeys { get; init; }

    public int PropertyCount { get; init; }
}

public sealed class JobSchedulerLeaseModel
{
    public Guid OccurrenceId { get; init; }

    public string JobName { get; init; }

    public string TriggerName { get; init; }

    public string SchedulerInstanceId { get; init; }

    public JobSchedulerLeaseStatus Status { get; init; }

    public DateTimeOffset AcquiredUtc { get; init; }

    public DateTimeOffset? RenewedUtc { get; init; }

    public DateTimeOffset ExpiresUtc { get; init; }

    public int RenewalCount { get; init; }
}

public sealed class JobSchedulerServerModel
{
    public string SchedulerInstanceId { get; init; }

    public JobSchedulerServerStatus Status { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public int ActiveLeaseCount { get; init; }

    public int ExpiredLeaseCount { get; init; }

    public int ExecutionCount { get; init; }

    public DateTimeOffset? LastExecutionUtc { get; init; }

    public DateTimeOffset? LastHistoryUtc { get; init; }
}

public sealed class JobSchedulerMetricsModel
{
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    public int RegisteredJobCount { get; init; }

    public int RegisteredTriggerCount { get; init; }

    public long OccurrenceCount { get; init; }

    public long ExecutionCount { get; init; }

    public long BatchCount { get; init; }

    public long ActiveLeaseCount { get; init; }

    public long ExpiredLeaseCount { get; init; }

    public long ActiveServerCount { get; init; }

    public long RetryScheduledCount { get; init; }

    public double? AverageExecutionDurationSeconds { get; init; }

    public IReadOnlyDictionary<JobOccurrenceStatus, long> OccurrenceCountsByStatus { get; init; }

    public IReadOnlyDictionary<JobExecutionStatus, long> ExecutionCountsByStatus { get; init; }

    public IReadOnlyDictionary<string, long> CountsByJob { get; init; }
}

public sealed class JobSchedulerDashboardSummaryModel
{
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    public int EnabledJobCount { get; init; }

    public int PausedJobCount { get; init; }

    public int EnabledTriggerCount { get; init; }

    public long DueOccurrenceCount { get; init; }

    public long RunningOccurrenceCount { get; init; }

    public long FailedOccurrenceCount { get; init; }

    public long RetryScheduledCount { get; init; }

    public long ActiveLeaseCount { get; init; }

    public long ProcessingBatchCount { get; init; }

    public long ActiveServerCount { get; init; }

    public DateTimeOffset? OldestDueOccurrenceUtc { get; init; }
}

public sealed class JobSchedulerJobFacetCountsModel
{
    public long EnabledCount { get; init; }

    public long DisabledCount { get; init; }

    public long PausedCount { get; init; }

    public long OrphanedRuntimeStateCount { get; init; }

    public long FailedLatestExecutionCount { get; init; }
}

public sealed class JobSchedulerDashboardNavigationModel
{
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    public IReadOnlyList<JobSchedulerDashboardNavigationLinkModel> Links { get; init; }
}

public sealed class JobSchedulerDashboardNavigationLinkModel
{
    public string Key { get; init; }

    public string Title { get; init; }

    public string Route { get; init; }

    public long Count { get; init; }
}

public sealed class JobSchedulerDashboardOverviewModel
{
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    public int EnabledTriggerCount { get; init; }

    public long DueOccurrenceCount { get; init; }

    public long RunningOccurrenceCount { get; init; }

    public long FailedOccurrenceCount { get; init; }

    public long RetryScheduledCount { get; init; }

    public long ActiveLeaseCount { get; init; }

    public long ProcessingBatchCount { get; init; }

    public long ActiveServerCount { get; init; }

    public DateTimeOffset? OldestDueOccurrenceUtc { get; init; }
}

public sealed class JobSchedulerTimelineModel
{
    public JobSchedulerTimelineMode Mode { get; init; }

    public DateTimeOffset FromUtc { get; init; }

    public DateTimeOffset ToUtc { get; init; }

    public int BucketMinutes { get; init; }

    public IReadOnlyList<JobSchedulerTimelineBucketModel> Buckets { get; init; }
}

public sealed class JobSchedulerTimelineBucketModel
{
    public DateTimeOffset BucketStartUtc { get; init; }

    public DateTimeOffset BucketEndUtc { get; init; }

    public IReadOnlyDictionary<string, long> CountsByStatus { get; init; }
}
