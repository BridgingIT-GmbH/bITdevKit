// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Collections;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides provider-neutral Jobs query operations over active registrations and persisted scheduler state.
/// </summary>
public class JobSchedulerQueryService(
    JobRegistrationStore registrations,
    IJobStoreProvider stores,
    TimeProvider timeProvider,
    IJobCronEngine cronEngine,
    IJobCalendarEngine calendarEngine) : IJobSchedulerQueryService
{
    private static readonly JobSchedulerQueryCapabilities Capabilities = new();

    public async Task<ResultPaged<JobSchedulerJobModel>> QueryJobsAsync(JobSchedulerJobQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerJobQueryRequest { SortBy = "JobName" };
        var validation = ValidatePagedRequest(request, ["JobName", "DisplayName", "Group", "Module", "LastExecutionUtc", "LastOccurrenceUtc"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerJobModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);

            var models = BuildJobModels(snapshot, request.IncludeOrphanedRuntimeState)
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.Group, request.Group))
                .Where(model => Matches(model.Module, request.Module))
                .Where(model => !request.Enabled.HasValue || model.EffectiveEnabled == request.Enabled.Value)
                .Where(model => !request.Paused.HasValue || model.Paused == request.Paused.Value)
                .ToList();

            return Page(models, request, SortJobs);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerJobModel>.Failure().WithError(new Error("Job query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerJobModel>.Failure().WithError(new Error($"Job query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerTriggerModel>> QueryTriggersAsync(JobSchedulerTriggerQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerTriggerQueryRequest { SortBy = "JobName" };
        var validation = ValidatePagedRequest(request, ["JobName", "TriggerName", "TriggerType", "NextDueUtc", "LastOccurrenceUtc"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerTriggerModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var typeFilter = request.TriggerTypes.SafeNull().ToHashSet();

            var models = snapshot.Definitions
                .SelectMany(definition => definition.Triggers.Select(trigger => MapTrigger(definition, trigger, snapshot, cronEngine, calendarEngine)))
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => typeFilter.Count == 0 || typeFilter.Contains(model.TriggerType))
                .Where(model => !request.Enabled.HasValue || model.EffectiveEnabled == request.Enabled.Value)
                .Where(model => !request.Paused.HasValue || model.Paused == request.Paused.Value)
                .Cast<JobSchedulerTriggerModel>()
                .ToList();

            return Page(models, request, SortTriggers);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerTriggerModel>.Failure().WithError(new Error("Trigger query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerTriggerModel>.Failure().WithError(new Error($"Trigger query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerRecurringTriggerModel>> QueryRecurringTriggersAsync(JobSchedulerRecurringTriggerQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerRecurringTriggerQueryRequest { SortBy = "NextDueUtc" };
        var validation = ValidatePagedRequest(request, ["JobName", "TriggerName", "TriggerType", "NextDueUtc", "LastMaterializedScheduledUtc"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerRecurringTriggerModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var typeFilter = request.TriggerTypes.SafeNull().ToHashSet();

            var models = snapshot.Definitions
                .SelectMany(definition => definition.Triggers
                    .Where(IsRecurringTrigger)
                    .Select(trigger => MapRecurringTrigger(definition, trigger, snapshot, cronEngine, calendarEngine)))
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => typeFilter.Count == 0 || typeFilter.Contains(model.TriggerType))
                .Where(model => !request.Enabled.HasValue || model.EffectiveEnabled == request.Enabled.Value)
                .Where(model => !request.Paused.HasValue || model.Paused == request.Paused.Value)
                .ToList();

            return Page(models, request, SortRecurringTriggers);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerRecurringTriggerModel>.Failure().WithError(new Error("Recurring trigger query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerRecurringTriggerModel>.Failure().WithError(new Error($"Recurring trigger query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerOccurrenceModel>> QueryOccurrencesAsync(JobSchedulerOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerOccurrenceQueryRequest { SortBy = "DueUtc" };
        var validation = ValidatePagedRequest(request, ["DueUtc", "CreatedDate", "UpdatedDate", "JobName", "TriggerName", "Status"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerOccurrenceModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();

            var models = snapshot.Occurrences
                .Select(occurrence => MapOccurrence(occurrence, snapshot))
                .Where(model => !request.OccurrenceId.HasValue || model.OccurrenceId == request.OccurrenceId.Value)
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => !request.TriggerType.HasValue || model.TriggerType == request.TriggerType.Value)
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .Where(model => Matches(model.CorrelationId, request.CorrelationId))
                .Where(model => Matches(model.IdempotencyKey, request.IdempotencyKey))
                .Where(model => Matches(model.LeaseOwnerSchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => !request.DueFrom.HasValue || model.DueUtc >= request.DueFrom.Value)
                .Where(model => !request.DueTo.HasValue || model.DueUtc <= request.DueTo.Value)
                .Where(model => MatchesExecutionWindow(snapshot, model.OccurrenceId, request.StartedFrom, request.StartedTo, request.CompletedFrom, request.CompletedTo))
                .Where(model => !request.CreatedFromUtc.HasValue || model.CreatedDate >= request.CreatedFromUtc.Value)
                .Where(model => !request.CreatedToUtc.HasValue || model.CreatedDate <= request.CreatedToUtc.Value)
                .ToList();

            return Page(models, request, SortOccurrences);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerOccurrenceModel>.Failure().WithError(new Error("Occurrence query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerOccurrenceModel>.Failure().WithError(new Error($"Occurrence query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerRetryModel>> QueryRetriesAsync(JobSchedulerRetryQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerRetryQueryRequest { SortBy = "RetryDueUtc" };
        var validation = ValidatePagedRequest(request, ["RetryDueUtc", "JobName", "TriggerName", "AttemptCount"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerRetryModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);

            var models = snapshot.Occurrences
                .Select(occurrence => MapRetry(occurrence, snapshot))
                .Where(model => model is not null)
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => Matches(model.CorrelationId, request.CorrelationId))
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => !request.HasRemainingAttempts.HasValue || model.HasRemainingAttempts == request.HasRemainingAttempts.Value)
                .ToList();

            return Page(models, request, SortRetries);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerRetryModel>.Failure().WithError(new Error("Retry query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerRetryModel>.Failure().WithError(new Error($"Retry query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerBatchModel>> QueryBatchesAsync(JobSchedulerBatchQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerBatchQueryRequest { SortBy = "CreatedDate" };
        var validation = ValidatePagedRequest(request, ["CreatedDate", "UpdatedDate", "CompletedDate", "BatchId", "Status"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerBatchModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();

            var models = snapshot.Batches
                .Select(batch => MapBatch(batch, snapshot))
                .Where(model => Matches(model.ExternalBatchId, request.BatchId))
                .Where(model => Matches(model.CorrelationId, request.CorrelationId))
                .Where(model => Matches(model.IdempotencyKey, request.IdempotencyKey))
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .Where(model => !request.CreatedFromUtc.HasValue || model.CreatedDate >= request.CreatedFromUtc.Value)
                .Where(model => !request.CreatedToUtc.HasValue || model.CreatedDate <= request.CreatedToUtc.Value)
                .ToList();

            return Page(models, request, SortBatches);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerBatchModel>.Failure().WithError(new Error("Batch query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerBatchModel>.Failure().WithError(new Error($"Batch query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerBatchChildOccurrenceModel>> QueryBatchOccurrencesAsync(string batchId, JobSchedulerBatchOccurrenceQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerBatchOccurrenceQueryRequest { SortBy = "Sequence" };
        var validation = ValidatePagedRequest(request, ["Sequence", "DueUtc", "JobName", "TriggerName", "Status"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerBatchChildOccurrenceModel>.Failure().WithErrors(validation.Errors);
        }

        if (string.IsNullOrWhiteSpace(batchId))
        {
            return ResultPaged<JobSchedulerBatchChildOccurrenceModel>.Failure().WithError(new Error("Batch id is required."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var batch = snapshot.Batches.FirstOrDefault(x =>
                string.Equals(x.ExternalBatchId, batchId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.BatchId.ToString("D"), batchId, StringComparison.OrdinalIgnoreCase));

            if (batch is null)
            {
                return ResultPaged<JobSchedulerBatchChildOccurrenceModel>.Failure().WithError(new Error($"Batch '{batchId}' was not found."));
            }

            var statusFilter = request.Statuses.SafeNull().ToHashSet();
            var models = snapshot.BatchOccurrencesByBatchId.GetValueOrDefault(batch.BatchId, [])
                .Select(link => MapBatchOccurrence(batch, link, snapshot))
                .Where(model => model is not null)
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.ChildStatus))
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .ToList();

            return Page(models, request, SortBatchOccurrences);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerBatchChildOccurrenceModel>.Failure().WithError(new Error("Batch occurrence query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerBatchChildOccurrenceModel>.Failure().WithError(new Error($"Batch occurrence query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerBatchHistoryModel>> QueryBatchHistoryAsync(string batchId, JobSchedulerBatchHistoryQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerBatchHistoryQueryRequest { SortBy = "RecordedAt" };
        var validation = ValidatePagedRequest(request, ["RecordedAt", "EventName", "BatchStatus", "SchedulerInstanceId"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerBatchHistoryModel>.Failure().WithErrors(validation.Errors);
        }

        if (string.IsNullOrWhiteSpace(batchId))
        {
            return ResultPaged<JobSchedulerBatchHistoryModel>.Failure().WithError(new Error("Batch id is required."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var batch = snapshot.Batches.FirstOrDefault(x =>
                string.Equals(x.ExternalBatchId, batchId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.BatchId.ToString("D"), batchId, StringComparison.OrdinalIgnoreCase));

            if (batch is null)
            {
                return ResultPaged<JobSchedulerBatchHistoryModel>.Failure().WithError(new Error($"Batch '{batchId}' was not found."));
            }

            var statusFilter = request.BatchStatuses.SafeNull().ToHashSet();
            var models = snapshot.BatchHistoryByBatchId.GetValueOrDefault(batch.BatchId, [])
                .Select(MapBatchHistory)
                .Where(model => Matches(model.EventName, request.EventName))
                .Where(model => statusFilter.Count == 0 || model.BatchStatus.HasValue && statusFilter.Contains(model.BatchStatus.Value))
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => !request.RecordedFromUtc.HasValue || model.RecordedAt >= request.RecordedFromUtc.Value)
                .Where(model => !request.RecordedToUtc.HasValue || model.RecordedAt <= request.RecordedToUtc.Value)
                .ToList();

            return Page(models, request, SortBatchHistory);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerBatchHistoryModel>.Failure().WithError(new Error("Batch history query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerBatchHistoryModel>.Failure().WithError(new Error($"Batch history query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerDependencyModel>> QueryDependenciesAsync(JobSchedulerDependencyQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerDependencyQueryRequest { SortBy = "CreatedDate" };
        var validation = ValidatePagedRequest(request, ["CreatedDate", "UpdatedDate", "Status", "DependentJobName", "PrerequisiteJobName"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerDependencyModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();
            var failurePolicyFilter = request.FailurePolicies.SafeNull().ToHashSet();

            var models = snapshot.Dependencies
                .Select(dependency => MapDependency(dependency, snapshot))
                .Where(model => !request.DependencyId.HasValue || model.DependencyId == request.DependencyId.Value)
                .Where(model => !request.OccurrenceId.HasValue || model.DependentOccurrenceId == request.OccurrenceId.Value || model.PrerequisiteOccurrenceId == request.OccurrenceId.Value)
                .Where(model => !request.DependentOccurrenceId.HasValue || model.DependentOccurrenceId == request.DependentOccurrenceId.Value)
                .Where(model => !request.PrerequisiteOccurrenceId.HasValue || model.PrerequisiteOccurrenceId == request.PrerequisiteOccurrenceId.Value)
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .Where(model => failurePolicyFilter.Count == 0 || failurePolicyFilter.Contains(model.FailurePolicy))
                .Where(model => !request.CreatedFromUtc.HasValue || model.CreatedDate >= request.CreatedFromUtc.Value)
                .Where(model => !request.CreatedToUtc.HasValue || model.CreatedDate <= request.CreatedToUtc.Value)
                .ToList();

            return Page(models, request, SortDependencies);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerDependencyModel>.Failure().WithError(new Error("Dependency query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerDependencyModel>.Failure().WithError(new Error($"Dependency query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerExecutionModel>> QueryExecutionsAsync(JobSchedulerExecutionQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerExecutionQueryRequest { SortBy = "StartedUtc" };
        var validation = ValidatePagedRequest(request, ["StartedUtc", "CompletedUtc", "AttemptNumber", "JobName", "TriggerName", "Status", "SchedulerInstanceId"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerExecutionModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();

            var models = snapshot.Executions
                .Select(execution => MapExecution(execution, snapshot))
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => !request.TriggerType.HasValue || MatchesExecutionTriggerType(snapshot, model.OccurrenceId, request.TriggerType.Value))
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => Matches(model.CorrelationId, request.CorrelationId))
                .Where(model => Matches(model.IdempotencyKey, request.IdempotencyKey))
                .Where(model => !request.DueFrom.HasValue || MatchesExecutionDueLowerBound(snapshot, model.OccurrenceId, request.DueFrom.Value))
                .Where(model => !request.DueTo.HasValue || MatchesExecutionDueUpperBound(snapshot, model.OccurrenceId, request.DueTo.Value))
                .Where(model => !request.StartedFrom.HasValue || model.StartedUtc >= request.StartedFrom.Value)
                .Where(model => !request.StartedTo.HasValue || model.StartedUtc <= request.StartedTo.Value)
                .Where(model => !request.CompletedFrom.HasValue || (model.CompletedUtc.HasValue && model.CompletedUtc.Value >= request.CompletedFrom.Value))
                .Where(model => !request.CompletedTo.HasValue || (model.CompletedUtc.HasValue && model.CompletedUtc.Value <= request.CompletedTo.Value))
                .ToList();

            return Page(models, request, SortExecutions);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerExecutionModel>.Failure().WithError(new Error("Execution query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerExecutionModel>.Failure().WithError(new Error($"Execution query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerExecutionHistoryModel>> QueryExecutionHistoryAsync(JobSchedulerExecutionHistoryQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerExecutionHistoryQueryRequest { SortBy = "RecordedAt" };
        var validation = ValidatePagedRequest(request, ["RecordedAt", "JobName", "TriggerName", "EventName", "SchedulerInstanceId"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerExecutionHistoryModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var occurrenceStatusFilter = request.OccurrenceStatuses.SafeNull().ToHashSet();
            var executionStatusFilter = request.ExecutionStatuses.SafeNull().ToHashSet();
            var eventNames = request.EventNames.SafeNull().Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var models = snapshot.History
                .Select(MapHistory)
                .Where(model => !request.OccurrenceId.HasValue || model.OccurrenceId == request.OccurrenceId.Value)
                .Where(model => !request.ExecutionId.HasValue || model.ExecutionId == request.ExecutionId.Value)
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => occurrenceStatusFilter.Count == 0 || (model.OccurrenceStatus.HasValue && occurrenceStatusFilter.Contains(model.OccurrenceStatus.Value)))
                .Where(model => executionStatusFilter.Count == 0 || (model.ExecutionStatus.HasValue && executionStatusFilter.Contains(model.ExecutionStatus.Value)))
                .Where(model => eventNames.Count == 0 || eventNames.Contains(model.EventName ?? string.Empty))
                .Where(model => !request.RecordedFromUtc.HasValue || model.RecordedAt >= request.RecordedFromUtc.Value)
                .Where(model => !request.RecordedToUtc.HasValue || model.RecordedAt <= request.RecordedToUtc.Value)
                .ToList();

            return Page(models, request, SortHistory);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerExecutionHistoryModel>.Failure().WithError(new Error("Execution history query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerExecutionHistoryModel>.Failure().WithError(new Error($"Execution history query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerLeaseModel>> QueryLeasesAsync(JobSchedulerLeaseQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerLeaseQueryRequest { SortBy = "ExpiresUtc" };
        var validation = ValidatePagedRequest(request, ["AcquiredUtc", "ExpiresUtc", "RenewedUtc", "SchedulerInstanceId", "JobName", "TriggerName"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerLeaseModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();

            var models = snapshot.Leases
                .Select(lease => MapLease(lease, snapshot))
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => Matches(model.JobName, request.JobName))
                .Where(model => Matches(model.TriggerName, request.TriggerName))
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .Where(model => !request.ExpiresFromUtc.HasValue || model.ExpiresUtc >= request.ExpiresFromUtc.Value)
                .Where(model => !request.ExpiresToUtc.HasValue || model.ExpiresUtc <= request.ExpiresToUtc.Value)
                .ToList();

            return Page(models, request, SortLeases);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerLeaseModel>.Failure().WithError(new Error("Lease query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerLeaseModel>.Failure().WithError(new Error($"Lease query failed: {exception.Message}"));
        }
    }

    public async Task<ResultPaged<JobSchedulerServerModel>> QueryServersAsync(JobSchedulerServerQueryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerServerQueryRequest { SortBy = "LastSeenUtc" };
        var validation = ValidatePagedRequest(request, ["SchedulerInstanceId", "Status", "LastSeenUtc", "LastExecutionUtc", "LastHistoryUtc"]);
        if (validation.IsFailure)
        {
            return ResultPaged<JobSchedulerServerModel>.Failure().WithErrors(validation.Errors);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var statusFilter = request.Statuses.SafeNull().ToHashSet();

            var models = BuildServers(snapshot)
                .Where(model => Matches(model.SchedulerInstanceId, request.SchedulerInstanceId))
                .Where(model => statusFilter.Count == 0 || statusFilter.Contains(model.Status))
                .ToList();

            return Page(models, request, SortServers);
        }
        catch (OperationCanceledException)
        {
            return ResultPaged<JobSchedulerServerModel>.Failure().WithError(new Error("Server query was canceled."));
        }
        catch (Exception exception)
        {
            return ResultPaged<JobSchedulerServerModel>.Failure().WithError(new Error($"Server query failed: {exception.Message}"));
        }
    }

    public async Task<Result<JobSchedulerMetricsModel>> GetMetricsAsync(JobSchedulerMetricsRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerMetricsRequest();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var filteredOccurrences = FilterMetricsOccurrences(snapshot, request).ToList();
            var filteredExecutions = FilterMetricsExecutions(snapshot, request).ToList();
            var filteredLeases = FilterMetricsLeases(snapshot, request).ToList();
            var filteredServers = BuildServers(snapshot)
                .Where(x => Matches(x.SchedulerInstanceId, request.SchedulerInstanceId))
                .ToList();
            var durations = filteredExecutions
                .Where(x => x.CompletedUtc.HasValue)
                .Select(x => (x.CompletedUtc.Value - x.StartedUtc).TotalSeconds)
                .ToArray();

            var model = new JobSchedulerMetricsModel
            {
                Capabilities = Capabilities,
                RegisteredJobCount = snapshot.Definitions.Count,
                RegisteredTriggerCount = snapshot.Definitions.Sum(x => x.Triggers.Count),
                OccurrenceCount = filteredOccurrences.Count,
                ExecutionCount = filteredExecutions.Count,
                BatchCount = snapshot.Batches.Count,
                ActiveLeaseCount = filteredLeases.LongCount(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Active),
                ExpiredLeaseCount = filteredLeases.LongCount(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Expired),
                ActiveServerCount = filteredServers.LongCount(x => x.Status == JobSchedulerServerStatus.Active),
                RetryScheduledCount = filteredOccurrences.LongCount(x => x.Status == JobOccurrenceStatus.RetryScheduled),
                AverageExecutionDurationSeconds = durations.Length == 0 ? null : durations.Average(),
                OccurrenceCountsByStatus = filteredOccurrences
                    .GroupBy(x => x.Status)
                    .ToDictionary(group => group.Key, group => (long)group.LongCount()),
                ExecutionCountsByStatus = filteredExecutions
                    .GroupBy(x => x.Status)
                    .ToDictionary(group => group.Key, group => (long)group.LongCount()),
                CountsByJob = filteredOccurrences
                    .GroupBy(x => x.JobName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => (long)group.LongCount(), StringComparer.OrdinalIgnoreCase),
            };

            return Result<JobSchedulerMetricsModel>.Success(model);
        }
        catch (OperationCanceledException)
        {
            return Result<JobSchedulerMetricsModel>.Failure().WithError(new Error("Metrics query was canceled."));
        }
        catch (Exception exception)
        {
            return Result<JobSchedulerMetricsModel>.Failure().WithError(new Error($"Metrics query failed: {exception.Message}"));
        }
    }

    public async Task<Result<JobSchedulerDashboardSummaryModel>> GetDashboardSummaryAsync(JobSchedulerDashboardSummaryRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerDashboardSummaryRequest();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var jobs = BuildJobModels(snapshot, includeOrphanedRuntimeState: true);
            var triggers = snapshot.Definitions.SelectMany(definition => definition.Triggers.Select(trigger => MapTrigger(definition, trigger, snapshot, cronEngine, calendarEngine))).ToList();
            var occurrences = snapshot.Occurrences
                .Where(x => !request.From.HasValue || x.CreatedDate >= request.From.Value)
                .Where(x => !request.To.HasValue || x.CreatedDate <= request.To.Value)
                .ToList();
            var leases = snapshot.Leases;
            var batches = snapshot.Batches;
            var servers = BuildServers(snapshot);
            var jobFacets = BuildJobFacets(jobs);

            return Result<JobSchedulerDashboardSummaryModel>.Success(new JobSchedulerDashboardSummaryModel
            {
                Capabilities = Capabilities,
                JobFacets = jobFacets,
                EnabledJobCount = jobs.Count(x => x.EffectiveEnabled),
                PausedJobCount = jobs.Count(x => x.Paused),
                EnabledTriggerCount = triggers.Count(x => x.EffectiveEnabled),
                DueOccurrenceCount = occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Due),
                RunningOccurrenceCount = occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Running),
                FailedOccurrenceCount = occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Failed),
                RetryScheduledCount = occurrences.LongCount(x => x.Status == JobOccurrenceStatus.RetryScheduled),
                ActiveLeaseCount = leases.LongCount(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Active),
                ProcessingBatchCount = batches.LongCount(x => x.Status == JobBatchStatus.Processing),
                ActiveServerCount = servers.LongCount(x => x.Status == JobSchedulerServerStatus.Active),
                OldestDueOccurrenceUtc = occurrences
                    .Where(x => x.Status == JobOccurrenceStatus.Due)
                    .OrderBy(x => x.DueUtc)
                    .Select(x => (DateTimeOffset?)x.DueUtc)
                    .FirstOrDefault(),
            });
        }
        catch (OperationCanceledException)
        {
            return Result<JobSchedulerDashboardSummaryModel>.Failure().WithError(new Error("Dashboard summary query was canceled."));
        }
        catch (Exception exception)
        {
            return Result<JobSchedulerDashboardSummaryModel>.Failure().WithError(new Error($"Dashboard summary query failed: {exception.Message}"));
        }
    }

    public async Task<Result<JobSchedulerDashboardNavigationModel>> GetDashboardNavigationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var jobs = BuildJobModels(snapshot, includeOrphanedRuntimeState: true);
            var facets = BuildJobFacets(jobs);

            return Result<JobSchedulerDashboardNavigationModel>.Success(new JobSchedulerDashboardNavigationModel
            {
                Capabilities = Capabilities,
                JobFacets = facets,
                Links =
                [
                    new JobSchedulerDashboardNavigationLinkModel { Key = "jobs", Title = "Jobs", Route = "/_bdk/api/jobs", Count = jobs.Count },
                    new JobSchedulerDashboardNavigationLinkModel { Key = "failed", Title = "Failed occurrences", Route = "/_bdk/api/jobs/occurrences?statuses=Failed", Count = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Failed) },
                    new JobSchedulerDashboardNavigationLinkModel { Key = "retries", Title = "Retries", Route = "/_bdk/api/jobs/retries", Count = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.RetryScheduled) },
                    new JobSchedulerDashboardNavigationLinkModel { Key = "leases", Title = "Leases", Route = "/_bdk/api/jobs/leases", Count = snapshot.Leases.LongCount() },
                    new JobSchedulerDashboardNavigationLinkModel { Key = "orphaned-runtime-state", Title = "Orphaned runtime state", Route = "/_bdk/api/jobs?includeOrphanedRuntimeState=true", Count = facets.OrphanedRuntimeStateCount },
                ],
            });
        }
        catch (OperationCanceledException)
        {
            return Result<JobSchedulerDashboardNavigationModel>.Failure().WithError(new Error("Dashboard navigation query was canceled."));
        }
        catch (Exception exception)
        {
            return Result<JobSchedulerDashboardNavigationModel>.Failure().WithError(new Error($"Dashboard navigation query failed: {exception.Message}"));
        }
    }

    public async Task<Result<JobSchedulerDashboardOverviewModel>> GetDashboardOverviewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var jobs = BuildJobModels(snapshot, includeOrphanedRuntimeState: true);
            var triggers = snapshot.Definitions.SelectMany(definition => definition.Triggers.Select(trigger => MapTrigger(definition, trigger, snapshot, cronEngine, calendarEngine))).ToList();

            return Result<JobSchedulerDashboardOverviewModel>.Success(new JobSchedulerDashboardOverviewModel
            {
                Capabilities = Capabilities,
                JobFacets = BuildJobFacets(jobs),
                EnabledTriggerCount = triggers.Count(x => x.EffectiveEnabled),
                DueOccurrenceCount = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Due),
                RunningOccurrenceCount = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Running),
                FailedOccurrenceCount = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.Failed),
                RetryScheduledCount = snapshot.Occurrences.LongCount(x => x.Status == JobOccurrenceStatus.RetryScheduled),
                ActiveLeaseCount = snapshot.Leases.LongCount(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Active),
                ProcessingBatchCount = snapshot.Batches.LongCount(x => x.Status == JobBatchStatus.Processing),
                ActiveServerCount = BuildServers(snapshot).LongCount(x => x.Status == JobSchedulerServerStatus.Active),
                OldestDueOccurrenceUtc = snapshot.Occurrences
                    .Where(x => x.Status == JobOccurrenceStatus.Due)
                    .OrderBy(x => x.DueUtc)
                    .Select(x => (DateTimeOffset?)x.DueUtc)
                    .FirstOrDefault(),
            });
        }
        catch (OperationCanceledException)
        {
            return Result<JobSchedulerDashboardOverviewModel>.Failure().WithError(new Error("Dashboard overview query was canceled."));
        }
        catch (Exception exception)
        {
            return Result<JobSchedulerDashboardOverviewModel>.Failure().WithError(new Error($"Dashboard overview query failed: {exception.Message}"));
        }
    }

    public async Task<Result<JobSchedulerTimelineModel>> GetDashboardTimelineAsync(JobSchedulerTimelineRequest request = null, CancellationToken cancellationToken = default)
    {
        request ??= new JobSchedulerTimelineRequest();
        if (request.Bucket <= 0)
        {
            return Result<JobSchedulerTimelineModel>.Failure().WithError(new Error("Bucket must be greater than zero."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var nowUtc = timeProvider.GetUtcNow();
            var fromUtc = request.From ?? nowUtc.AddHours(-24);
            var toUtc = request.To ?? nowUtc;

            if (toUtc < fromUtc)
            {
                return Result<JobSchedulerTimelineModel>.Failure().WithError(new Error("Timeline ToUtc must be greater than or equal to FromUtc."));
            }

            var buckets = request.Mode == JobSchedulerTimelineMode.Executions
                ? BuildExecutionTimelineBuckets(snapshot, request, fromUtc, toUtc)
                : BuildOccurrenceTimelineBuckets(snapshot, request, fromUtc, toUtc);

            return Result<JobSchedulerTimelineModel>.Success(new JobSchedulerTimelineModel
            {
                Mode = request.Mode,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                BucketMinutes = request.Bucket,
                Buckets = buckets,
            });
        }
        catch (OperationCanceledException)
        {
            return Result<JobSchedulerTimelineModel>.Failure().WithError(new Error("Dashboard timeline query was canceled."));
        }
        catch (Exception exception)
        {
            return Result<JobSchedulerTimelineModel>.Failure().WithError(new Error($"Dashboard timeline query failed: {exception.Message}"));
        }
    }

    private async Task<QuerySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var definitions = registrations.GetDefinitions().ToArray();
        var runtimeStates = await stores.RuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false);
        var triggerRuntimeStates = await stores.TriggerRuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false);
        var occurrences = await stores.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var executions = await stores.Queries.ListExecutionsAsync(cancellationToken).ConfigureAwait(false);
        var history = await stores.Queries.ListExecutionHistoryAsync(cancellationToken).ConfigureAwait(false);
        var batches = await stores.Queries.ListBatchesAsync(cancellationToken).ConfigureAwait(false);
        var batchOccurrences = await stores.Queries.ListBatchOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var batchHistory = await stores.Queries.ListBatchHistoryAsync(cancellationToken).ConfigureAwait(false);
        var dependencies = await stores.Queries.ListDependenciesAsync(cancellationToken).ConfigureAwait(false);
        var leases = await stores.Queries.ListLeasesAsync(cancellationToken).ConfigureAwait(false);

        return new QuerySnapshot(timeProvider.GetUtcNow(), definitions, runtimeStates, triggerRuntimeStates, occurrences, executions, history, batches, batchOccurrences, batchHistory, dependencies, leases);
    }

    private static JobSchedulerJobModel MapJob(JobDefinition definition, QuerySnapshot snapshot)
    {
        snapshot.JobRuntimeStates.TryGetValue(definition.JobName ?? string.Empty, out var runtimeState);
        var occurrences = snapshot.OccurrencesByJob.GetValueOrDefault(definition.JobName ?? string.Empty, []);
        var executions = snapshot.ExecutionsByJob.GetValueOrDefault(definition.JobName ?? string.Empty, []);
        var lastOccurrence = occurrences.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.DueUtc).FirstOrDefault();
        var lastExecution = executions.OrderByDescending(x => x.CompletedUtc ?? x.StartedUtc).ThenByDescending(x => x.AttemptNumber).FirstOrDefault();
        var hasOrphanedRuntimeState = HasOrphanedRuntimeState(definition, snapshot);

        return new JobSchedulerJobModel
        {
            JobName = definition.JobName,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Group = definition.Group,
            Module = definition.Module,
            JobType = definition.JobType?.FullName ?? definition.JobType?.Name,
            RegisteredEnabled = definition.Enabled,
            EffectiveEnabled = runtimeState?.Enabled ?? definition.Enabled,
            Paused = runtimeState?.Paused ?? false,
            IsOrphanedRuntimeState = false,
            HasOrphanedRuntimeState = hasOrphanedRuntimeState,
            Priority = definition.Priority,
            Timeout = definition.Timeout,
            ConcurrencyLimit = definition.Concurrency?.Limit,
            TriggerCount = definition.Triggers.Count,
            RecurringTriggerCount = definition.Triggers.Count(IsRecurringTrigger),
            PendingOccurrenceCount = occurrences.Count(IsPendingOccurrence),
            RunningOccurrenceCount = occurrences.Count(x => x.Status == JobOccurrenceStatus.Running),
            FailedOccurrenceCount = occurrences.Count(x => x.Status == JobOccurrenceStatus.Failed),
            LastOccurrenceUtc = lastOccurrence?.UpdatedDate,
            LastExecutionUtc = lastExecution?.CompletedUtc ?? lastExecution?.StartedUtc,
            LastExecutionStatus = lastExecution?.Status,
            HasFailedLatestExecution = lastExecution?.Status == JobExecutionStatus.Failed,
            DataType = definition.DataType?.FullName ?? definition.DataType?.Name,
            TargetInstances = definition.TargetInstances ?? [],
            PropertyKeys = GetPropertyKeys(definition.Properties),
            PropertyCount = definition.Properties?.Count ?? 0,
        };
    }

    private static JobSchedulerTriggerModel MapTrigger(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        QuerySnapshot snapshot,
        IJobCronEngine cronEngine,
        IJobCalendarEngine calendarEngine)
    {
        snapshot.TriggerRuntimeStates.TryGetValue(ToTriggerKey(definition.JobName, trigger.TriggerName), out var runtimeState);
        var occurrences = snapshot.OccurrencesByTrigger.GetValueOrDefault(ToTriggerKey(definition.JobName, trigger.TriggerName), []);
        var lastOccurrence = occurrences.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.DueUtc).FirstOrDefault();
        var nextDueUtc = ResolveNextDueUtc(trigger, runtimeState, occurrences, snapshot.NowUtc, cronEngine, calendarEngine);

        return new JobSchedulerTriggerModel
        {
            JobName = definition.JobName,
            TriggerName = trigger.TriggerName,
            TriggerType = trigger.TriggerType,
            RegisteredEnabled = trigger.Enabled,
            EffectiveEnabled = runtimeState?.Enabled ?? trigger.Enabled,
            Paused = runtimeState?.Paused ?? false,
            Priority = trigger.Priority,
            Timeout = trigger.Timeout,
            RetryMaxAttempts = ResolveRetryPolicy(definition, trigger)?.MaxAttempts,
            RetryUsesExponentialBackoff = ResolveRetryPolicy(definition, trigger)?.UseExponentialBackoff ?? false,
            Schedule = trigger.Schedule,
            DueUtc = trigger.DueUtc,
            Delay = trigger.Delay,
            NextDueUtc = nextDueUtc,
            LastMaterializedScheduledUtc = runtimeState?.LastMaterializedScheduledUtc,
            HasMaterializedOccurrence = runtimeState?.HasMaterializedOccurrence ?? false,
            TimeZoneId = trigger.TimeZone?.Id,
            DataType = trigger.DataType?.FullName ?? trigger.DataType?.Name,
            TargetInstances = trigger.TargetInstances?.Count > 0 ? trigger.TargetInstances : definition.TargetInstances ?? [],
            DataPreview = BuildDataPreview(trigger.Data),
            PropertyKeys = GetPropertyKeys(trigger.Properties),
            PropertyCount = trigger.Properties?.Count ?? 0,
            LastOccurrenceUtc = lastOccurrence?.UpdatedDate,
            LastOccurrenceStatus = lastOccurrence?.Status,
        };
    }

    private static JobSchedulerRecurringTriggerModel MapRecurringTrigger(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        QuerySnapshot snapshot,
        IJobCronEngine cronEngine,
        IJobCalendarEngine calendarEngine)
    {
        var model = MapTrigger(definition, trigger, snapshot, cronEngine, calendarEngine);
        return new JobSchedulerRecurringTriggerModel
        {
            JobName = model.JobName,
            TriggerName = model.TriggerName,
            TriggerType = model.TriggerType,
            RegisteredEnabled = model.RegisteredEnabled,
            EffectiveEnabled = model.EffectiveEnabled,
            Paused = model.Paused,
            Priority = model.Priority,
            Timeout = model.Timeout,
            RetryMaxAttempts = model.RetryMaxAttempts,
            RetryUsesExponentialBackoff = model.RetryUsesExponentialBackoff,
            Schedule = model.Schedule,
            DueUtc = model.DueUtc,
            Delay = model.Delay,
            NextDueUtc = model.NextDueUtc,
            LastMaterializedScheduledUtc = model.LastMaterializedScheduledUtc,
            HasMaterializedOccurrence = model.HasMaterializedOccurrence,
            TimeZoneId = model.TimeZoneId,
            DataType = model.DataType,
            DataPreview = model.DataPreview,
            PropertyKeys = model.PropertyKeys,
            PropertyCount = model.PropertyCount,
            LastOccurrenceUtc = model.LastOccurrenceUtc,
            LastOccurrenceStatus = model.LastOccurrenceStatus,
        };
    }

    private static DateTimeOffset? ResolveNextDueUtc(
        JobTriggerDefinition trigger,
        JobTriggerRuntimeState runtimeState,
        IReadOnlyList<JobOccurrence> occurrences,
        DateTimeOffset nowUtc,
        IJobCronEngine cronEngine,
        IJobCalendarEngine calendarEngine)
    {
        var pendingDueUtc = occurrences
            .Where(IsPendingOccurrence)
            .OrderBy(x => x.DueUtc)
            .Select(x => (DateTimeOffset?)x.DueUtc)
            .FirstOrDefault();

        if (pendingDueUtc.HasValue)
        {
            return pendingDueUtc;
        }

        if (runtimeState?.DueUtc is { } runtimeDueUtc)
        {
            return runtimeDueUtc;
        }

        return trigger.TriggerType switch
        {
            JobTriggerType.OneTime => trigger.DueUtc,
            JobTriggerType.Delayed or JobTriggerType.StartupDelay => runtimeState?.ActivatedUtc is { } activatedUtc && trigger.Delay.HasValue
                ? activatedUtc + trigger.Delay.Value
                : null,
            JobTriggerType.Cron when !string.IsNullOrWhiteSpace(trigger.Schedule) =>
                cronEngine.GetNextOccurrenceUtc(trigger.Schedule, nowUtc, trigger.TimeZone ?? TimeZoneInfo.Utc).Value,
            JobTriggerType.Calendar when trigger.Calendar is not null =>
                calendarEngine.GetNextOccurrenceUtc(trigger.Calendar, nowUtc, trigger.TimeZone ?? TimeZoneInfo.Utc).Value,
            _ => null,
        };
    }

    private static JobSchedulerOccurrenceModel MapOccurrence(JobOccurrence occurrence, QuerySnapshot snapshot)
    {
        var executions = snapshot.ExecutionsByOccurrenceId.GetValueOrDefault(occurrence.OccurrenceId, []);
        var lastExecution = executions.OrderByDescending(x => x.AttemptNumber).ThenByDescending(x => x.CompletedUtc ?? x.StartedUtc).FirstOrDefault();
        var lease = snapshot.LeasesByOccurrenceId.GetValueOrDefault(occurrence.OccurrenceId);
        var batchLink = snapshot.BatchOccurrenceByOccurrenceId.GetValueOrDefault(occurrence.OccurrenceId);
        var batch = batchLink is null ? null : snapshot.BatchesById.GetValueOrDefault(batchLink.BatchId);
        var dependencies = snapshot.DependenciesByDependentOccurrenceId.GetValueOrDefault(occurrence.OccurrenceId, []);

        return new JobSchedulerOccurrenceModel
        {
            OccurrenceId = occurrence.OccurrenceId,
            OccurrenceKey = occurrence.OccurrenceKey,
            JobName = occurrence.JobName,
            TriggerName = occurrence.TriggerName,
            TriggerType = occurrence.TriggerType,
            Status = occurrence.Status,
            DueUtc = occurrence.DueUtc,
            ScheduledUtc = occurrence.ScheduledUtc,
            CreatedDate = occurrence.CreatedDate,
            UpdatedDate = occurrence.UpdatedDate,
            CorrelationId = occurrence.CorrelationId,
            CausationId = occurrence.CausationId,
            IdempotencyKey = occurrence.IdempotencyKey,
            ResumeStatus = occurrence.ResumeStatus,
            BlockedReason = occurrence.BlockedReason,
            DependencyCount = dependencies.Count,
            PendingDependencyCount = dependencies.Count(x => x.Status == JobDependencyStatus.Pending),
            FailedDependencyCount = dependencies.Count(x => x.Status == JobDependencyStatus.Failed),
            DataType = occurrence.DataType?.FullName ?? occurrence.DataType?.Name,
            DataPreview = BuildDataPreview(occurrence.Data),
            PropertyKeys = GetPropertyKeys(occurrence.Properties),
            PropertyCount = occurrence.Properties?.Count ?? 0,
            Properties = GetProperties(occurrence.Properties),
            AttemptCount = executions.Count,
            LatestExecutionStatus = lastExecution?.Status,
            LatestExecutionStartedUtc = lastExecution?.StartedUtc,
            LatestExecutionCompletedUtc = lastExecution?.CompletedUtc,
            LatestExecutionDurationSeconds = lastExecution?.CompletedUtc.HasValue == true
                ? (lastExecution.CompletedUtc.Value - lastExecution.StartedUtc).TotalSeconds
                : null,
            ExecutionMessages = executions
                .OrderByDescending(x => x.AttemptNumber)
                .ThenByDescending(x => x.CompletedUtc ?? x.StartedUtc)
                .Select(x => x.Message)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(5)
                .ToArray(),
            LeaseOwnerSchedulerInstanceId = lease?.SchedulerInstanceId,
            BatchInternalId = batch?.BatchId,
            ExternalBatchId = batch?.ExternalBatchId,
        };
    }

    private static JobSchedulerRetryModel MapRetry(JobOccurrence occurrence, QuerySnapshot snapshot)
    {
        var definition = snapshot.DefinitionsByJob.GetValueOrDefault(occurrence.JobName ?? string.Empty);
        if (definition is null)
        {
            return null;
        }

        var trigger = definition.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, occurrence.TriggerName, StringComparison.OrdinalIgnoreCase));
        var retryPolicy = ResolveRetryPolicy(definition, trigger);
        if (retryPolicy is null || retryPolicy.MaxAttempts <= 1)
        {
            return null;
        }

        var executions = snapshot.ExecutionsByOccurrenceId.GetValueOrDefault(occurrence.OccurrenceId, []);
        if (executions.Count == 0 && occurrence.Status != JobOccurrenceStatus.RetryScheduled)
        {
            return null;
        }

        var lastExecution = executions.OrderByDescending(x => x.AttemptNumber).ThenByDescending(x => x.CompletedUtc ?? x.StartedUtc).FirstOrDefault();
        var attemptCount = Math.Max(executions.Count, lastExecution?.AttemptNumber ?? 0);
        var hasRemainingAttempts = attemptCount < retryPolicy.MaxAttempts;

        return new JobSchedulerRetryModel
        {
            OccurrenceId = occurrence.OccurrenceId,
            JobName = occurrence.JobName,
            TriggerName = occurrence.TriggerName,
            CorrelationId = occurrence.CorrelationId,
            OccurrenceStatus = occurrence.Status,
            AttemptCount = attemptCount,
            MaxAttempts = retryPolicy.MaxAttempts,
            HasRemainingAttempts = hasRemainingAttempts,
            NextAttemptNumber = Math.Min(retryPolicy.MaxAttempts, attemptCount + 1),
            RetryDueUtc = occurrence.DueUtc,
            LastFailureMessage = lastExecution?.Message,
            SchedulerInstanceId = lastExecution?.SchedulerInstanceId,
        };
    }

    private static JobSchedulerBatchModel MapBatch(JobBatch batch, QuerySnapshot snapshot)
    {
        var childLinks = snapshot.BatchOccurrencesByBatchId.GetValueOrDefault(batch.BatchId, []);
        return new JobSchedulerBatchModel
        {
            BatchId = batch.BatchId,
            ExternalBatchId = batch.ExternalBatchId,
            Description = batch.Description,
            Status = batch.Status,
            CompletionPolicy = batch.CompletionPolicy,
            CorrelationId = batch.CorrelationId,
            CausationId = batch.CausationId,
            IdempotencyKey = batch.IdempotencyKey,
            AcceptedCount = batch.AcceptedCount,
            SucceededCount = batch.SucceededCount,
            FailedCount = batch.FailedCount,
            CancelledCount = batch.CancelledCount,
            ArchivedCount = batch.ArchivedCount,
            ChildOccurrenceCount = childLinks.Count,
            CreatedDate = batch.CreatedDate,
            UpdatedDate = batch.UpdatedDate,
            CompletedDate = batch.CompletedDate,
            PropertyKeys = GetPropertyKeys(batch.Properties),
            PropertyCount = batch.Properties?.Count ?? 0,
        };
    }

    private static JobSchedulerBatchChildOccurrenceModel MapBatchOccurrence(JobBatch batch, JobBatchOccurrence link, QuerySnapshot snapshot)
    {
        if (!snapshot.OccurrencesById.TryGetValue(link.OccurrenceId, out var occurrence))
        {
            return null;
        }

        var mapped = MapOccurrence(occurrence, snapshot);
        return new JobSchedulerBatchChildOccurrenceModel
        {
            BatchId = batch.BatchId,
            Sequence = link.Sequence,
            ChildStatus = link.ChildStatus,
            OccurrenceId = mapped.OccurrenceId,
            OccurrenceKey = mapped.OccurrenceKey,
            JobName = mapped.JobName,
            TriggerName = mapped.TriggerName,
            TriggerType = mapped.TriggerType,
            Status = mapped.Status,
            DueUtc = mapped.DueUtc,
            ScheduledUtc = mapped.ScheduledUtc,
            CreatedDate = mapped.CreatedDate,
            UpdatedDate = mapped.UpdatedDate,
            CorrelationId = mapped.CorrelationId,
            CausationId = mapped.CausationId,
            IdempotencyKey = mapped.IdempotencyKey,
            ResumeStatus = mapped.ResumeStatus,
            BlockedReason = mapped.BlockedReason,
            DependencyCount = mapped.DependencyCount,
            PendingDependencyCount = mapped.PendingDependencyCount,
            FailedDependencyCount = mapped.FailedDependencyCount,
            DataType = mapped.DataType,
            DataPreview = mapped.DataPreview,
            PropertyKeys = mapped.PropertyKeys,
            PropertyCount = mapped.PropertyCount,
            AttemptCount = mapped.AttemptCount,
            LatestExecutionStatus = mapped.LatestExecutionStatus,
            LeaseOwnerSchedulerInstanceId = mapped.LeaseOwnerSchedulerInstanceId,
            BatchInternalId = mapped.BatchInternalId,
            ExternalBatchId = mapped.ExternalBatchId,
        };
    }

    private static JobSchedulerBatchHistoryModel MapBatchHistory(JobBatchHistoryEntry entry)
        => new()
        {
            HistoryId = entry.HistoryId,
            BatchId = entry.BatchId,
            ExternalBatchId = entry.ExternalBatchId,
            EventName = entry.EventName,
            BatchStatus = entry.BatchStatus,
            Message = entry.Message,
            SchedulerInstanceId = entry.SchedulerInstanceId,
            PropertyKeys = GetPropertyKeys(entry.Properties),
            PropertyCount = entry.Properties?.Count ?? 0,
            RecordedAt = entry.RecordedAt,
        };

    private static JobSchedulerDependencyModel MapDependency(JobOccurrenceDependency dependency, QuerySnapshot snapshot)
    {
        snapshot.OccurrencesById.TryGetValue(dependency.DependentOccurrenceId, out var dependent);
        snapshot.OccurrencesById.TryGetValue(dependency.PrerequisiteOccurrenceId, out var prerequisite);

        return new JobSchedulerDependencyModel
        {
            DependencyId = dependency.DependencyId,
            DependentOccurrenceId = dependency.DependentOccurrenceId,
            DependentJobName = dependent?.JobName,
            DependentTriggerName = dependent?.TriggerName,
            DependentStatus = dependent?.Status,
            PrerequisiteOccurrenceId = dependency.PrerequisiteOccurrenceId,
            PrerequisiteJobName = prerequisite?.JobName,
            PrerequisiteTriggerName = prerequisite?.TriggerName,
            PrerequisiteStatus = prerequisite?.Status,
            RequiredStatuses = dependency.RequiredStatuses ?? [],
            Status = dependency.Status,
            FailurePolicy = dependency.FailurePolicy,
            Reason = dependency.Reason,
            PropertyKeys = GetPropertyKeys(dependency.Properties),
            PropertyCount = dependency.Properties?.Count ?? 0,
            CreatedDate = dependency.CreatedDate,
            UpdatedDate = dependency.UpdatedDate,
        };
    }

    private static JobSchedulerExecutionModel MapExecution(JobExecution execution, QuerySnapshot snapshot)
    {
        snapshot.OccurrencesById.TryGetValue(execution.OccurrenceId, out var occurrence);

        return new JobSchedulerExecutionModel
        {
            ExecutionId = execution.ExecutionId,
            OccurrenceId = execution.OccurrenceId,
            JobName = execution.JobName,
            TriggerName = execution.TriggerName,
            AttemptNumber = execution.AttemptNumber,
            Status = execution.Status,
            SchedulerInstanceId = execution.SchedulerInstanceId,
            StartedUtc = execution.StartedUtc,
            CompletedUtc = execution.CompletedUtc,
            DurationSeconds = execution.CompletedUtc.HasValue ? (execution.CompletedUtc.Value - execution.StartedUtc).TotalSeconds : null,
            Message = execution.Message,
            CorrelationId = occurrence?.CorrelationId,
            IdempotencyKey = occurrence?.IdempotencyKey,
        };
    }

    private static JobSchedulerExecutionHistoryModel MapHistory(JobExecutionHistoryEntry history)
    {
        return new JobSchedulerExecutionHistoryModel
        {
            HistoryId = history.HistoryId,
            OccurrenceId = history.OccurrenceId,
            ExecutionId = history.ExecutionId,
            JobName = history.JobName,
            TriggerName = history.TriggerName,
            SchedulerInstanceId = history.SchedulerInstanceId,
            EventName = history.EventName,
            OccurrenceStatus = history.OccurrenceStatus,
            ExecutionStatus = history.ExecutionStatus,
            Message = history.Message,
            RecordedAt = history.RecordedAt,
            RecordedBy = history.RecordedBy,
            PropertyKeys = GetPropertyKeys(history.Properties),
            PropertyCount = history.Properties?.Count ?? 0,
        };
    }

    private static JobSchedulerLeaseModel MapLease(JobLeaseRecord lease, QuerySnapshot snapshot)
    {
        snapshot.OccurrencesById.TryGetValue(lease.OccurrenceId, out var occurrence);

        return new JobSchedulerLeaseModel
        {
            OccurrenceId = lease.OccurrenceId,
            JobName = occurrence?.JobName,
            TriggerName = occurrence?.TriggerName,
            SchedulerInstanceId = lease.SchedulerInstanceId,
            Status = GetLeaseStatus(lease, snapshot.NowUtc),
            AcquiredUtc = lease.AcquiredUtc,
            RenewedUtc = lease.RenewedUtc,
            ExpiresUtc = lease.ExpiresUtc,
            RenewalCount = lease.RenewalCount,
        };
    }

    private static List<JobSchedulerServerModel> BuildServers(QuerySnapshot snapshot)
    {
        var identifiers = snapshot.Leases.Select(x => x.SchedulerInstanceId)
            .Concat(snapshot.Executions.Select(x => x.SchedulerInstanceId))
            .Concat(snapshot.History.Select(x => x.SchedulerInstanceId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return identifiers.Select(identifier =>
        {
            var leases = snapshot.Leases.Where(x => string.Equals(x.SchedulerInstanceId, identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            var executions = snapshot.Executions.Where(x => string.Equals(x.SchedulerInstanceId, identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            var history = snapshot.History.Where(x => string.Equals(x.SchedulerInstanceId, identifier, StringComparison.OrdinalIgnoreCase)).ToList();
            var activeLeaseCount = leases.Count(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Active);
            var expiredLeaseCount = leases.Count(x => GetLeaseStatus(x, snapshot.NowUtc) == JobSchedulerLeaseStatus.Expired);
            var status = activeLeaseCount > 0
                ? JobSchedulerServerStatus.Active
                : expiredLeaseCount > 0
                    ? JobSchedulerServerStatus.Expired
                    : JobSchedulerServerStatus.Observed;

            return new JobSchedulerServerModel
            {
                SchedulerInstanceId = identifier,
                Status = status,
                LastSeenUtc = leases.Select(x => x.RenewedUtc ?? x.AcquiredUtc)
                    .Concat(executions.Select(x => x.CompletedUtc ?? x.StartedUtc))
                    .Concat(history.Select(x => x.RecordedAt))
                    .DefaultIfEmpty()
                    .Max(),
                ActiveLeaseCount = activeLeaseCount,
                ExpiredLeaseCount = expiredLeaseCount,
                ExecutionCount = executions.Count,
                LastExecutionUtc = executions.Select(x => (DateTimeOffset?)(x.CompletedUtc ?? x.StartedUtc)).DefaultIfEmpty().Max(),
                LastHistoryUtc = history.Select(x => (DateTimeOffset?)x.RecordedAt).DefaultIfEmpty().Max(),
            };
        }).ToList();
    }

    private IReadOnlyList<JobSchedulerTimelineBucketModel> BuildOccurrenceTimelineBuckets(QuerySnapshot snapshot, JobSchedulerTimelineRequest request, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var statusFilter = ResolveOccurrenceTimelineStatuses(request);
        var items = snapshot.History
            .Where(x => x.OccurrenceStatus.HasValue)
            .Where(x => Matches(x.JobName, request.JobName))
            .Where(x => Matches(x.TriggerName, request.TriggerName))
            .Where(x => Matches(x.SchedulerInstanceId, request.SchedulerInstanceId))
            .Where(x => x.RecordedAt >= fromUtc && x.RecordedAt <= toUtc)
            .Where(x => statusFilter.Count == 0 || statusFilter.Contains(x.OccurrenceStatus!.Value))
            .Select(x => new TimelineEntry(x.RecordedAt, x.OccurrenceStatus!.Value.ToString()))
            .ToList();

        return BuildTimelineBuckets(items, fromUtc, toUtc, request.Bucket);
    }

    private IReadOnlyList<JobSchedulerTimelineBucketModel> BuildExecutionTimelineBuckets(QuerySnapshot snapshot, JobSchedulerTimelineRequest request, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var statusFilter = ResolveExecutionTimelineStatuses(request);
        var items = snapshot.History
            .Where(x => x.ExecutionStatus.HasValue)
            .Where(x => Matches(x.JobName, request.JobName))
            .Where(x => Matches(x.TriggerName, request.TriggerName))
            .Where(x => Matches(x.SchedulerInstanceId, request.SchedulerInstanceId))
            .Where(x => x.RecordedAt >= fromUtc && x.RecordedAt <= toUtc)
            .Where(x => statusFilter.Count == 0 || statusFilter.Contains(x.ExecutionStatus!.Value))
            .Select(x => new TimelineEntry(x.RecordedAt, x.ExecutionStatus!.Value.ToString()))
            .ToList();

        return BuildTimelineBuckets(items, fromUtc, toUtc, request.Bucket);
    }

    private static IReadOnlyList<JobSchedulerTimelineBucketModel> BuildTimelineBuckets(IEnumerable<TimelineEntry> items, DateTimeOffset fromUtc, DateTimeOffset toUtc, int bucketMinutes)
    {
        var buckets = new List<JobSchedulerTimelineBucketModel>();
        for (var bucketStart = fromUtc; bucketStart < toUtc; bucketStart = bucketStart.AddMinutes(bucketMinutes))
        {
            var bucketEnd = bucketStart.AddMinutes(bucketMinutes);
            var counts = items
                .Where(x => x.RecordedAt >= bucketStart && x.RecordedAt < bucketEnd)
                .GroupBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (long)group.LongCount(), StringComparer.OrdinalIgnoreCase);

            buckets.Add(new JobSchedulerTimelineBucketModel
            {
                BucketStartUtc = bucketStart,
                BucketEndUtc = bucketEnd,
                CountsByStatus = counts,
            });
        }

        return buckets;
    }

    private IEnumerable<JobOccurrence> FilterMetricsOccurrences(QuerySnapshot snapshot, JobSchedulerMetricsRequest request)
    {
        var occurrenceStatusFilter = request.OccurrenceStatuses.SafeNull().ToHashSet();
        return snapshot.Occurrences
            .Where(x => Matches(x.JobName, request.JobName))
            .Where(x => Matches(x.TriggerName, request.TriggerName))
            .Where(x => !request.TriggerType.HasValue || x.TriggerType == request.TriggerType.Value)
            .Where(x => occurrenceStatusFilter.Count == 0 || occurrenceStatusFilter.Contains(x.Status))
            .Where(x => !request.DueFrom.HasValue || x.DueUtc >= request.DueFrom.Value)
            .Where(x => !request.DueTo.HasValue || x.DueUtc <= request.DueTo.Value)
            .Where(x => string.IsNullOrWhiteSpace(request.SchedulerInstanceId)
                || string.Equals(snapshot.LeasesByOccurrenceId.GetValueOrDefault(x.OccurrenceId)?.SchedulerInstanceId, request.SchedulerInstanceId, StringComparison.OrdinalIgnoreCase)
                || snapshot.ExecutionsByOccurrenceId.GetValueOrDefault(x.OccurrenceId, []).Any(exec => string.Equals(exec.SchedulerInstanceId, request.SchedulerInstanceId, StringComparison.OrdinalIgnoreCase)));
    }

    private IEnumerable<JobExecution> FilterMetricsExecutions(QuerySnapshot snapshot, JobSchedulerMetricsRequest request)
    {
        var executionStatusFilter = request.ExecutionStatuses.SafeNull().ToHashSet();
        return snapshot.Executions
            .Where(x => Matches(x.JobName, request.JobName))
            .Where(x => Matches(x.TriggerName, request.TriggerName))
            .Where(x => !request.TriggerType.HasValue || MatchesExecutionTriggerType(snapshot, x.OccurrenceId, request.TriggerType.Value))
            .Where(x => executionStatusFilter.Count == 0 || executionStatusFilter.Contains(x.Status))
            .Where(x => Matches(x.SchedulerInstanceId, request.SchedulerInstanceId))
            .Where(x => !request.DueFrom.HasValue || MatchesExecutionDueLowerBound(snapshot, x.OccurrenceId, request.DueFrom.Value))
            .Where(x => !request.DueTo.HasValue || MatchesExecutionDueUpperBound(snapshot, x.OccurrenceId, request.DueTo.Value))
            .Where(x => !request.CompletedFrom.HasValue || (x.CompletedUtc.HasValue && x.CompletedUtc.Value >= request.CompletedFrom.Value))
            .Where(x => !request.CompletedTo.HasValue || (x.CompletedUtc.HasValue && x.CompletedUtc.Value <= request.CompletedTo.Value));
    }

    private static IReadOnlyList<JobSchedulerJobModel> BuildJobModels(QuerySnapshot snapshot, bool includeOrphanedRuntimeState)
    {
        var models = snapshot.Definitions.Select(definition => MapJob(definition, snapshot)).ToList();
        if (includeOrphanedRuntimeState)
        {
            models.AddRange(BuildOrphanedRuntimeStateJobs(snapshot));
        }

        return models;
    }

    private static IReadOnlyList<JobSchedulerJobModel> BuildOrphanedRuntimeStateJobs(QuerySnapshot snapshot)
    {
        var orphanedJobNames = snapshot.RuntimeStates
            .Where(x => !snapshot.DefinitionsByJob.ContainsKey(x.JobName ?? string.Empty))
            .Select(x => x.JobName)
            .Concat(snapshot.TriggerRuntimeStateRecords
                .Where(x => !snapshot.DefinitionsByJob.ContainsKey(x.JobName ?? string.Empty))
                .Select(x => x.JobName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return orphanedJobNames.Select(jobName => CreateOrphanedRuntimeStateJobModel(jobName, snapshot)).ToArray();
    }

    private static JobSchedulerJobModel CreateOrphanedRuntimeStateJobModel(string jobName, QuerySnapshot snapshot)
    {
        snapshot.JobRuntimeStates.TryGetValue(jobName ?? string.Empty, out var runtimeState);
        var orphanedTriggers = snapshot.TriggerRuntimeStateRecords
            .Where(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.State)
            .ToArray();

        return new JobSchedulerJobModel
        {
            JobName = jobName,
            DisplayName = jobName,
            Description = "Orphaned runtime state",
            RegisteredEnabled = false,
            EffectiveEnabled = runtimeState?.Enabled ?? orphanedTriggers.All(x => x.Enabled ?? true),
            Paused = runtimeState?.Paused ?? orphanedTriggers.Any(x => x.Paused),
            IsOrphanedRuntimeState = true,
            HasOrphanedRuntimeState = true,
            HasFailedLatestExecution = false,
            PropertyKeys = [],
        };
    }

    private static bool HasOrphanedRuntimeState(JobDefinition definition, QuerySnapshot snapshot)
        => snapshot.TriggerRuntimeStateRecords.Any(x =>
            string.Equals(x.JobName, definition.JobName, StringComparison.OrdinalIgnoreCase)
            && definition.Triggers.All(trigger => !string.Equals(trigger.TriggerName, x.TriggerName, StringComparison.OrdinalIgnoreCase)));

    private static JobSchedulerJobFacetCountsModel BuildJobFacets(IReadOnlyCollection<JobSchedulerJobModel> jobs)
        => new()
        {
            EnabledCount = jobs.LongCount(x => x.EffectiveEnabled),
            DisabledCount = jobs.LongCount(x => !x.EffectiveEnabled),
            PausedCount = jobs.LongCount(x => x.Paused),
            OrphanedRuntimeStateCount = jobs.LongCount(x => x.IsOrphanedRuntimeState || x.HasOrphanedRuntimeState),
            FailedLatestExecutionCount = jobs.LongCount(x => x.HasFailedLatestExecution),
        };

    private static bool MatchesExecutionWindow(QuerySnapshot snapshot, Guid occurrenceId, DateTimeOffset? startedFrom, DateTimeOffset? startedTo, DateTimeOffset? completedFrom, DateTimeOffset? completedTo)
    {
        if (!startedFrom.HasValue && !startedTo.HasValue && !completedFrom.HasValue && !completedTo.HasValue)
        {
            return true;
        }

        return snapshot.ExecutionsByOccurrenceId.GetValueOrDefault(occurrenceId, []).Any(execution =>
            (!startedFrom.HasValue || execution.StartedUtc >= startedFrom.Value)
            && (!startedTo.HasValue || execution.StartedUtc <= startedTo.Value)
            && (!completedFrom.HasValue || (execution.CompletedUtc.HasValue && execution.CompletedUtc.Value >= completedFrom.Value))
            && (!completedTo.HasValue || (execution.CompletedUtc.HasValue && execution.CompletedUtc.Value <= completedTo.Value)));
    }

    private static bool MatchesExecutionTriggerType(QuerySnapshot snapshot, Guid occurrenceId, JobTriggerType triggerType)
        => snapshot.OccurrencesById.TryGetValue(occurrenceId, out var occurrence) && occurrence.TriggerType == triggerType;

    private static bool MatchesExecutionDueLowerBound(QuerySnapshot snapshot, Guid occurrenceId, DateTimeOffset dueFrom)
        => snapshot.OccurrencesById.TryGetValue(occurrenceId, out var occurrence) && occurrence.DueUtc >= dueFrom;

    private static bool MatchesExecutionDueUpperBound(QuerySnapshot snapshot, Guid occurrenceId, DateTimeOffset dueTo)
        => snapshot.OccurrencesById.TryGetValue(occurrenceId, out var occurrence) && occurrence.DueUtc <= dueTo;

    private static HashSet<JobOccurrenceStatus> ResolveOccurrenceTimelineStatuses(JobSchedulerTimelineRequest request)
    {
        var typed = request.OccurrenceStatuses.SafeNull().ToHashSet();
        if (typed.Count > 0 || !request.Statuses.SafeNull().Any())
        {
            return typed;
        }

        return request.Statuses
            .Where(x => Enum.TryParse<JobOccurrenceStatus>(x, true, out _))
            .Select(x => Enum.Parse<JobOccurrenceStatus>(x, true))
            .ToHashSet();
    }

    private static HashSet<JobExecutionStatus> ResolveExecutionTimelineStatuses(JobSchedulerTimelineRequest request)
    {
        var typed = request.ExecutionStatuses.SafeNull().ToHashSet();
        if (typed.Count > 0 || !request.Statuses.SafeNull().Any())
        {
            return typed;
        }

        return request.Statuses
            .Where(x => Enum.TryParse<JobExecutionStatus>(x, true, out _))
            .Select(x => Enum.Parse<JobExecutionStatus>(x, true))
            .ToHashSet();
    }

    private IEnumerable<JobLeaseRecord> FilterMetricsLeases(QuerySnapshot snapshot, JobSchedulerMetricsRequest request)
    {
        return snapshot.Leases
            .Where(x => Matches(x.SchedulerInstanceId, request.SchedulerInstanceId))
            .Where(x => !request.FromUtc.HasValue || x.AcquiredUtc >= request.FromUtc.Value)
            .Where(x => !request.ToUtc.HasValue || x.AcquiredUtc <= request.ToUtc.Value)
            .Where(x =>
            {
                if (string.IsNullOrWhiteSpace(request.JobName) && string.IsNullOrWhiteSpace(request.TriggerName))
                {
                    return true;
                }

                if (!snapshot.OccurrencesById.TryGetValue(x.OccurrenceId, out var occurrence))
                {
                    return false;
                }

                return Matches(occurrence.JobName, request.JobName) && Matches(occurrence.TriggerName, request.TriggerName);
            });
    }

    private static Result ValidatePagedRequest(JobSchedulerPagedQueryRequest request, IReadOnlyCollection<string> supportedSorts)
    {
        if (request is null)
        {
            return Result.Success();
        }

        if (request.Skip < 0)
        {
            return Result.Failure().WithError(new Error("Skip must be zero or greater."));
        }

        if (request.Take < 0)
        {
            return Result.Failure().WithError(new Error("Take must be zero or greater."));
        }

        if (!string.IsNullOrWhiteSpace(request.SortBy)
            && !supportedSorts.Contains(request.SortBy.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure().WithError(new Error($"Unsupported sort field '{request.SortBy}'."));
        }

        return Result.Success();
    }

    private static ResultPaged<TModel> Page<TModel>(List<TModel> source, JobSchedulerPagedQueryRequest request, Func<IEnumerable<TModel>, string, bool, IEnumerable<TModel>> order)
    {
        var take = request.Take <= 0 ? 50 : request.Take;
        var skip = Math.Max(0, request.Skip);
        var sorted = order(source, request.SortBy, request.SortDescending).ToList();
        var page = (skip / take) + 1;
        var items = sorted.Skip(skip).Take(take).ToArray();

        return ResultPaged<TModel>.Success(items, sorted.Count, page, take);
    }

    private static IEnumerable<JobSchedulerJobModel> SortJobs(IEnumerable<JobSchedulerJobModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "JobName" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "DISPLAYNAME" => source.OrderByDescending(x => x.DisplayName ?? string.Empty).ThenBy(x => x.JobName),
                "GROUP" => source.OrderByDescending(x => x.Group ?? string.Empty).ThenBy(x => x.JobName),
                "MODULE" => source.OrderByDescending(x => x.Module ?? string.Empty).ThenBy(x => x.JobName),
                "LASTEXECUTIONUTC" => source.OrderByDescending(x => x.LastExecutionUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName),
                "LASTOCCURRENCEUTC" => source.OrderByDescending(x => x.LastOccurrenceUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName),
                _ => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.DisplayName ?? string.Empty),
            }
            : normalized.ToUpperInvariant() switch
            {
                "DISPLAYNAME" => source.OrderBy(x => x.DisplayName ?? string.Empty).ThenBy(x => x.JobName),
                "GROUP" => source.OrderBy(x => x.Group ?? string.Empty).ThenBy(x => x.JobName),
                "MODULE" => source.OrderBy(x => x.Module ?? string.Empty).ThenBy(x => x.JobName),
                "LASTEXECUTIONUTC" => source.OrderBy(x => x.LastExecutionUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName),
                "LASTOCCURRENCEUTC" => source.OrderBy(x => x.LastOccurrenceUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName),
                _ => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.DisplayName ?? string.Empty),
            };
    }

    private static IEnumerable<JobSchedulerTriggerModel> SortTriggers(IEnumerable<JobSchedulerTriggerModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "JobName" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.JobName),
                "TRIGGERTYPE" => source.OrderByDescending(x => x.TriggerType).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                "NEXTDUEUTC" => source.OrderByDescending(x => x.NextDueUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                "LASTOCCURRENCEUTC" => source.OrderByDescending(x => x.LastOccurrenceUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                _ => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.TriggerName ?? string.Empty),
            }
            : normalized.ToUpperInvariant() switch
            {
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.JobName),
                "TRIGGERTYPE" => source.OrderBy(x => x.TriggerType).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                "NEXTDUEUTC" => source.OrderBy(x => x.NextDueUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                "LASTOCCURRENCEUTC" => source.OrderBy(x => x.LastOccurrenceUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.JobName).ThenBy(x => x.TriggerName),
                _ => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.TriggerName ?? string.Empty),
            };
    }

    private static IEnumerable<JobSchedulerRecurringTriggerModel> SortRecurringTriggers(IEnumerable<JobSchedulerRecurringTriggerModel> source, string sortBy, bool descending)
        => SortTriggers(source, sortBy, descending).Cast<JobSchedulerRecurringTriggerModel>();

    private static IEnumerable<JobSchedulerOccurrenceModel> SortOccurrences(IEnumerable<JobSchedulerOccurrenceModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "DueUtc" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "CREATEDDATE" => source.OrderByDescending(x => x.CreatedDate).ThenBy(x => x.OccurrenceId),
                "UPDATEDDATE" => source.OrderByDescending(x => x.UpdatedDate).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "STATUS" => source.OrderByDescending(x => x.Status).ThenBy(x => x.OccurrenceId),
                _ => source.OrderByDescending(x => x.DueUtc).ThenBy(x => x.OccurrenceId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "CREATEDDATE" => source.OrderBy(x => x.CreatedDate).ThenBy(x => x.OccurrenceId),
                "UPDATEDDATE" => source.OrderBy(x => x.UpdatedDate).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "STATUS" => source.OrderBy(x => x.Status).ThenBy(x => x.OccurrenceId),
                _ => source.OrderBy(x => x.DueUtc).ThenBy(x => x.OccurrenceId),
            };
    }

    private static IEnumerable<JobSchedulerRetryModel> SortRetries(IEnumerable<JobSchedulerRetryModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "RetryDueUtc" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "ATTEMPTCOUNT" => source.OrderByDescending(x => x.AttemptCount).ThenBy(x => x.OccurrenceId),
                _ => source.OrderByDescending(x => x.RetryDueUtc).ThenBy(x => x.OccurrenceId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "ATTEMPTCOUNT" => source.OrderBy(x => x.AttemptCount).ThenBy(x => x.OccurrenceId),
                _ => source.OrderBy(x => x.RetryDueUtc).ThenBy(x => x.OccurrenceId),
            };
    }

    private static IEnumerable<JobSchedulerBatchModel> SortBatches(IEnumerable<JobSchedulerBatchModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "CreatedDate" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "UPDATEDDATE" => source.OrderByDescending(x => x.UpdatedDate).ThenBy(x => x.BatchId),
                "COMPLETEDDATE" => source.OrderByDescending(x => x.CompletedDate ?? DateTimeOffset.MinValue).ThenBy(x => x.BatchId),
                "BATCHID" => source.OrderByDescending(x => x.ExternalBatchId ?? string.Empty).ThenBy(x => x.BatchId),
                "STATUS" => source.OrderByDescending(x => x.Status).ThenBy(x => x.BatchId),
                _ => source.OrderByDescending(x => x.CreatedDate).ThenBy(x => x.BatchId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "UPDATEDDATE" => source.OrderBy(x => x.UpdatedDate).ThenBy(x => x.BatchId),
                "COMPLETEDDATE" => source.OrderBy(x => x.CompletedDate ?? DateTimeOffset.MinValue).ThenBy(x => x.BatchId),
                "BATCHID" => source.OrderBy(x => x.ExternalBatchId ?? string.Empty).ThenBy(x => x.BatchId),
                "STATUS" => source.OrderBy(x => x.Status).ThenBy(x => x.BatchId),
                _ => source.OrderBy(x => x.CreatedDate).ThenBy(x => x.BatchId),
            };
    }

    private static IEnumerable<JobSchedulerBatchChildOccurrenceModel> SortBatchOccurrences(IEnumerable<JobSchedulerBatchChildOccurrenceModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "Sequence" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "DUEUTC" => source.OrderByDescending(x => x.DueUtc).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "STATUS" => source.OrderByDescending(x => x.ChildStatus).ThenBy(x => x.OccurrenceId),
                _ => source.OrderByDescending(x => x.Sequence ?? int.MinValue).ThenBy(x => x.OccurrenceId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "DUEUTC" => source.OrderBy(x => x.DueUtc).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "STATUS" => source.OrderBy(x => x.ChildStatus).ThenBy(x => x.OccurrenceId),
                _ => source.OrderBy(x => x.Sequence ?? int.MaxValue).ThenBy(x => x.OccurrenceId),
            };
    }

    private static IEnumerable<JobSchedulerBatchHistoryModel> SortBatchHistory(IEnumerable<JobSchedulerBatchHistoryModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "RecordedAt" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "EVENTNAME" => source.OrderByDescending(x => x.EventName ?? string.Empty).ThenBy(x => x.HistoryId),
                "BATCHSTATUS" => source.OrderByDescending(x => x.BatchStatus).ThenBy(x => x.HistoryId),
                "SCHEDULERINSTANCEID" => source.OrderByDescending(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.HistoryId),
                _ => source.OrderByDescending(x => x.RecordedAt).ThenBy(x => x.HistoryId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "EVENTNAME" => source.OrderBy(x => x.EventName ?? string.Empty).ThenBy(x => x.HistoryId),
                "BATCHSTATUS" => source.OrderBy(x => x.BatchStatus).ThenBy(x => x.HistoryId),
                "SCHEDULERINSTANCEID" => source.OrderBy(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.HistoryId),
                _ => source.OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId),
            };
    }

    private static IEnumerable<JobSchedulerDependencyModel> SortDependencies(IEnumerable<JobSchedulerDependencyModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "CreatedDate" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "UPDATEDDATE" => source.OrderByDescending(x => x.UpdatedDate).ThenBy(x => x.DependencyId),
                "STATUS" => source.OrderByDescending(x => x.Status).ThenBy(x => x.DependencyId),
                "DEPENDENTJOBNAME" => source.OrderByDescending(x => x.DependentJobName ?? string.Empty).ThenBy(x => x.DependencyId),
                "PREREQUISITEJOBNAME" => source.OrderByDescending(x => x.PrerequisiteJobName ?? string.Empty).ThenBy(x => x.DependencyId),
                _ => source.OrderByDescending(x => x.CreatedDate).ThenBy(x => x.DependencyId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "UPDATEDDATE" => source.OrderBy(x => x.UpdatedDate).ThenBy(x => x.DependencyId),
                "STATUS" => source.OrderBy(x => x.Status).ThenBy(x => x.DependencyId),
                "DEPENDENTJOBNAME" => source.OrderBy(x => x.DependentJobName ?? string.Empty).ThenBy(x => x.DependencyId),
                "PREREQUISITEJOBNAME" => source.OrderBy(x => x.PrerequisiteJobName ?? string.Empty).ThenBy(x => x.DependencyId),
                _ => source.OrderBy(x => x.CreatedDate).ThenBy(x => x.DependencyId),
            };
    }

    private static IEnumerable<JobSchedulerExecutionModel> SortExecutions(IEnumerable<JobSchedulerExecutionModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "StartedUtc" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "COMPLETEDUTC" => source.OrderByDescending(x => x.CompletedUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.ExecutionId),
                "ATTEMPTNUMBER" => source.OrderByDescending(x => x.AttemptNumber).ThenBy(x => x.ExecutionId),
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.ExecutionId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.ExecutionId),
                "STATUS" => source.OrderByDescending(x => x.Status).ThenBy(x => x.ExecutionId),
                "SCHEDULERINSTANCEID" => source.OrderByDescending(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.ExecutionId),
                _ => source.OrderByDescending(x => x.StartedUtc).ThenBy(x => x.ExecutionId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "COMPLETEDUTC" => source.OrderBy(x => x.CompletedUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.ExecutionId),
                "ATTEMPTNUMBER" => source.OrderBy(x => x.AttemptNumber).ThenBy(x => x.ExecutionId),
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.ExecutionId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.ExecutionId),
                "STATUS" => source.OrderBy(x => x.Status).ThenBy(x => x.ExecutionId),
                "SCHEDULERINSTANCEID" => source.OrderBy(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.ExecutionId),
                _ => source.OrderBy(x => x.StartedUtc).ThenBy(x => x.ExecutionId),
            };
    }

    private static IEnumerable<JobSchedulerExecutionHistoryModel> SortHistory(IEnumerable<JobSchedulerExecutionHistoryModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "RecordedAt" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.HistoryId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.HistoryId),
                "EVENTNAME" => source.OrderByDescending(x => x.EventName ?? string.Empty).ThenBy(x => x.HistoryId),
                "SCHEDULERINSTANCEID" => source.OrderByDescending(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.HistoryId),
                _ => source.OrderByDescending(x => x.RecordedAt).ThenBy(x => x.HistoryId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.HistoryId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.HistoryId),
                "EVENTNAME" => source.OrderBy(x => x.EventName ?? string.Empty).ThenBy(x => x.HistoryId),
                "SCHEDULERINSTANCEID" => source.OrderBy(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.HistoryId),
                _ => source.OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId),
            };
    }

    private static IEnumerable<JobSchedulerLeaseModel> SortLeases(IEnumerable<JobSchedulerLeaseModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "ExpiresUtc" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "ACQUIREDUTC" => source.OrderByDescending(x => x.AcquiredUtc).ThenBy(x => x.OccurrenceId),
                "RENEWEDUTC" => source.OrderByDescending(x => x.RenewedUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.OccurrenceId),
                "SCHEDULERINSTANCEID" => source.OrderByDescending(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderByDescending(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderByDescending(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                _ => source.OrderByDescending(x => x.ExpiresUtc).ThenBy(x => x.OccurrenceId),
            }
            : normalized.ToUpperInvariant() switch
            {
                "ACQUIREDUTC" => source.OrderBy(x => x.AcquiredUtc).ThenBy(x => x.OccurrenceId),
                "RENEWEDUTC" => source.OrderBy(x => x.RenewedUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.OccurrenceId),
                "SCHEDULERINSTANCEID" => source.OrderBy(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "JOBNAME" => source.OrderBy(x => x.JobName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                "TRIGGERNAME" => source.OrderBy(x => x.TriggerName ?? string.Empty).ThenBy(x => x.OccurrenceId),
                _ => source.OrderBy(x => x.ExpiresUtc).ThenBy(x => x.OccurrenceId),
            };
    }

    private static IEnumerable<JobSchedulerServerModel> SortServers(IEnumerable<JobSchedulerServerModel> source, string sortBy, bool descending)
    {
        var normalized = string.IsNullOrWhiteSpace(sortBy) ? "LastSeenUtc" : sortBy.Trim();
        return descending
            ? normalized.ToUpperInvariant() switch
            {
                "SCHEDULERINSTANCEID" => source.OrderByDescending(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.LastSeenUtc ?? DateTimeOffset.MinValue),
                "STATUS" => source.OrderByDescending(x => x.Status).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                "LASTEXECUTIONUTC" => source.OrderByDescending(x => x.LastExecutionUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                "LASTHISTORYUTC" => source.OrderByDescending(x => x.LastHistoryUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                _ => source.OrderByDescending(x => x.LastSeenUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
            }
            : normalized.ToUpperInvariant() switch
            {
                "SCHEDULERINSTANCEID" => source.OrderBy(x => x.SchedulerInstanceId ?? string.Empty).ThenBy(x => x.LastSeenUtc ?? DateTimeOffset.MinValue),
                "STATUS" => source.OrderBy(x => x.Status).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                "LASTEXECUTIONUTC" => source.OrderBy(x => x.LastExecutionUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                "LASTHISTORYUTC" => source.OrderBy(x => x.LastHistoryUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
                _ => source.OrderBy(x => x.LastSeenUtc ?? DateTimeOffset.MinValue).ThenBy(x => x.SchedulerInstanceId ?? string.Empty),
            };
    }

    private static bool Matches(string actual, string filter)
        => string.IsNullOrWhiteSpace(filter) || string.Equals(actual, filter, StringComparison.OrdinalIgnoreCase);

    private static bool IsRecurringTrigger(JobTriggerDefinition trigger)
        => trigger.TriggerType is JobTriggerType.Cron or JobTriggerType.Calendar;

    private static bool IsPendingOccurrence(JobOccurrence occurrence)
        => occurrence.Status is JobOccurrenceStatus.Pending or JobOccurrenceStatus.Scheduled or JobOccurrenceStatus.Due or JobOccurrenceStatus.Blocked or JobOccurrenceStatus.RetryScheduled;

    private static JobRetryPolicy ResolveRetryPolicy(JobDefinition definition, JobTriggerDefinition trigger)
        => trigger?.RetryPolicy ?? definition?.RetryPolicy;

    private static string ToTriggerKey(string jobName, string triggerName)
        => $"{jobName ?? string.Empty}\u001f{triggerName ?? string.Empty}";

    private static IReadOnlyList<string> GetPropertyKeys(PropertyBag properties)
        => properties?.Keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray() ?? [];

    private static IReadOnlyDictionary<string, string> GetProperties(PropertyBag properties)
        => properties?
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string BuildDataPreview(object value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return $"String (Length={text.Length})";
        }

        var type = value.GetType();
        if (type.IsPrimitive || value is decimal or DateTime or DateTimeOffset or Guid or TimeSpan or Enum)
        {
            return type.Name;
        }

        if (value is ICollection collection)
        {
            return $"{type.Name} (Count={collection.Count})";
        }

        return type.Name;
    }

    private static JobSchedulerLeaseStatus GetLeaseStatus(JobLeaseRecord lease, DateTimeOffset nowUtc)
        => lease.ExpiresUtc > nowUtc ? JobSchedulerLeaseStatus.Active : JobSchedulerLeaseStatus.Expired;

    private sealed record TimelineEntry(DateTimeOffset RecordedAt, string Status);

    private sealed class QuerySnapshot
    {
        public QuerySnapshot(
            DateTimeOffset nowUtc,
            IReadOnlyList<JobDefinition> definitions,
            IReadOnlyList<JobRuntimeState> runtimeStates,
            IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)> triggerRuntimeStates,
            IReadOnlyList<JobOccurrence> occurrences,
            IReadOnlyList<JobExecution> executions,
            IReadOnlyList<JobExecutionHistoryEntry> history,
            IReadOnlyList<JobBatch> batches,
            IReadOnlyList<JobBatchOccurrence> batchOccurrences,
            IReadOnlyList<JobBatchHistoryEntry> batchHistory,
            IReadOnlyList<JobOccurrenceDependency> dependencies,
            IReadOnlyList<JobLeaseRecord> leases)
        {
            this.NowUtc = nowUtc;
            this.Definitions = definitions;
            this.RuntimeStates = runtimeStates;
            this.TriggerRuntimeStateRecords = triggerRuntimeStates;
            this.Occurrences = occurrences;
            this.Executions = executions;
            this.History = history;
            this.Batches = batches;
            this.BatchOccurrences = batchOccurrences;
            this.BatchHistory = batchHistory;
            this.Dependencies = dependencies;
            this.Leases = leases;

            this.DefinitionsByJob = definitions.ToDictionary(x => x.JobName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            this.JobRuntimeStates = runtimeStates.ToDictionary(x => x.JobName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            this.TriggerRuntimeStates = triggerRuntimeStates.ToDictionary(x => ToTriggerKey(x.JobName, x.TriggerName), x => x.State, StringComparer.OrdinalIgnoreCase);
            this.OccurrencesById = occurrences.ToDictionary(x => x.OccurrenceId);
            this.OccurrencesByJob = occurrences.GroupBy(x => x.JobName ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => (IReadOnlyList<JobOccurrence>)x.ToList(), StringComparer.OrdinalIgnoreCase);
            this.OccurrencesByTrigger = occurrences.GroupBy(x => ToTriggerKey(x.JobName, x.TriggerName), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => (IReadOnlyList<JobOccurrence>)x.ToList(), StringComparer.OrdinalIgnoreCase);
            this.ExecutionsByOccurrenceId = executions.GroupBy(x => x.OccurrenceId).ToDictionary(x => x.Key, x => (IReadOnlyList<JobExecution>)x.ToList());
            this.ExecutionsByJob = executions.GroupBy(x => x.JobName ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => (IReadOnlyList<JobExecution>)x.ToList(), StringComparer.OrdinalIgnoreCase);
            this.BatchesById = batches.ToDictionary(x => x.BatchId);
            this.BatchOccurrencesByBatchId = batchOccurrences.GroupBy(x => x.BatchId).ToDictionary(x => x.Key, x => (IReadOnlyList<JobBatchOccurrence>)x.ToList());
            this.BatchOccurrenceByOccurrenceId = batchOccurrences.GroupBy(x => x.OccurrenceId).ToDictionary(x => x.Key, x => x.OrderBy(y => y.Sequence ?? int.MaxValue).ThenBy(y => y.CreatedDate).First());
            this.BatchHistoryByBatchId = batchHistory.GroupBy(x => x.BatchId).ToDictionary(x => x.Key, x => (IReadOnlyList<JobBatchHistoryEntry>)x.OrderBy(y => y.RecordedAt).ThenBy(y => y.HistoryId).ToList());
            this.DependenciesByDependentOccurrenceId = dependencies.GroupBy(x => x.DependentOccurrenceId).ToDictionary(x => x.Key, x => (IReadOnlyList<JobOccurrenceDependency>)x.OrderBy(y => y.CreatedDate).ThenBy(y => y.DependencyId).ToList());
            this.LeasesByOccurrenceId = leases.GroupBy(x => x.OccurrenceId).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.ExpiresUtc).ThenByDescending(y => y.RenewedUtc ?? y.AcquiredUtc).First());
        }

        public DateTimeOffset NowUtc { get; }

        public IReadOnlyList<JobDefinition> Definitions { get; }

        public IReadOnlyList<JobRuntimeState> RuntimeStates { get; }

        public IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)> TriggerRuntimeStateRecords { get; }

        public IReadOnlyList<JobOccurrence> Occurrences { get; }

        public IReadOnlyList<JobExecution> Executions { get; }

        public IReadOnlyList<JobExecutionHistoryEntry> History { get; }

        public IReadOnlyList<JobBatch> Batches { get; }

        public IReadOnlyList<JobBatchOccurrence> BatchOccurrences { get; }

        public IReadOnlyList<JobBatchHistoryEntry> BatchHistory { get; }

        public IReadOnlyList<JobOccurrenceDependency> Dependencies { get; }

        public IReadOnlyList<JobLeaseRecord> Leases { get; }

        public IReadOnlyDictionary<string, JobDefinition> DefinitionsByJob { get; }

        public IReadOnlyDictionary<string, JobRuntimeState> JobRuntimeStates { get; }

        public IReadOnlyDictionary<string, JobTriggerRuntimeState> TriggerRuntimeStates { get; }

        public IReadOnlyDictionary<Guid, JobOccurrence> OccurrencesById { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<JobOccurrence>> OccurrencesByJob { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<JobOccurrence>> OccurrencesByTrigger { get; }

        public IReadOnlyDictionary<Guid, IReadOnlyList<JobExecution>> ExecutionsByOccurrenceId { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<JobExecution>> ExecutionsByJob { get; }

        public IReadOnlyDictionary<Guid, JobBatch> BatchesById { get; }

        public IReadOnlyDictionary<Guid, IReadOnlyList<JobBatchOccurrence>> BatchOccurrencesByBatchId { get; }

        public IReadOnlyDictionary<Guid, JobBatchOccurrence> BatchOccurrenceByOccurrenceId { get; }

        public IReadOnlyDictionary<Guid, IReadOnlyList<JobBatchHistoryEntry>> BatchHistoryByBatchId { get; }

        public IReadOnlyDictionary<Guid, IReadOnlyList<JobOccurrenceDependency>> DependenciesByDependentOccurrenceId { get; }

        public IReadOnlyDictionary<Guid, JobLeaseRecord> LeasesByOccurrenceId { get; }
    }
}
