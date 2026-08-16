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
    /// <summary>
    /// Executes the query jobs operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerJobModel>> QueryJobsAsync(JobSchedulerJobQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query triggers operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerTriggerModel>> QueryTriggersAsync(JobSchedulerTriggerQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query recurring triggers operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerRecurringTriggerModel>> QueryRecurringTriggersAsync(JobSchedulerRecurringTriggerQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query occurrences operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerOccurrenceModel>> QueryOccurrencesAsync(JobSchedulerOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query retries operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerRetryModel>> QueryRetriesAsync(JobSchedulerRetryQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query batches operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerBatchModel>> QueryBatchesAsync(JobSchedulerBatchQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query batch occurrences operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerBatchChildOccurrenceModel>> QueryBatchOccurrencesAsync(string batchId, JobSchedulerBatchOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query batch history operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerBatchHistoryModel>> QueryBatchHistoryAsync(string batchId, JobSchedulerBatchHistoryQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query dependencies operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerDependencyModel>> QueryDependenciesAsync(JobSchedulerDependencyQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query executions operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerExecutionModel>> QueryExecutionsAsync(JobSchedulerExecutionQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query execution history operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerExecutionHistoryModel>> QueryExecutionHistoryAsync(JobSchedulerExecutionHistoryQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query leases operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerLeaseModel>> QueryLeasesAsync(JobSchedulerLeaseQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query servers operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<ResultPaged<JobSchedulerServerModel>> QueryServersAsync(JobSchedulerServerQueryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result<JobSchedulerMetricsModel>> GetMetricsAsync(JobSchedulerMetricsRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dashboard summary.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result<JobSchedulerDashboardSummaryModel>> GetDashboardSummaryAsync(JobSchedulerDashboardSummaryRequest request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dashboard navigation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result<JobSchedulerDashboardNavigationModel>> GetDashboardNavigationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dashboard overview.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result<JobSchedulerDashboardOverviewModel>> GetDashboardOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dashboard timeline.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result<JobSchedulerTimelineModel>> GetDashboardTimelineAsync(JobSchedulerTimelineRequest request = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents job scheduler paged query request.
/// </summary>
public abstract class JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the take.
    /// </summary>
    public int Take { get; set; } = 50;

    /// <summary>
    /// Gets or sets the sort by.
    /// </summary>
    public string SortBy { get; set; }

    /// <summary>
    /// Gets or sets the sort descending.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Represents job scheduler job query request.
/// </summary>
public sealed class JobSchedulerJobQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the module.
    /// </summary>
    public string Module { get; set; }

    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    public bool? Paused { get; set; }

    /// <summary>
    /// Gets or sets the include orphaned runtime state.
    /// </summary>
    public bool IncludeOrphanedRuntimeState { get; set; }
}

/// <summary>
/// Represents job scheduler trigger query request.
/// </summary>
public class JobSchedulerTriggerQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger types.
    /// </summary>
    public IReadOnlyList<JobTriggerType> TriggerTypes { get; set; }

    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    public bool? Paused { get; set; }
}

/// <summary>
/// Represents job scheduler recurring trigger query request.
/// </summary>
public sealed class JobSchedulerRecurringTriggerQueryRequest : JobSchedulerTriggerQueryRequest
{
}

/// <summary>
/// Represents job scheduler occurrence query request.
/// </summary>
public sealed class JobSchedulerOccurrenceQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid? OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType? TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the due from.
    /// </summary>
    public DateTimeOffset? DueFrom { get; set; }

    /// <summary>
    /// Gets or sets the due to.
    /// </summary>
    public DateTimeOffset? DueTo { get; set; }

    /// <summary>
    /// Gets or sets the started from.
    /// </summary>
    public DateTimeOffset? StartedFrom { get; set; }

    /// <summary>
    /// Gets or sets the started to.
    /// </summary>
    public DateTimeOffset? StartedTo { get; set; }

    /// <summary>
    /// Gets or sets the completed from.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the completed to.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }

    /// <summary>
    /// Stores the due from utc.
    /// </summary>
    public DateTimeOffset? DueFromUtc
    {
        get => this.DueFrom;
        set => this.DueFrom = value;
    }

    /// <summary>
    /// Stores the due to utc.
    /// </summary>
    public DateTimeOffset? DueToUtc
    {
        get => this.DueTo;
        set => this.DueTo = value;
    }

    /// <summary>
    /// Stores the started from utc.
    /// </summary>
    public DateTimeOffset? StartedFromUtc
    {
        get => this.StartedFrom;
        set => this.StartedFrom = value;
    }

    /// <summary>
    /// Stores the started to utc.
    /// </summary>
    public DateTimeOffset? StartedToUtc
    {
        get => this.StartedTo;
        set => this.StartedTo = value;
    }

    /// <summary>
    /// Stores the completed from utc.
    /// </summary>
    public DateTimeOffset? CompletedFromUtc
    {
        get => this.CompletedFrom;
        set => this.CompletedFrom = value;
    }

    /// <summary>
    /// Stores the completed to utc.
    /// </summary>
    public DateTimeOffset? CompletedToUtc
    {
        get => this.CompletedTo;
        set => this.CompletedTo = value;
    }

    /// <summary>
    /// Gets or sets the created from utc.
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the created to utc.
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler retry query request.
/// </summary>
public sealed class JobSchedulerRetryQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the has remaining attempts.
    /// </summary>
    public bool? HasRemainingAttempts { get; set; }
}

/// <summary>
/// Represents job scheduler batch query request.
/// </summary>
public sealed class JobSchedulerBatchQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobBatchStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the created from utc.
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the created to utc.
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler batch occurrence query request.
/// </summary>
public sealed class JobSchedulerBatchOccurrenceQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }
}

/// <summary>
/// Represents job scheduler batch history query request.
/// </summary>
public sealed class JobSchedulerBatchHistoryQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the batch statuses.
    /// </summary>
    public IReadOnlyList<JobBatchStatus> BatchStatuses { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the recorded from utc.
    /// </summary>
    public DateTimeOffset? RecordedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the recorded to utc.
    /// </summary>
    public DateTimeOffset? RecordedToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler dependency query request.
/// </summary>
public sealed class JobSchedulerDependencyQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the dependency id.
    /// </summary>
    public Guid? DependencyId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid? OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the dependent occurrence id.
    /// </summary>
    public Guid? DependentOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the prerequisite occurrence id.
    /// </summary>
    public Guid? PrerequisiteOccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobDependencyStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the failure policies.
    /// </summary>
    public IReadOnlyList<JobDependencyFailurePolicy> FailurePolicies { get; set; }

    /// <summary>
    /// Gets or sets the created from utc.
    /// </summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the created to utc.
    /// </summary>
    public DateTimeOffset? CreatedToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler execution query request.
/// </summary>
public sealed class JobSchedulerExecutionQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType? TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobExecutionStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the due from.
    /// </summary>
    public DateTimeOffset? DueFrom { get; set; }

    /// <summary>
    /// Gets or sets the due to.
    /// </summary>
    public DateTimeOffset? DueTo { get; set; }

    /// <summary>
    /// Gets or sets the started from.
    /// </summary>
    public DateTimeOffset? StartedFrom { get; set; }

    /// <summary>
    /// Gets or sets the started to.
    /// </summary>
    public DateTimeOffset? StartedTo { get; set; }

    /// <summary>
    /// Gets or sets the completed from.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the completed to.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }

    /// <summary>
    /// Stores the started from utc.
    /// </summary>
    public DateTimeOffset? StartedFromUtc
    {
        get => this.StartedFrom;
        set => this.StartedFrom = value;
    }

    /// <summary>
    /// Stores the started to utc.
    /// </summary>
    public DateTimeOffset? StartedToUtc
    {
        get => this.StartedTo;
        set => this.StartedTo = value;
    }

    /// <summary>
    /// Stores the completed from utc.
    /// </summary>
    public DateTimeOffset? CompletedFromUtc
    {
        get => this.CompletedFrom;
        set => this.CompletedFrom = value;
    }

    /// <summary>
    /// Stores the completed to utc.
    /// </summary>
    public DateTimeOffset? CompletedToUtc
    {
        get => this.CompletedTo;
        set => this.CompletedTo = value;
    }
}

/// <summary>
/// Represents job scheduler execution history query request.
/// </summary>
public sealed class JobSchedulerExecutionHistoryQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid? OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the execution id.
    /// </summary>
    public Guid? ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the occurrence statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    /// <summary>
    /// Gets or sets the execution statuses.
    /// </summary>
    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }

    /// <summary>
    /// Gets or sets the event names.
    /// </summary>
    public IReadOnlyList<string> EventNames { get; set; }

    /// <summary>
    /// Gets or sets the recorded from utc.
    /// </summary>
    public DateTimeOffset? RecordedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the recorded to utc.
    /// </summary>
    public DateTimeOffset? RecordedToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler lease query request.
/// </summary>
public sealed class JobSchedulerLeaseQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobSchedulerLeaseStatus> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the expires from utc.
    /// </summary>
    public DateTimeOffset? ExpiresFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the expires to utc.
    /// </summary>
    public DateTimeOffset? ExpiresToUtc { get; set; }
}

/// <summary>
/// Represents job scheduler server query request.
/// </summary>
public sealed class JobSchedulerServerQueryRequest : JobSchedulerPagedQueryRequest
{
    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<JobSchedulerServerStatus> Statuses { get; set; }
}

/// <summary>
/// Represents job scheduler metrics request.
/// </summary>
public sealed class JobSchedulerMetricsRequest
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType? TriggerType { get; set; }

    /// <summary>
    /// Gets or sets the occurrence statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    /// <summary>
    /// Gets or sets the execution statuses.
    /// </summary>
    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the due from.
    /// </summary>
    public DateTimeOffset? DueFrom { get; set; }

    /// <summary>
    /// Gets or sets the due to.
    /// </summary>
    public DateTimeOffset? DueTo { get; set; }

    /// <summary>
    /// Gets or sets the completed from.
    /// </summary>
    public DateTimeOffset? CompletedFrom { get; set; }

    /// <summary>
    /// Gets or sets the completed to.
    /// </summary>
    public DateTimeOffset? CompletedTo { get; set; }

    /// <summary>
    /// Stores the from utc.
    /// </summary>
    public DateTimeOffset? FromUtc
    {
        get => this.DueFrom;
        set => this.DueFrom = value;
    }

    /// <summary>
    /// Stores the to utc.
    /// </summary>
    public DateTimeOffset? ToUtc
    {
        get => this.DueTo;
        set => this.DueTo = value;
    }
}

/// <summary>
/// Represents job scheduler dashboard summary request.
/// </summary>
public sealed class JobSchedulerDashboardSummaryRequest
{
    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Stores the from utc.
    /// </summary>
    public DateTimeOffset? FromUtc
    {
        get => this.From;
        set => this.From = value;
    }

    /// <summary>
    /// Stores the to utc.
    /// </summary>
    public DateTimeOffset? ToUtc
    {
        get => this.To;
        set => this.To = value;
    }
}

/// <summary>
/// Represents job scheduler timeline request.
/// </summary>
public sealed class JobSchedulerTimelineRequest
{
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    public JobSchedulerTimelineMode Mode { get; set; } = JobSchedulerTimelineMode.Occurrences;

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets or sets the bucket.
    /// </summary>
    public int Bucket { get; set; } = 60;

    /// <summary>
    /// Stores the from utc.
    /// </summary>
    public DateTimeOffset? FromUtc
    {
        get => this.From;
        set => this.From = value;
    }

    /// <summary>
    /// Stores the to utc.
    /// </summary>
    public DateTimeOffset? ToUtc
    {
        get => this.To;
        set => this.To = value;
    }

    /// <summary>
    /// Stores the bucket minutes.
    /// </summary>
    public int BucketMinutes
    {
        get => this.Bucket;
        set => this.Bucket = value;
    }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the occurrence statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> OccurrenceStatuses { get; set; }

    /// <summary>
    /// Gets or sets the execution statuses.
    /// </summary>
    public IReadOnlyList<JobExecutionStatus> ExecutionStatuses { get; set; }
}

/// <summary>
/// Defines the supported job scheduler lease status values.
/// </summary>
public enum JobSchedulerLeaseStatus
{
    /// <summary>
    /// Represents the active value.
    /// </summary>
    Active = 0,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired = 1,
}

/// <summary>
/// Defines the supported job scheduler server status values.
/// </summary>
public enum JobSchedulerServerStatus
{
    /// <summary>
    /// Represents the active value.
    /// </summary>
    Active = 0,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired = 1,
    /// <summary>
    /// Represents the observed value.
    /// </summary>
    Observed = 2,
}

/// <summary>
/// Defines the supported job scheduler timeline mode values.
/// </summary>
public enum JobSchedulerTimelineMode
{
    /// <summary>
    /// Represents the occurrences value.
    /// </summary>
    Occurrences = 0,
    /// <summary>
    /// Represents the executions value.
    /// </summary>
    Executions = 1,
}

/// <summary>
/// Represents job scheduler query capabilities.
/// </summary>
public sealed class JobSchedulerQueryCapabilities
{
    /// <summary>
    /// Gets or sets the supports lease diagnostics.
    /// </summary>
    public bool SupportsLeaseDiagnostics { get; init; } = true;

    /// <summary>
    /// Gets or sets the supports server diagnostics.
    /// </summary>
    public bool SupportsServerDiagnostics { get; init; } = true;

    /// <summary>
    /// Gets or sets the supports timeline.
    /// </summary>
    public bool SupportsTimeline { get; init; } = true;

    /// <summary>
    /// Gets or sets the supports runtime overlay.
    /// </summary>
    public bool SupportsRuntimeOverlay { get; init; } = true;
}

/// <summary>
/// Represents job scheduler job model.
/// </summary>
public sealed class JobSchedulerJobModel
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public string Group { get; init; }

    /// <summary>
    /// Gets or sets the module.
    /// </summary>
    public string Module { get; init; }

    /// <summary>
    /// Gets or sets the job type.
    /// </summary>
    public string JobType { get; init; }

    /// <summary>
    /// Gets or sets the registered enabled.
    /// </summary>
    public bool RegisteredEnabled { get; init; }

    /// <summary>
    /// Gets or sets the effective enabled.
    /// </summary>
    public bool EffectiveEnabled { get; init; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    public bool Paused { get; init; }

    /// <summary>
    /// Gets or sets the is orphaned runtime state.
    /// </summary>
    public bool IsOrphanedRuntimeState { get; init; }

    /// <summary>
    /// Gets or sets the has orphaned runtime state.
    /// </summary>
    public bool HasOrphanedRuntimeState { get; init; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the concurrency limit.
    /// </summary>
    public int? ConcurrencyLimit { get; init; }

    /// <summary>
    /// Gets or sets the trigger count.
    /// </summary>
    public int TriggerCount { get; init; }

    /// <summary>
    /// Gets or sets the recurring trigger count.
    /// </summary>
    public int RecurringTriggerCount { get; init; }

    /// <summary>
    /// Gets or sets the pending occurrence count.
    /// </summary>
    public int PendingOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the running occurrence count.
    /// </summary>
    public int RunningOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the failed occurrence count.
    /// </summary>
    public int FailedOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the last occurrence utc.
    /// </summary>
    public DateTimeOffset? LastOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets or sets the last execution utc.
    /// </summary>
    public DateTimeOffset? LastExecutionUtc { get; init; }

    /// <summary>
    /// Gets or sets the last execution status.
    /// </summary>
    public JobExecutionStatus? LastExecutionStatus { get; init; }

    /// <summary>
    /// Gets or sets the has failed latest execution.
    /// </summary>
    public bool HasFailedLatestExecution { get; init; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public string DataType { get; init; }

    /// <summary>
    /// Gets or sets the target instances.
    /// </summary>
    public IReadOnlyList<string> TargetInstances { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }
}

/// <summary>
/// Represents job scheduler trigger model.
/// </summary>
public class JobSchedulerTriggerModel
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets or sets the registered enabled.
    /// </summary>
    public bool RegisteredEnabled { get; init; }

    /// <summary>
    /// Gets or sets the effective enabled.
    /// </summary>
    public bool EffectiveEnabled { get; init; }

    /// <summary>
    /// Gets or sets the paused.
    /// </summary>
    public bool Paused { get; init; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the retry max attempts.
    /// </summary>
    public int? RetryMaxAttempts { get; init; }

    /// <summary>
    /// Gets or sets the retry uses exponential backoff.
    /// </summary>
    public bool RetryUsesExponentialBackoff { get; init; }

    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public string Schedule { get; init; }

    /// <summary>
    /// Gets or sets the due utc.
    /// </summary>
    public DateTimeOffset? DueUtc { get; init; }

    /// <summary>
    /// Gets or sets the delay.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Gets or sets the next due utc.
    /// </summary>
    public DateTimeOffset? NextDueUtc { get; init; }

    /// <summary>
    /// Gets or sets the last materialized scheduled utc.
    /// </summary>
    public DateTimeOffset? LastMaterializedScheduledUtc { get; init; }

    /// <summary>
    /// Gets or sets the has materialized occurrence.
    /// </summary>
    public bool HasMaterializedOccurrence { get; init; }

    /// <summary>
    /// Gets or sets the time zone id.
    /// </summary>
    public string TimeZoneId { get; init; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public string DataType { get; init; }

    /// <summary>
    /// Gets or sets the target instances.
    /// </summary>
    public IReadOnlyList<string> TargetInstances { get; init; }

    /// <summary>
    /// Gets or sets the data preview.
    /// </summary>
    public string DataPreview { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }

    /// <summary>
    /// Gets or sets the last occurrence utc.
    /// </summary>
    public DateTimeOffset? LastOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets or sets the last occurrence status.
    /// </summary>
    public JobOccurrenceStatus? LastOccurrenceStatus { get; init; }
}

/// <summary>
/// Represents job scheduler recurring trigger model.
/// </summary>
public sealed class JobSchedulerRecurringTriggerModel : JobSchedulerTriggerModel
{
}

/// <summary>
/// Represents job scheduler occurrence model.
/// </summary>
public class JobSchedulerOccurrenceModel
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the occurrence key.
    /// </summary>
    public string OccurrenceKey { get; init; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobOccurrenceStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the due utc.
    /// </summary>
    public DateTimeOffset DueUtc { get; init; }

    /// <summary>
    /// Gets or sets the scheduled utc.
    /// </summary>
    public DateTimeOffset? ScheduledUtc { get; init; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the causation id.
    /// </summary>
    public string CausationId { get; init; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets or sets the resume status.
    /// </summary>
    public JobOccurrenceStatus? ResumeStatus { get; init; }

    /// <summary>
    /// Gets or sets the blocked reason.
    /// </summary>
    public string BlockedReason { get; init; }

    /// <summary>
    /// Gets or sets the dependency count.
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// Gets or sets the pending dependency count.
    /// </summary>
    public int PendingDependencyCount { get; init; }

    /// <summary>
    /// Gets or sets the failed dependency count.
    /// </summary>
    public int FailedDependencyCount { get; init; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public string DataType { get; init; }

    /// <summary>
    /// Gets or sets the data preview.
    /// </summary>
    public string DataPreview { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }

    /// <summary>
    /// Gets the occurrence properties as display-ready key/value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the attempt count.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// Gets or sets the latest execution status.
    /// </summary>
    public JobExecutionStatus? LatestExecutionStatus { get; init; }

    /// <summary>
    /// Gets or sets the latest execution started utc.
    /// </summary>
    public DateTimeOffset? LatestExecutionStartedUtc { get; init; }

    /// <summary>
    /// Gets or sets the latest execution completed utc.
    /// </summary>
    public DateTimeOffset? LatestExecutionCompletedUtc { get; init; }

    /// <summary>
    /// Gets or sets the latest execution duration seconds.
    /// </summary>
    public double? LatestExecutionDurationSeconds { get; init; }

    /// <summary>
    /// Gets the persisted execution messages for the occurrence, newest attempt first.
    /// </summary>
    public IReadOnlyList<string> ExecutionMessages { get; init; } = [];

    /// <summary>
    /// Gets or sets the lease owner scheduler instance id.
    /// </summary>
    public string LeaseOwnerSchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the batch internal id.
    /// </summary>
    public Guid? BatchInternalId { get; init; }

    /// <summary>
    /// Gets or sets the external batch id.
    /// </summary>
    public string ExternalBatchId { get; init; }
}

/// <summary>
/// Represents job scheduler retry model.
/// </summary>
public sealed class JobSchedulerRetryModel
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the occurrence status.
    /// </summary>
    public JobOccurrenceStatus OccurrenceStatus { get; init; }

    /// <summary>
    /// Gets or sets the attempt count.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// Gets or sets the max attempts.
    /// </summary>
    public int MaxAttempts { get; init; }

    /// <summary>
    /// Gets or sets the has remaining attempts.
    /// </summary>
    public bool HasRemainingAttempts { get; init; }

    /// <summary>
    /// Gets or sets the next attempt number.
    /// </summary>
    public int NextAttemptNumber { get; init; }

    /// <summary>
    /// Gets or sets the retry due utc.
    /// </summary>
    public DateTimeOffset RetryDueUtc { get; init; }

    /// <summary>
    /// Gets or sets the last failure message.
    /// </summary>
    public string LastFailureMessage { get; init; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }
}

/// <summary>
/// Represents job scheduler batch model.
/// </summary>
public sealed class JobSchedulerBatchModel
{
    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets or sets the external batch id.
    /// </summary>
    public string ExternalBatchId { get; init; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobBatchStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the completion policy.
    /// </summary>
    public JobBatchCompletionPolicy CompletionPolicy { get; init; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the causation id.
    /// </summary>
    public string CausationId { get; init; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets or sets the accepted count.
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// Gets or sets the succeeded count.
    /// </summary>
    public int SucceededCount { get; init; }

    /// <summary>
    /// Gets or sets the failed count.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets or sets the cancelled count.
    /// </summary>
    public int CancelledCount { get; init; }

    /// <summary>
    /// Gets or sets the archived count.
    /// </summary>
    public int ArchivedCount { get; init; }

    /// <summary>
    /// Gets or sets the child occurrence count.
    /// </summary>
    public int ChildOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }

    /// <summary>
    /// Gets or sets the completed date.
    /// </summary>
    public DateTimeOffset? CompletedDate { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }
}

/// <summary>
/// Represents job scheduler batch child occurrence model.
/// </summary>
public sealed class JobSchedulerBatchChildOccurrenceModel : JobSchedulerOccurrenceModel
{
    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets or sets the sequence.
    /// </summary>
    public int? Sequence { get; init; }

    /// <summary>
    /// Gets or sets the child status.
    /// </summary>
    public JobOccurrenceStatus ChildStatus { get; init; }
}

/// <summary>
/// Represents job scheduler batch history model.
/// </summary>
public sealed class JobSchedulerBatchHistoryModel
{
    /// <summary>
    /// Gets or sets the history id.
    /// </summary>
    public Guid HistoryId { get; init; }

    /// <summary>
    /// Gets or sets the batch id.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets or sets the external batch id.
    /// </summary>
    public string ExternalBatchId { get; init; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets or sets the batch status.
    /// </summary>
    public JobBatchStatus? BatchStatus { get; init; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }

    /// <summary>
    /// Gets or sets the recorded at.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Represents job scheduler dependency model.
/// </summary>
public sealed class JobSchedulerDependencyModel
{
    /// <summary>
    /// Gets or sets the dependency id.
    /// </summary>
    public Guid DependencyId { get; init; }

    /// <summary>
    /// Gets or sets the dependent occurrence id.
    /// </summary>
    public Guid DependentOccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the dependent job name.
    /// </summary>
    public string DependentJobName { get; init; }

    /// <summary>
    /// Gets or sets the dependent trigger name.
    /// </summary>
    public string DependentTriggerName { get; init; }

    /// <summary>
    /// Gets or sets the dependent status.
    /// </summary>
    public JobOccurrenceStatus? DependentStatus { get; init; }

    /// <summary>
    /// Gets or sets the prerequisite occurrence id.
    /// </summary>
    public Guid PrerequisiteOccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the prerequisite job name.
    /// </summary>
    public string PrerequisiteJobName { get; init; }

    /// <summary>
    /// Gets or sets the prerequisite trigger name.
    /// </summary>
    public string PrerequisiteTriggerName { get; init; }

    /// <summary>
    /// Gets or sets the prerequisite status.
    /// </summary>
    public JobOccurrenceStatus? PrerequisiteStatus { get; init; }

    /// <summary>
    /// Gets or sets the required statuses.
    /// </summary>
    public IReadOnlyList<JobOccurrenceStatus> RequiredStatuses { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobDependencyStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the failure policy.
    /// </summary>
    public JobDependencyFailurePolicy FailurePolicy { get; init; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}

/// <summary>
/// Represents job scheduler execution model.
/// </summary>
public sealed class JobSchedulerExecutionModel
{
    /// <summary>
    /// Gets or sets the execution id.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the started utc.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// Gets or sets the completed utc.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// Gets or sets the duration seconds.
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; init; }
}

/// <summary>
/// Represents job scheduler execution history model.
/// </summary>
public sealed class JobSchedulerExecutionHistoryModel
{
    /// <summary>
    /// Gets or sets the history id.
    /// </summary>
    public Guid HistoryId { get; init; }

    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the execution id.
    /// </summary>
    public Guid? ExecutionId { get; init; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets or sets the occurrence status.
    /// </summary>
    public JobOccurrenceStatus? OccurrenceStatus { get; init; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public JobExecutionStatus? ExecutionStatus { get; init; }

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets or sets the recorded at.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets or sets the recorded by.
    /// </summary>
    public string RecordedBy { get; init; }

    /// <summary>
    /// Gets or sets the property keys.
    /// </summary>
    public IReadOnlyList<string> PropertyKeys { get; init; }

    /// <summary>
    /// Gets or sets the property count.
    /// </summary>
    public int PropertyCount { get; init; }
}

/// <summary>
/// Represents job scheduler lease model.
/// </summary>
public sealed class JobSchedulerLeaseModel
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobSchedulerLeaseStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the acquired utc.
    /// </summary>
    public DateTimeOffset AcquiredUtc { get; init; }

    /// <summary>
    /// Gets or sets the renewed utc.
    /// </summary>
    public DateTimeOffset? RenewedUtc { get; init; }

    /// <summary>
    /// Gets or sets the expires utc.
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>
    /// Gets or sets the renewal count.
    /// </summary>
    public int RenewalCount { get; init; }
}

/// <summary>
/// Represents job scheduler server model.
/// </summary>
public sealed class JobSchedulerServerModel
{
    /// <summary>
    /// Gets or sets the scheduler instance id.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public JobSchedulerServerStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the last seen utc.
    /// </summary>
    public DateTimeOffset? LastSeenUtc { get; init; }

    /// <summary>
    /// Gets or sets the active lease count.
    /// </summary>
    public int ActiveLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the expired lease count.
    /// </summary>
    public int ExpiredLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets or sets the last execution utc.
    /// </summary>
    public DateTimeOffset? LastExecutionUtc { get; init; }

    /// <summary>
    /// Gets or sets the last history utc.
    /// </summary>
    public DateTimeOffset? LastHistoryUtc { get; init; }
}

/// <summary>
/// Represents job scheduler metrics model.
/// </summary>
public sealed class JobSchedulerMetricsModel
{
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets or sets the registered job count.
    /// </summary>
    public int RegisteredJobCount { get; init; }

    /// <summary>
    /// Gets or sets the registered trigger count.
    /// </summary>
    public int RegisteredTriggerCount { get; init; }

    /// <summary>
    /// Gets or sets the occurrence count.
    /// </summary>
    public long OccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    public long ExecutionCount { get; init; }

    /// <summary>
    /// Gets or sets the batch count.
    /// </summary>
    public long BatchCount { get; init; }

    /// <summary>
    /// Gets or sets the active lease count.
    /// </summary>
    public long ActiveLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the expired lease count.
    /// </summary>
    public long ExpiredLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the active server count.
    /// </summary>
    public long ActiveServerCount { get; init; }

    /// <summary>
    /// Gets or sets the retry scheduled count.
    /// </summary>
    public long RetryScheduledCount { get; init; }

    /// <summary>
    /// Gets or sets the average execution duration seconds.
    /// </summary>
    public double? AverageExecutionDurationSeconds { get; init; }

    /// <summary>
    /// Gets or sets the occurrence counts by status.
    /// </summary>
    public IReadOnlyDictionary<JobOccurrenceStatus, long> OccurrenceCountsByStatus { get; init; }

    /// <summary>
    /// Gets or sets the execution counts by status.
    /// </summary>
    public IReadOnlyDictionary<JobExecutionStatus, long> ExecutionCountsByStatus { get; init; }

    /// <summary>
    /// Gets or sets the counts by job.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountsByJob { get; init; }
}

/// <summary>
/// Represents job scheduler dashboard summary model.
/// </summary>
public sealed class JobSchedulerDashboardSummaryModel
{
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets or sets the job facets.
    /// </summary>
    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    /// <summary>
    /// Gets or sets the enabled job count.
    /// </summary>
    public int EnabledJobCount { get; init; }

    /// <summary>
    /// Gets or sets the paused job count.
    /// </summary>
    public int PausedJobCount { get; init; }

    /// <summary>
    /// Gets or sets the enabled trigger count.
    /// </summary>
    public int EnabledTriggerCount { get; init; }

    /// <summary>
    /// Gets or sets the due occurrence count.
    /// </summary>
    public long DueOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the running occurrence count.
    /// </summary>
    public long RunningOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the failed occurrence count.
    /// </summary>
    public long FailedOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the retry scheduled count.
    /// </summary>
    public long RetryScheduledCount { get; init; }

    /// <summary>
    /// Gets or sets the active lease count.
    /// </summary>
    public long ActiveLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the processing batch count.
    /// </summary>
    public long ProcessingBatchCount { get; init; }

    /// <summary>
    /// Gets or sets the active server count.
    /// </summary>
    public long ActiveServerCount { get; init; }

    /// <summary>
    /// Gets or sets the oldest due occurrence utc.
    /// </summary>
    public DateTimeOffset? OldestDueOccurrenceUtc { get; init; }
}

/// <summary>
/// Represents job scheduler job facet counts model.
/// </summary>
public sealed class JobSchedulerJobFacetCountsModel
{
    /// <summary>
    /// Gets or sets the enabled count.
    /// </summary>
    public long EnabledCount { get; init; }

    /// <summary>
    /// Gets or sets the disabled count.
    /// </summary>
    public long DisabledCount { get; init; }

    /// <summary>
    /// Gets or sets the paused count.
    /// </summary>
    public long PausedCount { get; init; }

    /// <summary>
    /// Gets or sets the orphaned runtime state count.
    /// </summary>
    public long OrphanedRuntimeStateCount { get; init; }

    /// <summary>
    /// Gets or sets the failed latest execution count.
    /// </summary>
    public long FailedLatestExecutionCount { get; init; }
}

/// <summary>
/// Represents job scheduler dashboard navigation model.
/// </summary>
public sealed class JobSchedulerDashboardNavigationModel
{
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets or sets the job facets.
    /// </summary>
    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    /// <summary>
    /// Gets or sets the links.
    /// </summary>
    public IReadOnlyList<JobSchedulerDashboardNavigationLinkModel> Links { get; init; }
}

/// <summary>
/// Represents job scheduler dashboard navigation link model.
/// </summary>
public sealed class JobSchedulerDashboardNavigationLinkModel
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Gets or sets the route.
    /// </summary>
    public string Route { get; init; }

    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    public long Count { get; init; }
}

/// <summary>
/// Represents job scheduler dashboard overview model.
/// </summary>
public sealed class JobSchedulerDashboardOverviewModel
{
    /// <summary>
    /// Gets or sets the capabilities.
    /// </summary>
    public JobSchedulerQueryCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets or sets the job facets.
    /// </summary>
    public JobSchedulerJobFacetCountsModel JobFacets { get; init; }

    /// <summary>
    /// Gets or sets the enabled trigger count.
    /// </summary>
    public int EnabledTriggerCount { get; init; }

    /// <summary>
    /// Gets or sets the due occurrence count.
    /// </summary>
    public long DueOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the running occurrence count.
    /// </summary>
    public long RunningOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the failed occurrence count.
    /// </summary>
    public long FailedOccurrenceCount { get; init; }

    /// <summary>
    /// Gets or sets the retry scheduled count.
    /// </summary>
    public long RetryScheduledCount { get; init; }

    /// <summary>
    /// Gets or sets the active lease count.
    /// </summary>
    public long ActiveLeaseCount { get; init; }

    /// <summary>
    /// Gets or sets the processing batch count.
    /// </summary>
    public long ProcessingBatchCount { get; init; }

    /// <summary>
    /// Gets or sets the active server count.
    /// </summary>
    public long ActiveServerCount { get; init; }

    /// <summary>
    /// Gets or sets the oldest due occurrence utc.
    /// </summary>
    public DateTimeOffset? OldestDueOccurrenceUtc { get; init; }
}

/// <summary>
/// Represents job scheduler timeline model.
/// </summary>
public sealed class JobSchedulerTimelineModel
{
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    public JobSchedulerTimelineMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the from utc.
    /// </summary>
    public DateTimeOffset FromUtc { get; init; }

    /// <summary>
    /// Gets or sets the to utc.
    /// </summary>
    public DateTimeOffset ToUtc { get; init; }

    /// <summary>
    /// Gets or sets the bucket minutes.
    /// </summary>
    public int BucketMinutes { get; init; }

    /// <summary>
    /// Gets or sets the buckets.
    /// </summary>
    public IReadOnlyList<JobSchedulerTimelineBucketModel> Buckets { get; init; }
}

/// <summary>
/// Represents job scheduler timeline bucket model.
/// </summary>
public sealed class JobSchedulerTimelineBucketModel
{
    /// <summary>
    /// Gets or sets the bucket start utc.
    /// </summary>
    public DateTimeOffset BucketStartUtc { get; init; }

    /// <summary>
    /// Gets or sets the bucket end utc.
    /// </summary>
    public DateTimeOffset BucketEndUtc { get; init; }

    /// <summary>
    /// Gets or sets the counts by status.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountsByStatus { get; init; }
}
