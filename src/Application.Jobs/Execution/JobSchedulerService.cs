// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Collections.Concurrent;
using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides the in-memory inline job runtime implementation.
/// </summary>
public class JobSchedulerService(
    TimeProvider timeProvider,
    IServiceScopeFactory scopeFactory,
    JobRegistrationStore registrations,
    IJobTriggerEvaluator triggerEvaluator,
    IJobStoreProvider storeProvider,
    ISerializer serializer,
    JobEventSourceRegistry eventSources,
    JobSchedulerHostedOptions options = null,
    IEnumerable<IJobSchedulerExceptionHandler> exceptionHandlers = null,
    IHostEnvironment hostEnvironment = null) : IJobSchedulerService
{
    private readonly string schedulerInstanceId = (options ?? new JobSchedulerHostedOptions()).ResolveSchedulerInstanceId(hostEnvironment);
    private readonly ConcurrentDictionary<Guid, ActiveExecutionState> activeExecutions = [];
    private readonly SemaphoreSlim jobConcurrencyGate = new(1, 1);
    private readonly IJobSchedulerExceptionHandler[] exceptionHandlers = exceptionHandlers?.ToArray() ?? [];
    private readonly JobSchedulerHostedOptions options = options ?? new JobSchedulerHostedOptions();

    internal string SchedulerInstanceId => this.schedulerInstanceId;

    internal int ActiveExecutionCount => this.activeExecutions.Count;

    internal async Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        await this.ReconcileAllDependenciesAsync(cancellationToken).ConfigureAwait(false);
        var nowUtc = timeProvider.GetUtcNow();
        var expiredLeases = await storeProvider.Leases.ListExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        var recoveredCount = 0;

        foreach (var lease in expiredLeases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var occurrence = await storeProvider.Occurrences.GetAsync(lease.OccurrenceId, cancellationToken).ConfigureAwait(false);
            await storeProvider.Leases.ReleaseAsync(lease.OccurrenceId, lease.SchedulerInstanceId, lease.OwnershipToken, cancellationToken).ConfigureAwait(false);
            if (occurrence is null)
            {
                continue;
            }

            if (occurrence.Status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived or JobOccurrenceStatus.Paused)
            {
                continue;
            }

            var recoveredStatus = GetRecoveredOccurrenceStatus(occurrence, nowUtc);
            var updated = occurrence with
            {
                Status = recoveredStatus,
                UpdatedDate = nowUtc,
            };

            await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(updated, null, "LeaseExpired", updated.Status, null, lease.SchedulerInstanceId, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(updated, null, "OccurrenceRecovered", updated.Status, null, null, cancellationToken).ConfigureAwait(false);
            recoveredCount++;
        }

        return recoveredCount;
    }

    /// <inheritdoc />
    public async Task<Result<JobDispatchResult>> DispatchAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("dispatch", typeof(TJob).Name);
        var definition = this.ResolveJobDefinition(typeof(TJob));
        if (definition is null)
        {
            return TraceManagementResult(activity, "dispatch", Result<JobDispatchResult>.Failure().WithError(new ValidationError($"The job type '{typeof(TJob).FullName}' is not registered.")), typeof(TJob).Name);
        }

        return TraceManagementResult(activity, "dispatch", await this.DispatchInternalAsync(definition, data, options, waitForCompletion: false, cancellationToken).ConfigureAwait(false), definition.JobName);
    }

    /// <inheritdoc />
    public async Task<Result<JobDispatchResult>> DispatchAsync(
        string jobName,
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("dispatch", jobName);
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return TraceManagementResult(activity, "dispatch", Result<JobDispatchResult>.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered.")), jobName);
        }

        return TraceManagementResult(activity, "dispatch", await this.DispatchInternalAsync(definition, data, options, waitForCompletion: false, cancellationToken).ConfigureAwait(false), definition.JobName);
    }

    /// <inheritdoc />
    public async Task<Result<JobExecutionResult>> DispatchAndWaitAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("dispatch-and-wait", typeof(TJob).Name);
        var definition = this.ResolveJobDefinition(typeof(TJob));
        if (definition is null)
        {
            return TraceManagementResult(activity, "dispatch-and-wait", Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The job type '{typeof(TJob).FullName}' is not registered.")), typeof(TJob).Name);
        }

        options ??= new JobDispatchOptions();
        using var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var dispatch = await this.DispatchPreparedAsync(definition, data, options, true, linkedCts.Token).ConfigureAwait(false);
        if (dispatch.IsFailure)
        {
            return TraceManagementResult(activity, "dispatch-and-wait", Result<JobExecutionResult>.Failure().WithErrors(dispatch.Errors).WithMessages(dispatch.Messages), definition.JobName);
        }

        return TraceManagementResult(activity, "dispatch-and-wait", dispatch.Value.ExecutionResult is null
            ? Result<JobExecutionResult>.Failure().WithError(new Error("The inline execution result was not produced."))
            : Result<JobExecutionResult>.Success(dispatch.Value.ExecutionResult, dispatch.Value.ExecutionResult.Messages), definition.JobName);
    }

    /// <inheritdoc />
    public async Task<Result<JobExecutionResult>> DispatchAndWaitAsync(
        string jobName,
        object data = null,
        JobDispatchOptions options = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("dispatch-and-wait", jobName);
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return TraceManagementResult(activity, "dispatch-and-wait", Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered.")), jobName);
        }

        options ??= new JobDispatchOptions();
        using var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var dispatch = await this.DispatchPreparedAsync(definition, data, options, true, linkedCts.Token).ConfigureAwait(false);
        if (dispatch.IsFailure)
        {
            return TraceManagementResult(activity, "dispatch-and-wait", Result<JobExecutionResult>.Failure().WithErrors(dispatch.Errors).WithMessages(dispatch.Messages), definition.JobName);
        }

        return TraceManagementResult(activity, "dispatch-and-wait", dispatch.Value.ExecutionResult is null
            ? Result<JobExecutionResult>.Failure().WithError(new Error("The inline execution result was not produced."))
            : Result<JobExecutionResult>.Success(dispatch.Value.ExecutionResult, dispatch.Value.ExecutionResult.Messages), definition.JobName);
    }

    /// <inheritdoc />
    public async Task<Result> CancelOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("cancel-occurrence", occurrenceId: occurrenceId);
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return TraceManagementResult(activity, "cancel-occurrence", Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found.")), occurrenceId: occurrenceId);
        }

        if (this.activeExecutions.TryGetValue(occurrenceId, out var active))
        {
            active.CancelRequested = true;
            active.Reason = string.IsNullOrWhiteSpace(reason) ? "Execution was cancelled." : reason.Trim();
            active.CancellationSource.Cancel();
            await this.AppendHistoryAsync(occurrence, active.ExecutionId, "OccurrenceCancelRequested", JobOccurrenceStatus.Running, null, reason, cancellationToken).ConfigureAwait(false);
            return TraceManagementResult(activity, "cancel-occurrence", Result.Success(), occurrence.JobName, occurrence.TriggerName, occurrenceId);
        }

        if (occurrence.Status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived)
        {
            return TraceManagementResult(activity, "cancel-occurrence", Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' cannot be cancelled from status '{occurrence.Status}'.")), occurrence.JobName, occurrence.TriggerName, occurrenceId);
        }

        var updated = occurrence with
        {
            Status = JobOccurrenceStatus.Cancelled,
            UpdatedDate = timeProvider.GetUtcNow(),
        };

        await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updated, null, "OccurrenceCancelled", JobOccurrenceStatus.Cancelled, null, reason, cancellationToken).ConfigureAwait(false);
        return TraceManagementResult(activity, "cancel-occurrence", Result.Success(), occurrence.JobName, occurrence.TriggerName, occurrenceId);
    }

    /// <inheritdoc />
    public async Task<Result> InterruptOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!this.activeExecutions.TryGetValue(occurrenceId, out var active))
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is not running and cannot be interrupted."));
        }

        active.InterruptRequested = true;
    active.Reason = string.IsNullOrWhiteSpace(reason) ? "Execution was interrupted." : reason.Trim();
        active.CancellationSource.Cancel();

        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is not null)
        {
            await this.AppendHistoryAsync(occurrence, active.ExecutionId, "OccurrenceInterruptRequested", JobOccurrenceStatus.Running, null, reason, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> EnableJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("enable-job", jobName);
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return TraceManagementResult(activity, "enable-job", Result.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered.")), jobName);
        }

        var existing = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
        if (existing is { Enabled: true } || (existing is not { Enabled: false } && definition.Enabled))
        {
            return TraceManagementResult(activity, "enable-job", Result.Failure().WithError(new ValidationError($"The job '{definition.JobName}' is already enabled.")), definition.JobName);
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.RuntimeStates.UpsertAsync(
            new JobRuntimeState
            {
                JobName = definition.JobName,
                Enabled = true,
                Paused = existing?.Paused ?? false,
                CreatedDate = existing?.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, null, "JobEnabled", reason, cancellationToken).ConfigureAwait(false);
        return TraceManagementResult(activity, "enable-job", Result.Success(), definition.JobName);
    }

    /// <inheritdoc />
    public async Task<Result> DisableJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return Result.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
        if (existing is { Enabled: false } || (existing is not { Enabled: true } && !definition.Enabled))
        {
            return Result.Failure().WithError(new ValidationError($"The job '{definition.JobName}' is already disabled."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.RuntimeStates.UpsertAsync(
            new JobRuntimeState
            {
                JobName = definition.JobName,
                Enabled = false,
                Paused = existing?.Paused ?? false,
                CreatedDate = existing?.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, null, "JobDisabled", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> PauseJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return Result.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
        if (existing is { Paused: true })
        {
            return Result.Failure().WithError(new ValidationError($"The job '{definition.JobName}' is already paused."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.RuntimeStates.UpsertAsync(
            new JobRuntimeState
            {
                JobName = definition.JobName,
                Enabled = existing?.Enabled,
                Paused = true,
                CreatedDate = existing?.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, null, "JobPaused", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResumeJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        if (definition is null)
        {
            return Result.Failure().WithError(new ValidationError($"The job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
        if (existing is not { Paused: true })
        {
            return Result.Failure().WithError(new ValidationError($"The job '{definition.JobName}' is not paused."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.RuntimeStates.UpsertAsync(
            existing with
            {
                Paused = false,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, null, "JobResumed", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> PauseTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || trigger is null)
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{triggerName}' on job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
            ?? JobTriggerRuntimeState.Empty;
        if (existing.Paused)
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{trigger.TriggerName}' on job '{definition.JobName}' is already paused."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.TriggerRuntimeStates.UpsertAsync(
            definition.JobName,
            trigger.TriggerName,
            existing with
            {
                Paused = true,
                CreatedDate = existing.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, trigger.TriggerName, "TriggerPaused", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResumeTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || trigger is null)
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{triggerName}' on job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false);
        if (existing is not { Paused: true })
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{trigger.TriggerName}' on job '{definition.JobName}' is not paused."));
        }

        await storeProvider.TriggerRuntimeStates.UpsertAsync(
            definition.JobName,
            trigger.TriggerName,
            existing with
            {
                Paused = false,
                UpdatedDate = timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, trigger.TriggerName, "TriggerResumed", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> EnableTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || trigger is null)
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{triggerName}' on job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
            ?? JobTriggerRuntimeState.Empty;
        if (existing.Enabled == true || (existing.Enabled is not false && trigger.Enabled))
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{trigger.TriggerName}' on job '{definition.JobName}' is already enabled."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.TriggerRuntimeStates.UpsertAsync(
            definition.JobName,
            trigger.TriggerName,
            existing with
            {
                Enabled = true,
                CreatedDate = existing.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, trigger.TriggerName, "TriggerEnabled", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DisableTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var definition = this.ResolveJobDefinition(jobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || trigger is null)
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{triggerName}' on job '{jobName}' is not registered."));
        }

        var existing = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
            ?? JobTriggerRuntimeState.Empty;
        if (existing.Enabled == false || (existing.Enabled is not true && !trigger.Enabled))
        {
            return Result.Failure().WithError(new ValidationError($"The trigger '{trigger.TriggerName}' on job '{definition.JobName}' is already disabled."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        await storeProvider.TriggerRuntimeStates.UpsertAsync(
            definition.JobName,
            trigger.TriggerName,
            existing with
            {
                Enabled = false,
                CreatedDate = existing.CreatedDate ?? nowUtc,
                UpdatedDate = nowUtc,
            },
            cancellationToken).ConfigureAwait(false);

        await this.AppendRegistrationHistoryAsync(definition.JobName, trigger.TriggerName, "TriggerDisabled", reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> PauseOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        if (occurrence.Status == JobOccurrenceStatus.Paused)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is already paused."));
        }

        if (occurrence.Status is JobOccurrenceStatus.Running or JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' cannot be paused from status '{occurrence.Status}'."));
        }

        var updated = occurrence with
        {
            Status = JobOccurrenceStatus.Paused,
            ResumeStatus = occurrence.Status,
            UpdatedDate = timeProvider.GetUtcNow(),
        };

        await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updated, null, "OccurrencePaused", JobOccurrenceStatus.Paused, null, reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResumeOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        if (occurrence.Status != JobOccurrenceStatus.Paused)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is not paused."));
        }

        var updated = occurrence with
        {
            Status = GetResumedOccurrenceStatus(occurrence, timeProvider.GetUtcNow()),
            ResumeStatus = null,
            UpdatedDate = timeProvider.GetUtcNow(),
        };

        await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updated, null, "OccurrenceResumed", updated.Status, null, reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RetryOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        if (occurrence.Status != JobOccurrenceStatus.Failed)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' cannot be retried from status '{occurrence.Status}'."));
        }

        var definition = this.ResolveJobDefinition(occurrence.JobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, occurrence.TriggerName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || trigger is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' no longer maps to an active job and trigger registration."));
        }

        var retryDueUtc = timeProvider.GetUtcNow();
        var updated = occurrence with
        {
            Status = JobOccurrenceStatus.RetryScheduled,
            DueUtc = retryDueUtc,
            ResumeStatus = null,
            UpdatedDate = retryDueUtc,
        };
        await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updated, null, "OccurrenceRetryRequested", JobOccurrenceStatus.RetryScheduled, null, reason, cancellationToken).ConfigureAwait(false);
        await this.ReconcileDependentsForPrerequisiteAsync(updated, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        if (occurrence.Status == JobOccurrenceStatus.Archived)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is already archived."));
        }

        if (occurrence.Status is not (JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled))
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' cannot be archived from status '{occurrence.Status}'."));
        }

        var updated = occurrence with
        {
            Status = JobOccurrenceStatus.Archived,
            UpdatedDate = timeProvider.GetUtcNow(),
        };
        await this.UpdateOccurrenceAsync(updated, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updated, null, "OccurrenceArchived", JobOccurrenceStatus.Archived, null, reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ReleaseOccurrenceLeaseAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        var lease = await storeProvider.Leases.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return Result.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' does not have an active lease."));
        }

        await storeProvider.Leases.RemoveAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(occurrence, null, "OccurrenceLeaseReleased", occurrence.Status, null, reason, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result<JobBulkOperationResult>> RetryOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default)
        => this.ExecuteOccurrenceOperationAsync(occurrenceIds, reason, this.RetryOccurrenceAsync, cancellationToken);

    /// <inheritdoc />
    public Task<Result<JobBulkOperationResult>> CancelOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default)
        => this.ExecuteOccurrenceOperationAsync(occurrenceIds, reason, this.CancelOccurrenceAsync, cancellationToken);

    /// <inheritdoc />
    public Task<Result<JobBulkOperationResult>> ArchiveOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default)
        => this.ExecuteOccurrenceOperationAsync(occurrenceIds, reason, this.ArchiveOccurrenceAsync, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<JobBatchDispatchResult>> CreateBatchAsync(JobBatchCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError("A batch create request is required."));
        }

        var existing = await this.FindExistingBatchAsync(request.BatchId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var externalBatchId = NormalizeBatchId(request.BatchId);
        var correlationId = NormalizeCorrelationId(request.CorrelationId);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey, externalBatchId);
        var batch = new JobBatch
        {
            BatchId = Guid.NewGuid(),
            ExternalBatchId = externalBatchId,
            Description = request.Description,
            Status = JobBatchStatus.Completed,
            CompletionPolicy = request.CompletionPolicy,
            Properties = CloneProperties(request.Properties),
            CorrelationId = correlationId,
            CausationId = string.IsNullOrWhiteSpace(request.CausationId) ? correlationId : request.CausationId.Trim(),
            IdempotencyKey = idempotencyKey,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
            CompletedDate = nowUtc,
        };

        var created = await storeProvider.Batches.TryCreateAsync(batch, [], cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            existing = await this.FindExistingBatchAsync(externalBatchId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError($"The batch '{externalBatchId}' could not be created."))
                : Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        await this.AppendBatchHistoryAsync(batch, "BatchCreated", "Batch created.", cancellationToken).ConfigureAwait(false);
        return Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(batch, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result<JobBatchDispatchResult>> DispatchBatchAsync(JobBatchDispatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError("A batch dispatch request is required."));
        }

        if (request.Items is null)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError("Batch dispatch items are required."));
        }

        var existing = await this.FindExistingBatchAsync(request.BatchId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var externalBatchId = NormalizeBatchId(request.BatchId);
        var correlationId = NormalizeCorrelationId(request.CorrelationId);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey, externalBatchId);
        var batch = new JobBatch
        {
            BatchId = Guid.NewGuid(),
            ExternalBatchId = externalBatchId,
            Description = request.Description,
            Status = request.Items.Count == 0 ? JobBatchStatus.Completed : JobBatchStatus.Processing,
            CompletionPolicy = request.CompletionPolicy,
            Properties = CloneProperties(request.Properties),
            CorrelationId = correlationId,
            CausationId = string.IsNullOrWhiteSpace(request.CausationId) ? correlationId : request.CausationId.Trim(),
            IdempotencyKey = idempotencyKey,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
            CompletedDate = request.Items.Count == 0 ? nowUtc : null,
        };

        var prepared = await this.PrepareBatchChildrenAsync(batch, request, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result<JobBatchDispatchResult>.Failure().WithErrors(prepared.Errors).WithMessages(prepared.Messages);
        }

        var memberships = prepared.Value.Children.Select(x => new JobBatchOccurrence
        {
            BatchId = batch.BatchId,
            OccurrenceId = x.Occurrence.OccurrenceId,
            ChildStatus = x.Occurrence.Status,
            Sequence = x.Sequence,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        }).ToArray();

        var created = await storeProvider.Batches.TryCreateWithChildrenAsync(
            batch,
            prepared.Value.Children.Select(x => x.Occurrence).ToArray(),
            memberships,
            cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            existing = await this.FindExistingBatchAsync(externalBatchId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError($"The batch '{externalBatchId}' could not be dispatched atomically."))
                : Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        await this.AppendBatchHistoryAsync(batch, "BatchCreated", "Batch created.", cancellationToken).ConfigureAwait(false);
        await this.AppendBatchHistoryAsync(batch, "BatchDispatched", $"Batch dispatched with {memberships.Length} child occurrence(s).", cancellationToken, new PropertyBag
        {
            ["acceptedCount"] = memberships.Length,
        }).ConfigureAwait(false);
        await this.FinalizePreparedBatchChildrenAsync(prepared.Value.Children.Where(x => x.Created), cancellationToken).ConfigureAwait(false);
        batch = await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(batch, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result<JobBatchDispatchResult>> AttachToBatchAsync(string batchId, JobBatchDispatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError("A batch attach request is required."));
        }

        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        if (batch.Status == JobBatchStatus.Archived || batch.ArchivedDate.HasValue)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError($"The batch '{batch.ExternalBatchId}' is archived and cannot accept additional child occurrences."));
        }

        var prepared = await this.PrepareBatchChildrenAsync(batch, request, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result<JobBatchDispatchResult>.Failure().WithErrors(prepared.Errors).WithMessages(prepared.Messages);
        }

        var nowUtc = timeProvider.GetUtcNow();
        var memberships = prepared.Value.Children.Select(x => new JobBatchOccurrence
        {
            BatchId = batch.BatchId,
            OccurrenceId = x.Occurrence.OccurrenceId,
            ChildStatus = x.Occurrence.Status,
            Sequence = x.Sequence,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        }).ToArray();

        var attached = await storeProvider.Batches.TryAttachChildrenAsync(
            batch.BatchId,
            prepared.Value.Children.Select(x => x.Occurrence).ToArray(),
            memberships,
            cancellationToken).ConfigureAwait(false);
        if (!attached)
        {
            return Result<JobBatchDispatchResult>.Failure().WithError(new ValidationError($"The batch '{batch.ExternalBatchId}' could not be updated atomically."));
        }

        await this.AppendBatchHistoryAsync(batch, "BatchChildrenAttached", $"Attached {memberships.Length} child occurrence(s).", cancellationToken, new PropertyBag
        {
            ["attachedCount"] = memberships.Length,
        }).ConfigureAwait(false);
        await this.FinalizePreparedBatchChildrenAsync(prepared.Value.Children.Where(x => x.Created), cancellationToken).ConfigureAwait(false);
        batch = await this.RefreshBatchAsync(batch with { UpdatedDate = nowUtc }, cancellationToken).ConfigureAwait(false);
        return Result<JobBatchDispatchResult>.Success(await this.BuildBatchDispatchResultAsync(batch, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result<JobBulkOperationResult>> RetryBatchAsync(string batchId, string reason = null, CancellationToken cancellationToken = default)
    {
        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result<JobBulkOperationResult>.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        var members = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        var eligibleIds = new List<Guid>();
        foreach (var member in members)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(member.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence?.Status == JobOccurrenceStatus.Failed)
            {
                eligibleIds.Add(occurrence.OccurrenceId);
            }
        }

        var result = await this.ExecuteBatchOccurrenceOperationAsync(eligibleIds, reason, this.RetryOccurrenceAsync, cancellationToken).ConfigureAwait(false);
        await this.AppendBatchOperationHistoryAsync(batch, "BatchRetryRequested", reason, result, cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result<JobBulkOperationResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<JobBulkOperationResult>> CancelBatchAsync(string batchId, string reason = null, CancellationToken cancellationToken = default)
    {
        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result<JobBulkOperationResult>.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        if (!batch.CancellationRequestedDate.HasValue)
        {
            var nowUtc = timeProvider.GetUtcNow();
            batch = batch with
            {
                CancellationRequestedDate = nowUtc,
                UpdatedDate = nowUtc,
            };
            await storeProvider.Batches.UpdateAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        var members = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        var eligibleIds = new List<Guid>();
        foreach (var member in members)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(member.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is null)
            {
                continue;
            }

            if (occurrence.Status is not (JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived))
            {
                eligibleIds.Add(occurrence.OccurrenceId);
            }
        }

        var result = await this.ExecuteBatchOccurrenceOperationAsync(eligibleIds, reason, this.CancelOccurrenceAsync, cancellationToken).ConfigureAwait(false);
        await this.AppendBatchOperationHistoryAsync(batch, "BatchCancelRequested", reason, result, cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result<JobBulkOperationResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result> PauseBatchAsync(string batchId, string reason = null, CancellationToken cancellationToken = default)
    {
        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        var members = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        foreach (var member in members)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(member.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is null || occurrence.Status == JobOccurrenceStatus.Paused)
            {
                continue;
            }

            if (occurrence.Status is JobOccurrenceStatus.Pending or JobOccurrenceStatus.Scheduled or JobOccurrenceStatus.Due or JobOccurrenceStatus.RetryScheduled or JobOccurrenceStatus.Blocked)
            {
                var paused = await this.PauseOccurrenceAsync(occurrence.OccurrenceId, reason, cancellationToken).ConfigureAwait(false);
                if (paused.IsFailure)
                {
                    return Result.Failure().WithErrors(paused.Errors).WithMessages(paused.Messages);
                }
            }
        }

        await this.AppendBatchHistoryAsync(batch, "BatchPauseRequested", reason ?? "Batch pause requested.", cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResumeBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        var members = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        foreach (var member in members)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(member.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence?.Status != JobOccurrenceStatus.Paused)
            {
                continue;
            }

            var resumed = await this.ResumeOccurrenceAsync(occurrence.OccurrenceId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (resumed.IsFailure)
            {
                return Result.Failure().WithErrors(resumed.Errors).WithMessages(resumed.Messages);
            }
        }

        await this.AppendBatchHistoryAsync(batch, "BatchResumeRequested", "Batch resume requested.", cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveBatchAsync(string batchId, string reason = null, CancellationToken cancellationToken = default)
    {
        var batch = await this.FindBatchByPublicIdAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure().WithError(new ValidationError($"The batch '{batchId}' was not found."));
        }

        if (batch.Status == JobBatchStatus.Archived || batch.ArchivedDate.HasValue)
        {
            return Result.Failure().WithError(new ValidationError($"The batch '{batch.ExternalBatchId}' is already archived."));
        }

        var members = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        var occurrences = new List<JobOccurrence>(members.Count);
        foreach (var member in members)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(member.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is null)
            {
                continue;
            }

            occurrences.Add(occurrence);
            if (occurrence.Status is not (JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived))
            {
                return Result.Failure().WithError(new ValidationError($"The batch '{batch.ExternalBatchId}' cannot be archived while occurrence '{occurrence.OccurrenceId}' is '{occurrence.Status}'."));
            }
        }

        foreach (var occurrence in occurrences.Where(x => x.Status != JobOccurrenceStatus.Archived))
        {
            var archived = await this.ArchiveOccurrenceAsync(occurrence.OccurrenceId, reason, cancellationToken).ConfigureAwait(false);
            if (archived.IsFailure)
            {
                return archived;
            }
        }

        var nowUtc = timeProvider.GetUtcNow();
        batch = batch with
        {
            Status = JobBatchStatus.Archived,
            ArchivedDate = nowUtc,
            UpdatedDate = nowUtc,
        };
        await storeProvider.Batches.UpdateAsync(batch, cancellationToken).ConfigureAwait(false);
        await this.AppendBatchHistoryAsync(batch, "BatchArchived", reason ?? "Batch archived.", cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal async Task<Result<IReadOnlyList<JobOccurrence>>> MaterializeScheduledOccurrencesAsync(
        DateTimeOffset schedulerStartedUtc,
        int maxCatchUpOccurrences,
        CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartMaterializationActivity(this.schedulerInstanceId);
        var createdOccurrences = new List<JobOccurrence>();
        var eventMaterialization = await this.MaterializeAcceptedEventsAsync(createdOccurrences, maxCatchUpOccurrences, cancellationToken).ConfigureAwait(false);
        if (eventMaterialization.IsFailure)
        {
            activity?.SetStatus(ActivityStatusCode.Error, BuildMessage(eventMaterialization.Errors, eventMaterialization.Messages));
            return Result<IReadOnlyList<JobOccurrence>>.Failure().WithErrors(eventMaterialization.Errors).WithMessages(eventMaterialization.Messages);
        }

        foreach (var definition in registrations.GetDefinitions())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jobState = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
            if (jobState is { Enabled: false } || (jobState is not { Enabled: true } && !definition.Enabled) || jobState is { Paused: true })
            {
                continue;
            }

            foreach (var trigger in definition.Triggers.Where(x => x.TriggerType is not (JobTriggerType.Manual or JobTriggerType.Event)))
            {
                var triggerState = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
                    ?? JobTriggerRuntimeState.Empty;
                if (triggerState is { Enabled: false } || (triggerState is not { Enabled: true } && !trigger.Enabled) || triggerState is { Paused: true })
                {
                    continue;
                }

                var evaluation = triggerEvaluator.Materialize(
                    definition,
                    trigger,
                    new JobTriggerEvaluationRequest
                    {
                        RuntimeState = triggerState,
                        SchedulerStartedUtc = schedulerStartedUtc,
                        ActivationUtc = schedulerStartedUtc,
                        MaxCatchUpOccurrences = maxCatchUpOccurrences,
                    });

                if (evaluation.IsFailure)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, BuildMessage(evaluation.Errors, evaluation.Messages));
                    return Result<IReadOnlyList<JobOccurrence>>.Failure().WithErrors(evaluation.Errors).WithMessages(evaluation.Messages);
                }

                await storeProvider.TriggerRuntimeStates.UpsertAsync(definition.JobName, trigger.TriggerName, evaluation.Value.RuntimeState, cancellationToken).ConfigureAwait(false);
                foreach (var materialized in evaluation.Value.Occurrences)
                {
                    var occurrence = await this.PersistOccurrenceAsync(materialized, cancellationToken).ConfigureAwait(false);
                    if (occurrence is not null)
                    {
                        createdOccurrences.Add(occurrence);
                    }
                }

                JobSchedulerInstrumentation.RecordMaterializedOccurrences(this.schedulerInstanceId, definition.JobName, trigger.TriggerName, trigger.TriggerType, evaluation.Value.Occurrences.Count);
            }
        }

        activity?.SetTag("jobs.materialization.created_count", createdOccurrences.Count);

        return Result<IReadOnlyList<JobOccurrence>>.Success(createdOccurrences);
    }

    internal async Task<Result> MaterializeAcceptedEventsAsync(
        ICollection<JobOccurrence> createdOccurrences,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        foreach (var definition in registrations.GetDefinitions())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jobState = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
            if (jobState is { Enabled: false } || (jobState is not { Enabled: true } && !definition.Enabled) || jobState is { Paused: true })
            {
                continue;
            }

            foreach (var trigger in definition.Triggers.Where(x => x.TriggerType == JobTriggerType.Event))
            {
                var triggerState = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
                    ?? JobTriggerRuntimeState.Empty;
                if (triggerState is { Enabled: false } || (triggerState is not { Enabled: true } && !trigger.Enabled) || triggerState is { Paused: true })
                {
                    continue;
                }

                if (!eventSources.IsRegistered(trigger.EventSource))
                {
                    return Result.Failure().WithError(new ValidationError($"The event trigger '{definition.JobName}/{trigger.TriggerName}' requires the '{trigger.EventSource}' event adapter, but no adapter is registered."));
                }

                var acceptedEvents = await storeProvider.AcceptedEvents.ListPendingAsync(
                    trigger.EventSource,
                    trigger.EventDataType,
                    triggerState.LastAcceptedEventUtc,
                    triggerState.LastAcceptedEventId,
                    batchSize,
                    cancellationToken).ConfigureAwait(false);

                if (acceptedEvents.Count == 0)
                {
                    continue;
                }

                foreach (var acceptedEvent in acceptedEvents)
                {
                    var occurrence = await this.PersistOccurrenceAsync(this.CreateEventOccurrence(definition, trigger, acceptedEvent), cancellationToken).ConfigureAwait(false);
                    if (occurrence is not null)
                    {
                        createdOccurrences.Add(occurrence);
                    }
                }

                var lastAcceptedEvent = acceptedEvents[^1];
                await storeProvider.TriggerRuntimeStates.UpsertAsync(
                    definition.JobName,
                    trigger.TriggerName,
                    triggerState with
                    {
                        ActivatedUtc = triggerState.ActivatedUtc ?? lastAcceptedEvent.AcceptedUtc,
                        LastAcceptedEventUtc = lastAcceptedEvent.AcceptedUtc,
                        LastAcceptedEventId = lastAcceptedEvent.AcceptedEventId,
                        UpdatedDate = timeProvider.GetUtcNow(),
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return Result.Success();
    }

    internal async Task<IReadOnlyList<JobOccurrence>> ListReadyOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        await this.ReconcileAllDependenciesAsync(cancellationToken).ConfigureAwait(false);
        var nowUtc = timeProvider.GetUtcNow();
        var occurrences = await storeProvider.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var leases = (await storeProvider.Leases.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => x.ExpiresUtc > nowUtc)
            .ToDictionary(x => x.OccurrenceId, x => x);
        var definitions = registrations.GetDefinitions().ToDictionary(x => x.JobName, StringComparer.OrdinalIgnoreCase);
        var runtimeStates = (await storeProvider.RuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(x => x.JobName, StringComparer.OrdinalIgnoreCase);
        var triggerStates = (await storeProvider.TriggerRuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(x => (x.JobName, x.TriggerName), x => x.State);

        return occurrences
            .Where(x => x.Status is JobOccurrenceStatus.Pending or JobOccurrenceStatus.Scheduled or JobOccurrenceStatus.Due or JobOccurrenceStatus.RetryScheduled)
            .Where(x => x.DueUtc <= nowUtc)
            .Where(x => !this.activeExecutions.ContainsKey(x.OccurrenceId))
            .Where(x => !leases.ContainsKey(x.OccurrenceId))
            .Where(x => definitions.TryGetValue(x.JobName, out var definition)
                && IsExecutionEnabled(definition, x.TriggerName, runtimeStates, triggerStates)
                && this.IsWorkerEligible(definition, x.TriggerName))
            .OrderByDescending(x => GetEffectivePriority(definitions[x.JobName], x.TriggerName))
            .ThenBy(x => x.DueUtc)
            .ThenBy(x => x.CreatedDate)
            .ThenBy(x => x.OccurrenceId)
            .ToArray();
    }

    internal async Task<Result<JobExecutionResult>> ExecuteStoredOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (occurrence is null)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
        }

        occurrence = await this.ReconcileDependentOccurrenceAsync(occurrence, cancellationToken).ConfigureAwait(false);

        if (this.activeExecutions.ContainsKey(occurrenceId) || occurrence.Status == JobOccurrenceStatus.Running)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is already running."));
        }

        if (occurrence.Status == JobOccurrenceStatus.Paused)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is paused."));
        }

        if (occurrence.Status == JobOccurrenceStatus.Blocked)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is blocked: {occurrence.BlockedReason ?? "dependency prerequisites are not yet satisfied"}."));
        }

        if (occurrence.Status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' cannot execute from status '{occurrence.Status}'."));
        }

        var batchGuardMessage = await this.TryPreventExecutionForCancelledBatchAsync(occurrence, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(batchGuardMessage))
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError(batchGuardMessage));
        }

        if (occurrence.DueUtc > timeProvider.GetUtcNow())
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is not due yet."));
        }

        var definition = this.ResolveJobDefinition(occurrence.JobName);
        var trigger = definition?.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, occurrence.TriggerName, StringComparison.OrdinalIgnoreCase));
        var runtimeStates = (await storeProvider.RuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(x => x.JobName, StringComparer.OrdinalIgnoreCase);
        var triggerStates = (await storeProvider.TriggerRuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(x => (x.JobName, x.TriggerName), x => x.State);
        if (definition is null || trigger is null || !IsExecutionEnabled(definition, occurrence.TriggerName, runtimeStates, triggerStates))
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' no longer maps to an active enabled job and trigger registration."));
        }

        if (!this.IsWorkerEligible(definition, trigger.TriggerName))
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError(this.BuildWorkerTargetFailureMessage(definition, trigger.TriggerName)));
        }

        var lease = await this.AcquireLeaseAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' is already leased by another scheduler instance."));
        }

        await this.AppendHistoryAsync(occurrence, null, "LeaseAcquired", occurrence.Status, null, lease.SchedulerInstanceId, cancellationToken).ConfigureAwait(false);

        var ready = occurrence with
        {
            Status = JobOccurrenceStatus.Due,
            UpdatedDate = timeProvider.GetUtcNow(),
        };
        await this.UpdateOccurrenceAsync(ready, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(ready, null, "OccurrenceDue", JobOccurrenceStatus.Due, null, null, cancellationToken).ConfigureAwait(false);

        return await this.ExecuteOccurrenceAsync(definition, trigger, ready, lease, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<JobDispatchResult>> DispatchInternalAsync(
        JobDefinition definition,
        object data,
        JobDispatchOptions options,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        var dispatch = await this.DispatchPreparedAsync(definition, data, options, waitForCompletion, cancellationToken).ConfigureAwait(false);
        if (dispatch.IsFailure)
        {
            return Result<JobDispatchResult>.Failure().WithErrors(dispatch.Errors).WithMessages(dispatch.Messages);
        }

        if (!waitForCompletion && dispatch.Value.ExecutionResult is { } executionResult && dispatch.Value.ExecutionFailure is null)
        {
            return Result<JobDispatchResult>.Success(dispatch.Value.DispatchResult);
        }

        return Result<JobDispatchResult>.Success(dispatch.Value.DispatchResult);
    }

    private async Task<Result<PreparedDispatch>> DispatchPreparedAsync(
        JobDefinition definition,
        object data,
        JobDispatchOptions options,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        options ??= new JobDispatchOptions();

        if (!options.Durable)
        {
            return Result<PreparedDispatch>.Failure().WithError(new ValidationError("Transient no-history dispatch is not supported in the current runtime phase."));
        }

        var resolvedJobState = await storeProvider.RuntimeStates.GetAsync(definition.JobName, cancellationToken).ConfigureAwait(false);
        if (resolvedJobState is { Enabled: false } || (resolvedJobState is not { Enabled: true } && !definition.Enabled) || resolvedJobState is { Paused: true })
        {
            return Result<PreparedDispatch>.Failure().WithError(new ValidationError($"The job '{definition.JobName}' is disabled or paused."));
        }

        var trigger = await this.ResolveManualTriggerAsync(definition, options.TriggerName, cancellationToken).ConfigureAwait(false);
        if (trigger.IsFailure)
        {
            return Result<PreparedDispatch>.Failure().WithErrors(trigger.Errors).WithMessages(trigger.Messages);
        }

        var evaluated = await this.CreateOccurrenceAsync(definition, trigger.Value, data, options, cancellationToken).ConfigureAwait(false);
        if (evaluated.IsFailure)
        {
            return Result<PreparedDispatch>.Failure().WithErrors(evaluated.Errors).WithMessages(evaluated.Messages);
        }

        var dispatchResult = new JobDispatchResult
        {
            JobName = definition.JobName,
            TriggerName = trigger.Value.TriggerName,
            OccurrenceId = evaluated.Value.OccurrenceId,
            CorrelationId = evaluated.Value.CorrelationId,
            AcceptedUtc = evaluated.Value.CreatedDate,
        };

        if (!waitForCompletion)
        {
            return Result<PreparedDispatch>.Success(new PreparedDispatch(dispatchResult, null, null));
        }

        if (!this.IsWorkerEligible(definition, trigger.Value.TriggerName))
        {
            return Result<PreparedDispatch>.Failure().WithError(new ValidationError(this.BuildWorkerTargetFailureMessage(definition, trigger.Value.TriggerName)));
        }

        var execution = await this.ExecuteOccurrenceAsync(definition, trigger.Value, evaluated.Value, null, cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return Result<PreparedDispatch>.Failure().WithErrors(execution.Errors).WithMessages(execution.Messages);
        }

        if (waitForCompletion && !IsTerminalExecutionStatus(execution.Value.Status))
        {
            execution = await this.WaitForTerminalOccurrenceAsync(evaluated.Value.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (execution.IsFailure)
            {
                return Result<PreparedDispatch>.Failure().WithErrors(execution.Errors).WithMessages(execution.Messages);
            }
        }

        return Result<PreparedDispatch>.Success(new PreparedDispatch(dispatchResult, execution.Value, null));
    }

    private async Task<Result<JobTriggerDefinition>> ResolveManualTriggerAsync(JobDefinition definition, string triggerName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manualTriggers = definition.Triggers.Where(x => x.TriggerType == JobTriggerType.Manual).ToArray();
        if (!string.IsNullOrWhiteSpace(triggerName))
        {
            var matching = manualTriggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
            if (matching is null)
            {
                return Result<JobTriggerDefinition>.Failure().WithError(new ValidationError($"The job '{definition.JobName}' does not define a manual trigger named '{triggerName}'."));
            }

            var runtimeState = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, matching.TriggerName, cancellationToken).ConfigureAwait(false);
            if (runtimeState is { Enabled: false } || (runtimeState is not { Enabled: true } && !matching.Enabled) || runtimeState is { Paused: true })
            {
                return Result<JobTriggerDefinition>.Failure().WithError(new ValidationError($"The manual trigger '{matching.TriggerName}' on job '{definition.JobName}' is disabled or paused."));
            }

            return Result<JobTriggerDefinition>.Success(matching);
        }

        var enabledManualTriggers = new List<JobTriggerDefinition>();
        foreach (var manualTrigger in manualTriggers)
        {
            var runtimeState = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, manualTrigger.TriggerName, cancellationToken).ConfigureAwait(false);
            if ((runtimeState is { Enabled: true } || manualTrigger.Enabled) && runtimeState is not { Enabled: false } && runtimeState is not { Paused: true })
            {
                enabledManualTriggers.Add(manualTrigger);
            }
        }

        if (enabledManualTriggers.Count == 0)
        {
            return Result<JobTriggerDefinition>.Failure().WithError(new ValidationError($"The job '{definition.JobName}' does not have an enabled manual trigger for dispatch."));
        }

        if (enabledManualTriggers.Count > 1)
        {
            return Result<JobTriggerDefinition>.Failure().WithError(new ValidationError($"The job '{definition.JobName}' has multiple enabled manual triggers. Specify TriggerName explicitly."));
        }

        return Result<JobTriggerDefinition>.Success(enabledManualTriggers[0]);
    }

    private bool IsWorkerEligible(JobDefinition definition, string triggerName)
    {
        var targets = this.GetEffectiveTargetInstances(definition, triggerName);
        return targets.Count == 0 || targets.Any(x => string.Equals(x, this.schedulerInstanceId, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<string> GetEffectiveTargetInstances(JobDefinition definition, string triggerName)
    {
        var triggerTargets = definition.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase))?.TargetInstances;
        return triggerTargets?.Count > 0 ? triggerTargets : definition.TargetInstances ?? [];
    }

    private string BuildWorkerTargetFailureMessage(JobDefinition definition, string triggerName)
    {
        var targets = this.GetEffectiveTargetInstances(definition, triggerName);
        var renderedTargets = string.Join(", ", targets);
        return $"The trigger '{triggerName}' on job '{definition.JobName}' targets scheduler instance(s) '{renderedTargets}' and cannot execute on scheduler instance '{this.schedulerInstanceId}'.";
    }

    private async Task<Result<ActiveExecutionState>> TryReserveConcurrencySlotAsync(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        Guid occurrenceId,
        Guid executionId,
        JobLeaseRecord lease,
        CancellationToken cancellationToken)
    {
        await this.jobConcurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var limit = definition.Concurrency?.Limit;
            if (limit is > 0)
            {
                var activeForJob = this.activeExecutions.Values.Count(x => string.Equals(x.JobName, definition.JobName, StringComparison.OrdinalIgnoreCase));
                if (activeForJob >= limit.Value)
                {
                    return Result<ActiveExecutionState>.Failure()
                        .WithError(new ValidationError($"The job '{definition.JobName}' already has {activeForJob} active execution(s), which reaches the configured concurrency limit of {limit.Value}."));
                }
            }

            var active = new ActiveExecutionState(executionId, lease, definition.JobName, trigger.TriggerName);
            this.activeExecutions[occurrenceId] = active;

            return Result<ActiveExecutionState>.Success(active);
        }
        finally
        {
            this.jobConcurrencyGate.Release();
        }
    }

    private async Task<Result<JobOccurrence>> CreateOccurrenceAsync(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        object data,
        JobDispatchOptions options,
        CancellationToken cancellationToken)
    {
        var prepared = await this.PrepareManualOccurrenceAsync(definition, trigger, data, options, null, null, null, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result<JobOccurrence>.Failure().WithErrors(prepared.Errors).WithMessages(prepared.Messages);
        }

        await storeProvider.TriggerRuntimeStates.UpsertAsync(definition.JobName, trigger.TriggerName, prepared.Value.RuntimeState, cancellationToken).ConfigureAwait(false);
        var occurrence = prepared.Value.Occurrence;

        var created = await storeProvider.Occurrences.TryCreateAsync(occurrence, cancellationToken).ConfigureAwait(false);
        var persisted = created
            ? occurrence
            : await storeProvider.Occurrences.GetByKeyAsync(occurrence.OccurrenceKey, cancellationToken).ConfigureAwait(false);

        await this.AppendHistoryAsync(persisted, null, "OccurrenceCreated", JobOccurrenceStatus.Pending, null, null, cancellationToken).ConfigureAwait(false);
        if (created)
        {
            await this.CreateChainedOccurrencesAsync(persisted, cancellationToken).ConfigureAwait(false);
        }

        return Result<JobOccurrence>.Success(persisted);
    }

    private async Task<JobOccurrence> PersistOccurrenceAsync(JobOccurrenceMaterialization materialized, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var initialStatus = materialized.DueUtc <= nowUtc
            ? JobOccurrenceStatus.Due
            : JobOccurrenceStatus.Scheduled;

        var occurrence = new JobOccurrence
        {
            OccurrenceId = Guid.NewGuid(),
            OccurrenceKey = materialized.OccurrenceKey,
            JobName = materialized.JobName,
            TriggerName = materialized.TriggerName,
            TriggerType = materialized.TriggerType,
            Status = initialStatus,
            DueUtc = materialized.DueUtc,
            ScheduledUtc = materialized.ScheduledUtc,
            Data = materialized.Data,
            DataType = materialized.DataType,
            Properties = materialized.Properties,
            CorrelationId = materialized.IdempotencyKey,
            CausationId = materialized.IdempotencyKey,
            IdempotencyKey = materialized.IdempotencyKey,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        };

        var created = await storeProvider.Occurrences.TryCreateAsync(occurrence, cancellationToken).ConfigureAwait(false);
        var persisted = created
            ? occurrence
            : await storeProvider.Occurrences.GetByKeyAsync(occurrence.OccurrenceKey, cancellationToken).ConfigureAwait(false);

        if (created)
        {
            await this.AppendHistoryAsync(persisted, null, "OccurrenceCreated", persisted.Status, null, null, cancellationToken).ConfigureAwait(false);
            await this.CreateChainedOccurrencesAsync(persisted, cancellationToken).ConfigureAwait(false);
        }

        return created ? persisted : null;
    }

    private JobOccurrenceMaterialization CreateEventOccurrence(
        JobDefinition job,
        JobTriggerDefinition trigger,
        JobAcceptedEvent acceptedEvent)
    {
        var properties = CloneProperties(trigger.Properties);
        properties.Merge(acceptedEvent.Properties);

        properties["jobs.event.source"] = acceptedEvent.Source;
        if (!string.IsNullOrWhiteSpace(acceptedEvent.SourceId))
        {
            properties["jobs.event.sourceId"] = acceptedEvent.SourceId;
        }

        if (!string.IsNullOrWhiteSpace(acceptedEvent.CorrelationId))
        {
            properties["jobs.event.correlationId"] = acceptedEvent.CorrelationId;
        }

        var occurrenceKey = JobOccurrenceKeyFactory.Create(
            job.JobName,
            trigger.TriggerName,
            JobTriggerType.Event,
            acceptedEvent.AcceptedUtc,
            acceptedEvent.AcceptedUtc,
            acceptedEvent.IdempotencyKey);

        return new JobOccurrenceMaterialization(
            occurrenceKey,
            job.JobName,
            trigger.TriggerName,
            JobTriggerType.Event,
            acceptedEvent.AcceptedUtc,
            acceptedEvent.AcceptedUtc,
            acceptedEvent.Data,
            acceptedEvent.DataType,
            properties,
            acceptedEvent.IdempotencyKey);
    }

    private async Task<Result<JobExecutionResult>> ExecuteOccurrenceAsync(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        JobOccurrence occurrence,
        JobLeaseRecord lease,
        CancellationToken cancellationToken)
    {
        var retryPolicy = trigger.RetryPolicy ?? definition.RetryPolicy;
        var effectiveTimeout = trigger.Timeout ?? definition.Timeout;
        var occurrenceState = occurrence;
        lease ??= await this.AcquireLeaseAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrence.OccurrenceId}' is already leased by another scheduler instance."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var executionId = Guid.NewGuid();
        var reserved = await this.TryReserveConcurrencySlotAsync(definition, trigger, occurrenceState.OccurrenceId, executionId, lease, cancellationToken).ConfigureAwait(false);
        if (reserved.IsFailure)
        {
            await storeProvider.Leases.ReleaseAsync(occurrenceState.OccurrenceId, lease.SchedulerInstanceId, lease.OwnershipToken, CancellationToken.None).ConfigureAwait(false);
            await this.AppendHistoryAsync(occurrenceState, null, "OccurrenceConcurrencyDeferred", occurrenceState.Status, null, BuildMessage(reserved.Errors, reserved.Messages), CancellationToken.None).ConfigureAwait(false);

            return Result<JobExecutionResult>.Failure().WithErrors(reserved.Errors).WithMessages(reserved.Messages);
        }

        var active = reserved.Value;
        var nowUtc = timeProvider.GetUtcNow();
        occurrenceState = occurrenceState with { Status = JobOccurrenceStatus.Running, ResumeStatus = null, UpdatedDate = nowUtc };
        await this.UpdateOccurrenceAsync(occurrenceState, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(occurrenceState, null, "OccurrenceRunning", JobOccurrenceStatus.Running, null, null, cancellationToken).ConfigureAwait(false);

        var attempts = await storeProvider.Executions.ListByOccurrenceAsync(occurrenceState.OccurrenceId, cancellationToken).ConfigureAwait(false);
        var attemptNumber = attempts.Count + 1;
        var execution = new JobExecution
        {
            ExecutionId = executionId,
            OccurrenceId = occurrenceState.OccurrenceId,
            JobName = occurrenceState.JobName,
            TriggerName = occurrenceState.TriggerName,
            AttemptNumber = attemptNumber,
            Status = JobExecutionStatus.Started,
            SchedulerInstanceId = this.schedulerInstanceId,
            StartedUtc = nowUtc,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        };

        await storeProvider.Executions.CreateAsync(execution, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(occurrenceState, executionId, "ExecutionStarted", JobOccurrenceStatus.Running, JobExecutionStatus.Started, null, cancellationToken).ConfigureAwait(false);

        using var timeoutCts = effectiveTimeout.HasValue ? new CancellationTokenSource(effectiveTimeout.Value) : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.CancellationSource.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.CancellationSource.Token, timeoutCts.Token);
        var renewalTask = this.StartLeaseRenewalLoopAsync(occurrenceState.OccurrenceId, active, cancellationToken);
        var hydrated = await this.HydrateExecutionContextAsync(definition, occurrenceState, execution, linkedCts.Token, cancellationToken).ConfigureAwait(false);
        if (hydrated.IsFailure)
        {
            var finalized = await this.FinalizeFailedExecutionAsync(occurrenceState, execution, active, JobExecutionStatus.Failed, BuildMessage(hydrated.Errors, hydrated.Messages), CancellationToken.None).ConfigureAwait(false);
            this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
            await renewalTask.ConfigureAwait(false);
            return finalized
                ? Result<JobExecutionResult>.Failure().WithErrors(hydrated.Errors).WithMessages(hydrated.Messages)
                : Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceState.OccurrenceId}' lost lease ownership before failure finalization."));
        }

        using var scope = scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetService(definition.JobType) as IJob;
        if (job is null)
        {
            var resolutionMessage = $"The job '{definition.JobName}' could not be resolved from the execution scope.";
            var finalized = await this.FinalizeFailedExecutionAsync(occurrenceState, execution, active, JobExecutionStatus.Failed, resolutionMessage, CancellationToken.None).ConfigureAwait(false);
            this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
            await renewalTask.ConfigureAwait(false);
            return finalized
                ? Result<JobExecutionResult>.Failure().WithError(new ValidationError(resolutionMessage))
                : Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceState.OccurrenceId}' lost lease ownership before failure finalization."));
        }

        var behaviorContext = new JobBehaviorContext(
            scope.ServiceProvider,
            definition,
            trigger,
            job,
            hydrated.Value,
            this.schedulerInstanceId,
            this.activeExecutions.Count,
            this.options.MaxConcurrency);

        try
        {
            return await this.ExecuteBehaviorPipelineAsync(
                behaviorContext,
                async () =>
                {
                    var jobResult = await job.ExecuteAsync(hydrated.Value, linkedCts.Token).ConfigureAwait(false);
                    var terminalMessages = MergeMessages(hydrated.Value.Messages, jobResult.Messages, jobResult.Errors.Select(x => x.Message));
                    if (jobResult.IsSuccess)
                    {
                        if (!await this.VerifyLeaseOwnershipAsync(active, occurrenceState.OccurrenceId, CancellationToken.None).ConfigureAwait(false))
                        {
                            this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
                            await renewalTask.ConfigureAwait(false);
                            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceState.OccurrenceId}' lost lease ownership before completion finalization."));
                        }

                        var completedUtc = timeProvider.GetUtcNow();
                        var completedExecution = execution with
                        {
                            Status = JobExecutionStatus.Completed,
                            CompletedUtc = completedUtc,
                            Message = terminalMessages.LastOrDefault(),
                            UpdatedDate = completedUtc,
                        };
                        await storeProvider.Executions.UpdateAsync(completedExecution, cancellationToken).ConfigureAwait(false);
                        occurrenceState = occurrenceState with { Status = JobOccurrenceStatus.Completed, UpdatedDate = completedUtc };
                        await this.UpdateOccurrenceAsync(occurrenceState, cancellationToken).ConfigureAwait(false);
                        await this.AppendHistoryAsync(occurrenceState, executionId, "ExecutionCompleted", JobOccurrenceStatus.Completed, JobExecutionStatus.Completed, terminalMessages.LastOrDefault(), cancellationToken).ConfigureAwait(false);
                        await this.AppendHistoryAsync(occurrenceState, executionId, "OccurrenceCompleted", JobOccurrenceStatus.Completed, null, terminalMessages.LastOrDefault(), cancellationToken).ConfigureAwait(false);
                        await this.ReconcileDependentsForPrerequisiteAsync(occurrenceState, cancellationToken).ConfigureAwait(false);
                        await this.ReleaseLeaseAsync(active, occurrenceState.OccurrenceId, CancellationToken.None).ConfigureAwait(false);
                        this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
                        await renewalTask.ConfigureAwait(false);
                        return Result<JobExecutionResult>.Success(new JobExecutionResult
                        {
                            JobName = occurrenceState.JobName,
                            TriggerName = occurrenceState.TriggerName,
                            OccurrenceId = occurrenceState.OccurrenceId,
                            ExecutionId = executionId,
                            Status = JobExecutionStatus.Completed,
                            TimedOut = false,
                            StartedUtc = execution.StartedUtc,
                            CompletedUtc = completedUtc,
                            Messages = terminalMessages,
                        });
                    }

                    var failureMessage = BuildMessage(jobResult.Errors, terminalMessages);
                    return await this.RetryOrFinalizeFailedExecutionAsync(
                        occurrenceState,
                        execution,
                        active,
                        trigger,
                        retryPolicy,
                        attemptNumber,
                        JobExecutionStatus.Failed,
                        failureMessage,
                        terminalMessages,
                        false,
                        renewalTask,
                        cancellationToken).ConfigureAwait(false);
                },
                linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            var message = "Execution timed out.";
            return await this.RetryOrFinalizeFailedExecutionAsync(
                occurrenceState,
                execution,
                active,
                trigger,
                retryPolicy,
                attemptNumber,
                JobExecutionStatus.TimedOut,
                message,
                [message],
                true,
                renewalTask,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (active.InterruptRequested)
        {
            var message = active.Reason ?? "Execution was interrupted.";
            var interrupted = await this.FinalizeCancelledExecutionAsync(occurrenceState, execution, active, JobExecutionStatus.Interrupted, message, CancellationToken.None).ConfigureAwait(false);
            this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
            await renewalTask.ConfigureAwait(false);
            if (!interrupted)
            {
                return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceState.OccurrenceId}' lost lease ownership before interruption finalization."));
            }

            return Result<JobExecutionResult>.Success(new JobExecutionResult
            {
                JobName = occurrenceState.JobName,
                TriggerName = occurrenceState.TriggerName,
                OccurrenceId = occurrenceState.OccurrenceId,
                ExecutionId = executionId,
                Status = JobExecutionStatus.Interrupted,
                StartedUtc = execution.StartedUtc,
                CompletedUtc = timeProvider.GetUtcNow(),
                Messages = [message],
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || active.CancelRequested)
        {
            var message = active.Reason ?? "Execution was cancelled.";
            var cancelled = await this.FinalizeCancelledExecutionAsync(occurrenceState, execution, active, JobExecutionStatus.Cancelled, message, CancellationToken.None).ConfigureAwait(false);
            this.CompleteActiveExecution(occurrenceState.OccurrenceId, active);
            await renewalTask.ConfigureAwait(false);
            if (!cancelled)
            {
                return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceState.OccurrenceId}' lost lease ownership before cancellation finalization."));
            }

            return Result<JobExecutionResult>.Success(new JobExecutionResult
            {
                JobName = occurrenceState.JobName,
                TriggerName = occurrenceState.TriggerName,
                OccurrenceId = occurrenceState.OccurrenceId,
                ExecutionId = executionId,
                Status = JobExecutionStatus.Cancelled,
                StartedUtc = execution.StartedUtc,
                CompletedUtc = timeProvider.GetUtcNow(),
                Messages = [message],
            });
        }
        catch (Exception exception)
        {
            await this.HandleUnhandledExceptionAsync(new JobSchedulerExceptionContext
            {
                SchedulerInstanceId = this.schedulerInstanceId,
                Source = JobSchedulerExceptionSource.Execution,
                Exception = exception,
                Definition = definition,
                Trigger = trigger,
                OccurrenceId = occurrenceState.OccurrenceId,
                ExecutionId = executionId,
            }, CancellationToken.None).ConfigureAwait(false);

            return await this.RetryOrFinalizeFailedExecutionAsync(
                occurrenceState,
                execution,
                active,
                trigger,
                retryPolicy,
                attemptNumber,
                JobExecutionStatus.Failed,
                exception.Message,
                [exception.Message],
                false,
                renewalTask,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<Result<JobExecutionResult>> ExecuteBehaviorPipelineAsync(
        JobBehaviorContext context,
        JobBehaviorDelegate jobExecutor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(jobExecutor);

        var behaviorTypes = registrations.GetGlobalBehaviorTypes()
            .Concat(context.Definition.BehaviorTypes)
            .Distinct()
            .ToArray();

        var behaviors = behaviorTypes
            .Select(type => ActivatorUtilities.CreateInstance(context.Services, type) as IJobBehavior)
            .Where(behavior => behavior is not null)
            .ToArray();

        var result = await behaviors
            .Reverse()
            .Aggregate(jobExecutor, (next, behavior) => () => behavior.HandleAsync(context, next, cancellationToken))()
            .ConfigureAwait(false);

        return result is Result<JobExecutionResult> concrete
            ? concrete
            : result.IsSuccess
                ? Result<JobExecutionResult>.Success(result.Value).WithMessages(result.Messages)
                : Result<JobExecutionResult>.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }

    private async Task HandleUnhandledExceptionAsync(JobSchedulerExceptionContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in this.exceptionHandlers)
        {
            try
            {
                await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Do not let exception handlers hide the scheduler's original failure semantics.
            }
        }
    }

    private async Task<Result<JobExecutionResult>> WaitForTerminalOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.RecoverExpiredLeasesAsync(cancellationToken).ConfigureAwait(false);
            var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is null)
            {
                return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was not found."));
            }

            if (occurrence.Status == JobOccurrenceStatus.Paused)
            {
                return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrenceId}' was paused before reaching a terminal state."));
            }

            if (occurrence.Status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived)
            {
                return await this.BuildExecutionResultAsync(occurrence, cancellationToken).ConfigureAwait(false);
            }

            var nowUtc = timeProvider.GetUtcNow();
            if (occurrence.DueUtc > nowUtc)
            {
                await Task.Delay(occurrence.DueUtc - nowUtc, timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var execution = await this.ExecuteStoredOccurrenceAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
            if (execution.IsFailure)
            {
                return execution;
            }

            if (IsTerminalExecutionStatus(execution.Value.Status))
            {
                return execution;
            }
        }
    }

    private async Task<Result<JobExecutionResult>> BuildExecutionResultAsync(JobOccurrence occurrence, CancellationToken cancellationToken)
    {
        var execution = (await storeProvider.Executions.ListByOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(x => x.AttemptNumber)
            .ThenByDescending(x => x.UpdatedDate)
            .FirstOrDefault();
        if (execution is null)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrence.OccurrenceId}' has no execution attempts."));
        }

        var history = await storeProvider.ExecutionHistory.ListAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        var messages = history
            .Where(x => x.ExecutionId == execution.ExecutionId && !string.IsNullOrWhiteSpace(x.Message))
            .Select(x => x.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Result<JobExecutionResult>.Success(new JobExecutionResult
        {
            JobName = occurrence.JobName,
            TriggerName = occurrence.TriggerName,
            OccurrenceId = occurrence.OccurrenceId,
            ExecutionId = execution.ExecutionId,
            Status = execution.Status,
            TimedOut = execution.Status == JobExecutionStatus.TimedOut,
            StartedUtc = execution.StartedUtc,
            CompletedUtc = execution.CompletedUtc,
            Messages = messages,
        });
    }

    private async Task<Result<IJobExecutionContext>> HydrateExecutionContextAsync(
        JobDefinition definition,
        JobOccurrence occurrence,
        JobExecution execution,
        CancellationToken executionCancellationToken,
        CancellationToken cancellationToken)
    {
        var dataResult = this.DeserializeData(occurrence.Data, occurrence.DataType);
        if (dataResult.IsFailure)
        {
            return Result<IJobExecutionContext>.Failure().WithErrors(dataResult.Errors).WithMessages(dataResult.Messages);
        }

        var previousExecution = await storeProvider.PreviousExecutions.GetPreviousExecutionAsync(occurrence.OccurrenceId, execution.ExecutionId, cancellationToken).ConfigureAwait(false);
        var previousSuccessful = await storeProvider.PreviousExecutions.GetPreviousSuccessfulExecutionAsync(occurrence.JobName, occurrence.TriggerName, execution.StartedUtc, cancellationToken).ConfigureAwait(false);

        return Result<IJobExecutionContext>.Success(this.CreateExecutionContext(
            occurrence,
            execution,
            dataResult.Value,
            await this.BuildSnapshotAsync(previousExecution, cancellationToken).ConfigureAwait(false),
            await this.BuildSnapshotAsync(previousSuccessful, cancellationToken).ConfigureAwait(false),
            executionCancellationToken));
    }

    private IJobExecutionContext CreateExecutionContext(
        JobOccurrence occurrence,
        JobExecution execution,
        object data,
        JobExecutionContextSnapshot previousExecution,
        JobExecutionContextSnapshot previousSuccessfulExecution,
        CancellationToken cancellationToken)
    {
        var contextType = typeof(RuntimeJobExecutionContext<>).MakeGenericType(occurrence.DataType);
        return Activator.CreateInstance(
            contextType,
            occurrence.JobName,
            occurrence.TriggerName,
            occurrence.OccurrenceId,
            execution.ExecutionId,
            execution.AttemptNumber,
            occurrence.CorrelationId,
            occurrence.IdempotencyKey,
            occurrence.ScheduledUtc,
            occurrence.DueUtc,
            execution.StartedUtc,
            data,
            occurrence.DataType,
            occurrence.Properties,
            previousExecution,
            previousSuccessfulExecution,
            cancellationToken) as IJobExecutionContext;
    }

    private async Task<JobExecutionContextSnapshot> BuildSnapshotAsync(JobExecution execution, CancellationToken cancellationToken)
    {
        if (execution is null)
        {
            return null;
        }

        var history = await storeProvider.ExecutionHistory.ListAsync(execution.OccurrenceId, cancellationToken).ConfigureAwait(false);
        var messages = history
            .Where(x => x.ExecutionId == execution.ExecutionId && !string.IsNullOrWhiteSpace(x.Message))
            .Select(x => x.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new JobExecutionContextSnapshot(
            execution.OccurrenceId,
            execution.ExecutionId,
            execution.JobName,
            execution.TriggerName,
            execution.AttemptNumber,
            execution.Status,
            execution.StartedUtc,
            execution.CompletedUtc,
            messages,
            execution.Status == JobExecutionStatus.Completed ? null : execution.Message);
    }

    private Result<object> DeserializeData(object value, Type targetType)
    {
        if (targetType == typeof(Unit))
        {
            return Result<object>.Success(Unit.Value);
        }

        if (value is null)
        {
            return Result<object>.Failure().WithError(new ValidationError($"A payload is required for job data contract '{targetType.FullName}'."));
        }

        if (targetType.IsInstanceOfType(value))
        {
            return Result<object>.Success(value);
        }

        try
        {
            using var stream = new MemoryStream();
            serializer.Serialize(value, stream);
            stream.Position = 0;
            var deserialized = serializer.Deserialize(stream, targetType);
            return deserialized is null
                ? Result<object>.Failure().WithError(new ValidationError($"The supplied payload could not be deserialized as '{targetType.FullName}'."))
                : Result<object>.Success(deserialized);
        }
        catch (Exception exception)
        {
            return Result<object>.Failure().WithError(new ValidationError($"The supplied payload is invalid for '{targetType.FullName}': {exception.Message}"));
        }
    }

    private async Task<bool> FinalizeFailedExecutionAsync(
        JobOccurrence occurrence,
        JobExecution execution,
        ActiveExecutionState active,
        JobExecutionStatus status,
        string message,
        CancellationToken cancellationToken)
    {
        if (!await this.VerifyLeaseOwnershipAsync(active, occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var completedUtc = timeProvider.GetUtcNow();
        var updatedExecution = execution with
        {
            Status = status,
            CompletedUtc = completedUtc,
            Message = message,
            UpdatedDate = completedUtc,
        };
        await storeProvider.Executions.UpdateAsync(updatedExecution, cancellationToken).ConfigureAwait(false);

        var updatedOccurrence = occurrence with
        {
            Status = JobOccurrenceStatus.Failed,
            UpdatedDate = completedUtc,
        };
        await this.UpdateOccurrenceAsync(updatedOccurrence, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updatedOccurrence, execution.ExecutionId, GetFailureExecutionEventName(status), JobOccurrenceStatus.Failed, status, message, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updatedOccurrence, execution.ExecutionId, GetFailureOccurrenceEventName(status), JobOccurrenceStatus.Failed, null, message, cancellationToken).ConfigureAwait(false);
        await this.ReconcileDependentsForPrerequisiteAsync(updatedOccurrence, cancellationToken).ConfigureAwait(false);
        await this.ReleaseLeaseAsync(active, occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<Result<JobExecutionResult>> RetryOrFinalizeFailedExecutionAsync(
        JobOccurrence occurrence,
        JobExecution execution,
        ActiveExecutionState active,
        JobTriggerDefinition trigger,
        JobRetryPolicy retryPolicy,
        int attemptNumber,
        JobExecutionStatus terminalStatus,
        string message,
        IReadOnlyList<string> messages,
        bool timedOut,
        Task renewalTask,
        CancellationToken cancellationToken)
    {
        var executionMessages = messages is { Count: > 0 } ? messages : [message];
        var shouldRetry = retryPolicy is { MaxAttempts: > 0 } && attemptNumber < retryPolicy.MaxAttempts;
        if (shouldRetry)
        {
            if (!await this.VerifyLeaseOwnershipAsync(active, occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false))
            {
                this.CompleteActiveExecution(occurrence.OccurrenceId, active);
                await renewalTask.ConfigureAwait(false);
                return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrence.OccurrenceId}' lost lease ownership before retry scheduling."));
            }

            var retryScheduledUtc = timeProvider.GetUtcNow();
            var nextRetryDueUtc = retryScheduledUtc + (GetRetryDelay(retryPolicy, attemptNumber + 1) ?? TimeSpan.Zero);
            using var retryActivity = JobSchedulerInstrumentation.StartRetrySchedulingActivity(this.schedulerInstanceId, occurrence, trigger, execution.ExecutionId, occurrence.CorrelationId);
            var retriedExecution = execution with
            {
                Status = JobExecutionStatus.Retried,
                CompletedUtc = retryScheduledUtc,
                Message = message,
                UpdatedDate = retryScheduledUtc,
            };

            await storeProvider.Executions.UpdateAsync(retriedExecution, cancellationToken).ConfigureAwait(false);
            var retryOccurrence = occurrence with
            {
                Status = JobOccurrenceStatus.RetryScheduled,
                DueUtc = nextRetryDueUtc,
                UpdatedDate = retryScheduledUtc,
            };
            await this.UpdateOccurrenceAsync(retryOccurrence, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(retryOccurrence, execution.ExecutionId, "ExecutionRetried", JobOccurrenceStatus.RetryScheduled, JobExecutionStatus.Retried, message, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(retryOccurrence, execution.ExecutionId, "OccurrenceRetryScheduled", JobOccurrenceStatus.RetryScheduled, null, message, cancellationToken).ConfigureAwait(false);
            await this.ReleaseLeaseAsync(active, retryOccurrence.OccurrenceId, CancellationToken.None).ConfigureAwait(false);
            retryActivity?.SetTag("jobs.retry.next_due_utc", nextRetryDueUtc.ToString("O"));
            this.CompleteActiveExecution(retryOccurrence.OccurrenceId, active);
            await renewalTask.ConfigureAwait(false);

            return Result<JobExecutionResult>.Success(new JobExecutionResult
            {
                JobName = retryOccurrence.JobName,
                TriggerName = retryOccurrence.TriggerName,
                OccurrenceId = retryOccurrence.OccurrenceId,
                ExecutionId = execution.ExecutionId,
                Status = JobExecutionStatus.Retried,
                TimedOut = timedOut,
                StartedUtc = execution.StartedUtc,
                CompletedUtc = retryScheduledUtc,
                Messages = executionMessages,
            });
        }

        var finalized = await this.FinalizeFailedExecutionAsync(occurrence, execution, active, terminalStatus, message, cancellationToken).ConfigureAwait(false);
        this.CompleteActiveExecution(occurrence.OccurrenceId, active);
        await renewalTask.ConfigureAwait(false);
        if (!finalized)
        {
            return Result<JobExecutionResult>.Failure().WithError(new ValidationError($"The occurrence '{occurrence.OccurrenceId}' lost lease ownership before failure finalization."));
        }

        return Result<JobExecutionResult>.Success(new JobExecutionResult
        {
            JobName = occurrence.JobName,
            TriggerName = occurrence.TriggerName,
            OccurrenceId = occurrence.OccurrenceId,
            ExecutionId = execution.ExecutionId,
            Status = terminalStatus,
            TimedOut = timedOut,
            StartedUtc = execution.StartedUtc,
            CompletedUtc = timeProvider.GetUtcNow(),
            Messages = executionMessages,
        });
    }

    private async Task<bool> FinalizeCancelledExecutionAsync(
        JobOccurrence occurrence,
        JobExecution execution,
        ActiveExecutionState active,
        JobExecutionStatus status,
        string message,
        CancellationToken cancellationToken)
    {
        if (!await this.VerifyLeaseOwnershipAsync(active, occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var completedUtc = timeProvider.GetUtcNow();
        var updatedExecution = execution with
        {
            Status = status,
            CompletedUtc = completedUtc,
            Message = message,
            UpdatedDate = completedUtc,
        };
        await storeProvider.Executions.UpdateAsync(updatedExecution, cancellationToken).ConfigureAwait(false);

        var updatedOccurrence = occurrence with
        {
            Status = JobOccurrenceStatus.Cancelled,
            UpdatedDate = completedUtc,
        };
        await this.UpdateOccurrenceAsync(updatedOccurrence, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updatedOccurrence, execution.ExecutionId, GetCancellationExecutionEventName(status), JobOccurrenceStatus.Cancelled, status, message, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(updatedOccurrence, execution.ExecutionId, GetCancellationOccurrenceEventName(status), JobOccurrenceStatus.Cancelled, null, message, cancellationToken).ConfigureAwait(false);
        await this.ReconcileDependentsForPrerequisiteAsync(updatedOccurrence, cancellationToken).ConfigureAwait(false);
        await this.ReleaseLeaseAsync(active, occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task CreateChainedOccurrencesAsync(JobOccurrence prerequisite, CancellationToken cancellationToken)
    {
        var definition = this.ResolveJobDefinition(prerequisite.JobName);
        if (definition?.Chains.Count is not > 0)
        {
            return;
        }

        foreach (var chain in definition.Chains)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var successor = this.ResolveJobDefinition(chain.SuccessorJobName);
            var trigger = ResolveChainTrigger(successor, chain);
            var nowUtc = timeProvider.GetUtcNow();
            var occurrenceKey = $"chain:{prerequisite.OccurrenceId:N}:{successor.JobName}:{trigger.TriggerName}";
            var blockedReason = BuildBlockedReason(prerequisite.OccurrenceId, chain.RequiredStatuses);
            var properties = CloneProperties(trigger.Properties);
            properties["chain:predecessorOccurrenceId"] = prerequisite.OccurrenceId.ToString("N");
            properties["chain:predecessorJob"] = prerequisite.JobName;
            properties["chain:predecessorTrigger"] = prerequisite.TriggerName;
            properties.Merge(chain.Properties);

            var dependent = new JobOccurrence
            {
                OccurrenceId = Guid.NewGuid(),
                OccurrenceKey = occurrenceKey,
                JobName = successor.JobName,
                TriggerName = trigger.TriggerName,
                TriggerType = trigger.TriggerType,
                Status = JobOccurrenceStatus.Blocked,
                DueUtc = prerequisite.DueUtc,
                ScheduledUtc = prerequisite.DueUtc,
                Data = trigger.Data ?? Unit.Value,
                DataType = trigger.DataType ?? typeof(Unit),
                Properties = properties,
                CorrelationId = prerequisite.CorrelationId,
                CausationId = prerequisite.OccurrenceId.ToString("N"),
                IdempotencyKey = occurrenceKey,
                ResumeStatus = prerequisite.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
                BlockedReason = blockedReason,
                CreatedDate = nowUtc,
                UpdatedDate = nowUtc,
            };

            var created = await storeProvider.Occurrences.TryCreateAsync(dependent, cancellationToken).ConfigureAwait(false);
            if (!created)
            {
                continue;
            }

            await storeProvider.Dependencies.AddAsync(
                new JobOccurrenceDependency
                {
                    DependencyId = Guid.NewGuid(),
                    DependentOccurrenceId = dependent.OccurrenceId,
                    PrerequisiteOccurrenceId = prerequisite.OccurrenceId,
                    RequiredStatuses = chain.RequiredStatuses.ToArray(),
                    Status = JobDependencyStatus.Pending,
                    FailurePolicy = chain.FailurePolicy,
                    Reason = blockedReason,
                    Properties = properties.Clone(),
                    CreatedDate = nowUtc,
                    UpdatedDate = nowUtc,
                },
                cancellationToken).ConfigureAwait(false);

            await this.AppendHistoryAsync(dependent, null, "OccurrenceCreated", JobOccurrenceStatus.Blocked, null, null, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(dependent, null, "DependencyCreated", JobOccurrenceStatus.Blocked, null, blockedReason, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(dependent, null, "OccurrenceBlocked", JobOccurrenceStatus.Blocked, null, blockedReason, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileAllDependenciesAsync(CancellationToken cancellationToken)
    {
        var dependencies = await storeProvider.Queries.ListDependenciesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var dependentOccurrenceId in dependencies.Select(x => x.DependentOccurrenceId).Distinct())
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(dependentOccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is not null)
            {
                await this.ReconcileDependentOccurrenceAsync(occurrence, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReconcileDependentsForPrerequisiteAsync(JobOccurrence prerequisite, CancellationToken cancellationToken)
    {
        var dependencies = await storeProvider.Dependencies.ListByPrerequisiteAsync(prerequisite.OccurrenceId, cancellationToken).ConfigureAwait(false);
        foreach (var dependency in dependencies)
        {
            var dependent = await storeProvider.Occurrences.GetAsync(dependency.DependentOccurrenceId, cancellationToken).ConfigureAwait(false);
            if (dependent is not null)
            {
                await this.ReconcileDependentOccurrenceAsync(dependent, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<JobOccurrence> ReconcileDependentOccurrenceAsync(JobOccurrence occurrence, CancellationToken cancellationToken)
    {
        var dependencies = await storeProvider.Dependencies.ListByDependentAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        if (dependencies.Count == 0)
        {
            return occurrence;
        }

        var nowUtc = timeProvider.GetUtcNow();
        var evaluated = new List<JobOccurrenceDependency>(dependencies.Count);
        foreach (var dependency in dependencies)
        {
            var prerequisite = await storeProvider.Occurrences.GetAsync(dependency.PrerequisiteOccurrenceId, cancellationToken).ConfigureAwait(false);
            var resolved = ResolveDependencyState(dependency, prerequisite, nowUtc);
            evaluated.Add(resolved);

            if (resolved.Status != dependency.Status || !string.Equals(resolved.Reason, dependency.Reason, StringComparison.Ordinal))
            {
                await storeProvider.Dependencies.UpdateAsync(resolved, cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(occurrence, null, GetDependencyEventName(resolved.Status), occurrence.Status, null, resolved.Reason, cancellationToken).ConfigureAwait(false);
            }
        }

        if (IsTerminalOccurrenceStatus(occurrence.Status) || occurrence.Status is JobOccurrenceStatus.Running or JobOccurrenceStatus.Paused or JobOccurrenceStatus.Archived)
        {
            return occurrence;
        }

        var failed = evaluated.FirstOrDefault(x => x.Status is JobDependencyStatus.Failed or JobDependencyStatus.Skipped or JobDependencyStatus.Cancelled);
        if (failed is not null)
        {
            return await this.ApplyDependencyFailurePolicyAsync(occurrence, failed, nowUtc, cancellationToken).ConfigureAwait(false);
        }

        var pending = evaluated.FirstOrDefault(x => x.Status == JobDependencyStatus.Pending);
        if (pending is not null)
        {
            var blockedReason = pending.Reason ?? BuildBlockedReason(pending.PrerequisiteOccurrenceId, pending.RequiredStatuses);
            if (occurrence.Status != JobOccurrenceStatus.Blocked || !string.Equals(occurrence.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                var blocked = occurrence with
                {
                    Status = JobOccurrenceStatus.Blocked,
                    ResumeStatus = occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
                    BlockedReason = blockedReason,
                    UpdatedDate = nowUtc,
                };
                await this.UpdateOccurrenceAsync(blocked, cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(blocked, null, "OccurrenceBlocked", JobOccurrenceStatus.Blocked, null, blockedReason, cancellationToken).ConfigureAwait(false);
                return blocked;
            }

            return occurrence;
        }

        var releasedStatus = occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled;
        if (occurrence.Status != releasedStatus || !string.IsNullOrWhiteSpace(occurrence.BlockedReason))
        {
            var released = occurrence with
            {
                Status = releasedStatus,
                ResumeStatus = null,
                BlockedReason = null,
                UpdatedDate = nowUtc,
            };
            await this.UpdateOccurrenceAsync(released, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(released, null, "OccurrenceDependencyReleased", released.Status, null, "All prerequisite occurrences were satisfied.", cancellationToken).ConfigureAwait(false);
            return released;
        }

        return occurrence;
    }

    private async Task<JobOccurrence> ApplyDependencyFailurePolicyAsync(
        JobOccurrence occurrence,
        JobOccurrenceDependency dependency,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var reason = dependency.Reason ?? $"The prerequisite occurrence '{dependency.PrerequisiteOccurrenceId}' did not reach a required terminal status.";
        return dependency.FailurePolicy switch
        {
            JobDependencyFailurePolicy.KeepBlocked => await this.BlockOccurrenceAsync(occurrence, reason, nowUtc, cancellationToken).ConfigureAwait(false),
            JobDependencyFailurePolicy.Skip => await this.TerminalizeDependencyBlockedOccurrenceAsync(occurrence, JobOccurrenceStatus.Cancelled, "OccurrenceDependencySkipped", reason, nowUtc, cancellationToken).ConfigureAwait(false),
            JobDependencyFailurePolicy.Cancel => await this.TerminalizeDependencyBlockedOccurrenceAsync(occurrence, JobOccurrenceStatus.Cancelled, "OccurrenceDependencyCancelled", reason, nowUtc, cancellationToken).ConfigureAwait(false),
            JobDependencyFailurePolicy.Fail => await this.TerminalizeDependencyBlockedOccurrenceAsync(occurrence, JobOccurrenceStatus.Failed, "OccurrenceDependencyFailed", reason, nowUtc, cancellationToken).ConfigureAwait(false),
            _ => occurrence,
        };
    }

    private async Task<JobOccurrence> BlockOccurrenceAsync(JobOccurrence occurrence, string blockedReason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (occurrence.Status == JobOccurrenceStatus.Blocked && string.Equals(occurrence.BlockedReason, blockedReason, StringComparison.Ordinal))
        {
            return occurrence;
        }

        var blocked = occurrence with
        {
            Status = JobOccurrenceStatus.Blocked,
            ResumeStatus = occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            BlockedReason = blockedReason,
            UpdatedDate = nowUtc,
        };
        await this.UpdateOccurrenceAsync(blocked, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(blocked, null, "OccurrenceBlocked", JobOccurrenceStatus.Blocked, null, blockedReason, cancellationToken).ConfigureAwait(false);
        return blocked;
    }

    private async Task<JobOccurrence> TerminalizeDependencyBlockedOccurrenceAsync(
        JobOccurrence occurrence,
        JobOccurrenceStatus targetStatus,
        string eventName,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (occurrence.Status == targetStatus && string.Equals(occurrence.BlockedReason, reason, StringComparison.Ordinal))
        {
            return occurrence;
        }

        var terminal = occurrence with
        {
            Status = targetStatus,
            ResumeStatus = null,
            BlockedReason = reason,
            UpdatedDate = nowUtc,
        };
        await this.UpdateOccurrenceAsync(terminal, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(terminal, null, eventName, terminal.Status, null, reason, cancellationToken).ConfigureAwait(false);
        return terminal;
    }

    private async Task<Result<PreparedBatchChildren>> PrepareBatchChildrenAsync(JobBatch batch, JobBatchDispatchRequest request, CancellationToken cancellationToken)
    {
        var children = new List<PreparedBatchChild>(request.Items?.Count ?? 0);
        for (var index = 0; index < (request.Items?.Count ?? 0); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = request.Items[index];
            if (item is null || string.IsNullOrWhiteSpace(item.JobName))
            {
                return Result<PreparedBatchChildren>.Failure().WithError(new ValidationError($"Batch item {index + 1} must specify a job name."));
            }

            var definition = this.ResolveJobDefinition(item.JobName);
            if (definition is null)
            {
                return Result<PreparedBatchChildren>.Failure().WithError(new ValidationError($"The job '{item.JobName}' is not registered."));
            }

            var options = BuildBatchItemOptions(batch, request, item, index);
            var trigger = await this.ResolveManualTriggerAsync(definition, options.TriggerName, cancellationToken).ConfigureAwait(false);
            if (trigger.IsFailure)
            {
                return Result<PreparedBatchChildren>.Failure().WithErrors(trigger.Errors).WithMessages(trigger.Messages);
            }

            var properties = BuildBatchItemProperties(batch, item, index);
            var prepared = await this.PrepareManualOccurrenceAsync(
                definition,
                trigger.Value,
                item.Data,
                options,
                item.DueUtc,
                properties,
                string.IsNullOrWhiteSpace(request.CausationId) ? batch.CausationId : request.CausationId.Trim(),
                cancellationToken).ConfigureAwait(false);
            if (prepared.IsFailure)
            {
                return Result<PreparedBatchChildren>.Failure().WithErrors(prepared.Errors).WithMessages(prepared.Messages);
            }

            var existingOccurrence = await storeProvider.Occurrences.GetByKeyAsync(prepared.Value.Occurrence.OccurrenceKey, cancellationToken).ConfigureAwait(false);
            var occurrence = existingOccurrence ?? prepared.Value.Occurrence;
            children.Add(new PreparedBatchChild(
                occurrence,
                prepared.Value.RuntimeState,
                definition.JobName,
                trigger.Value.TriggerName,
                item.Sequence ?? index + 1,
                existingOccurrence is null));
        }

        return Result<PreparedBatchChildren>.Success(new PreparedBatchChildren(children));
    }

    private async Task FinalizePreparedBatchChildrenAsync(IEnumerable<PreparedBatchChild> children, CancellationToken cancellationToken)
    {
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await storeProvider.TriggerRuntimeStates.UpsertAsync(child.JobName, child.TriggerName, child.RuntimeState, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(child.Occurrence, null, "OccurrenceCreated", child.Occurrence.Status, null, null, cancellationToken).ConfigureAwait(false);
            await this.CreateChainedOccurrencesAsync(child.Occurrence, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<JobBatch> FindBatchByPublicIdAsync(string batchId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return null;
        }

        var normalized = batchId.Trim();
        var batches = await storeProvider.Batches.ListAsync(cancellationToken).ConfigureAwait(false);
        return batches.FirstOrDefault(x =>
            string.Equals(x.ExternalBatchId, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.BatchId.ToString("D"), normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.BatchId.ToString("N"), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<JobBatch> FindExistingBatchAsync(string batchId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var batches = await storeProvider.Batches.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(batchId))
        {
            var normalizedBatchId = batchId.Trim();
            var existing = batches.FirstOrDefault(x => string.Equals(x.ExternalBatchId, normalizedBatchId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var normalizedIdempotencyKey = idempotencyKey.Trim();
            return batches.FirstOrDefault(x => string.Equals(x.IdempotencyKey, normalizedIdempotencyKey, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private async Task<JobBatchDispatchResult> BuildBatchDispatchResultAsync(JobBatch batch, CancellationToken cancellationToken)
    {
        var refreshed = await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        var memberships = await storeProvider.Batches.ListOccurrencesAsync(refreshed.BatchId, cancellationToken).ConfigureAwait(false);
        return new JobBatchDispatchResult
        {
            BatchId = refreshed.ExternalBatchId ?? refreshed.BatchId.ToString("N"),
            Status = refreshed.Status,
            AcceptedCount = refreshed.AcceptedCount,
            OccurrenceIds = memberships.Select(x => x.OccurrenceId).ToArray(),
        };
    }

    private async Task<JobBatch> RefreshBatchAsync(JobBatch batch, CancellationToken cancellationToken)
    {
        if (batch is null)
        {
            return null;
        }

        batch = await storeProvider.Batches.GetAsync(batch.BatchId, cancellationToken).ConfigureAwait(false) ?? batch;

        var memberships = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        var refreshedMemberships = new List<JobBatchOccurrence>(memberships.Count);
        foreach (var membership in memberships)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(membership.OccurrenceId, cancellationToken).ConfigureAwait(false);
            refreshedMemberships.Add(membership with
            {
                ChildStatus = occurrence?.Status ?? membership.ChildStatus,
                UpdatedDate = occurrence?.UpdatedDate ?? membership.UpdatedDate,
            });
        }

        await storeProvider.Batches.ReplaceOccurrencesAsync(batch.BatchId, refreshedMemberships, cancellationToken).ConfigureAwait(false);

        var status = ResolveBatchStatus(batch, refreshedMemberships);
        var updatedBatch = batch with
        {
            Status = status,
            AcceptedCount = refreshedMemberships.Count,
            SucceededCount = refreshedMemberships.Count(x => x.ChildStatus == JobOccurrenceStatus.Completed),
            FailedCount = refreshedMemberships.Count(x => x.ChildStatus == JobOccurrenceStatus.Failed),
            CancelledCount = refreshedMemberships.Count(x => x.ChildStatus == JobOccurrenceStatus.Cancelled),
            ArchivedCount = refreshedMemberships.Count(x => x.ChildStatus == JobOccurrenceStatus.Archived),
            CompletedDate = IsTerminalBatchStatus(status)
                ? batch.CompletedDate ?? timeProvider.GetUtcNow()
                : null,
            UpdatedDate = timeProvider.GetUtcNow(),
        };

        await storeProvider.Batches.UpdateAsync(updatedBatch, cancellationToken).ConfigureAwait(false);
        if (batch.Status != updatedBatch.Status)
        {
            await this.AppendBatchHistoryAsync(updatedBatch, "BatchStatusChanged", $"Batch status changed from '{batch.Status}' to '{updatedBatch.Status}'.", cancellationToken, new PropertyBag
            {
                ["previousStatus"] = batch.Status.ToString(),
                ["status"] = updatedBatch.Status.ToString(),
            }).ConfigureAwait(false);
        }

        return updatedBatch;
    }

    private async Task RefreshBatchesForOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        var memberships = await storeProvider.Queries.ListBatchOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var batchId in memberships.Where(x => x.OccurrenceId == occurrenceId).Select(x => x.BatchId).Distinct())
        {
            var batch = await storeProvider.Batches.GetAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (batch is not null)
            {
                await this.RefreshBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task UpdateOccurrenceAsync(JobOccurrence occurrence, CancellationToken cancellationToken)
    {
        await storeProvider.Occurrences.UpdateAsync(occurrence, cancellationToken).ConfigureAwait(false);
        await this.RefreshBatchesForOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> TryPreventExecutionForCancelledBatchAsync(JobOccurrence occurrence, CancellationToken cancellationToken)
    {
        var memberships = await storeProvider.Queries.ListBatchOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var membership = memberships.FirstOrDefault(x => x.OccurrenceId == occurrence.OccurrenceId);
        if (membership is null)
        {
            return null;
        }

        var batch = await storeProvider.Batches.GetAsync(membership.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return null;
        }

        if (batch.ArchivedDate.HasValue || batch.Status == JobBatchStatus.Archived)
        {
            return $"The occurrence '{occurrence.OccurrenceId}' belongs to archived batch '{batch.ExternalBatchId}'.";
        }

        if (!batch.CancellationRequestedDate.HasValue)
        {
            return null;
        }

        if (occurrence.Status is not (JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived))
        {
            await this.CancelOccurrenceAsync(occurrence.OccurrenceId, $"The parent batch '{batch.ExternalBatchId}' was cancelled.", cancellationToken).ConfigureAwait(false);
        }

        return $"The occurrence '{occurrence.OccurrenceId}' cannot start because batch '{batch.ExternalBatchId}' was cancelled.";
    }

    private async Task<Result<PreparedManualOccurrence>> PrepareManualOccurrenceAsync(
        JobDefinition definition,
        JobTriggerDefinition trigger,
        object data,
        JobDispatchOptions options,
        DateTimeOffset? dueUtcOverride,
        PropertyBag extraMetadata,
        string causationId,
        CancellationToken cancellationToken)
    {
        options ??= new JobDispatchOptions();
        var runtimeState = await storeProvider.TriggerRuntimeStates.GetAsync(definition.JobName, trigger.TriggerName, cancellationToken).ConfigureAwait(false)
            ?? JobTriggerRuntimeState.Empty;

        var correlationId = NormalizeCorrelationId(options.CorrelationId);
        var idempotencyKey = NormalizeIdempotencyKey(options.IdempotencyKey, Guid.NewGuid().ToString("N"));
        var evaluation = triggerEvaluator.Materialize(
            definition,
            trigger,
            new JobTriggerEvaluationRequest
            {
                RuntimeState = runtimeState,
                ManualDispatchRequested = true,
                DispatchRequestedUtc = timeProvider.GetUtcNow(),
                DispatchIdentity = idempotencyKey,
                OverrideData = data,
                OverrideProperties = MergeProperties(options.Properties, extraMetadata),
            });

        if (evaluation.IsFailure)
        {
            return Result<PreparedManualOccurrence>.Failure().WithErrors(evaluation.Errors).WithMessages(evaluation.Messages);
        }

        var materialized = evaluation.Value.Occurrences.SingleOrDefault();
        if (materialized is null)
        {
            return Result<PreparedManualOccurrence>.Failure().WithError(new ValidationError($"The manual trigger '{trigger.TriggerName}' on job '{definition.JobName}' did not produce an occurrence."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var dueUtc = dueUtcOverride ?? materialized.DueUtc;
        var occurrence = new JobOccurrence
        {
            OccurrenceId = Guid.NewGuid(),
            OccurrenceKey = materialized.OccurrenceKey,
            JobName = materialized.JobName,
            TriggerName = materialized.TriggerName,
            TriggerType = materialized.TriggerType,
            Status = JobOccurrenceStatus.Pending,
            DueUtc = dueUtc,
            ScheduledUtc = dueUtcOverride ?? materialized.ScheduledUtc ?? dueUtc,
            Data = materialized.Data,
            DataType = materialized.DataType,
            Properties = materialized.Properties,
            CorrelationId = correlationId,
            CausationId = string.IsNullOrWhiteSpace(causationId) ? correlationId : causationId,
            IdempotencyKey = string.IsNullOrWhiteSpace(materialized.IdempotencyKey) ? idempotencyKey : materialized.IdempotencyKey,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc,
        };

        return Result<PreparedManualOccurrence>.Success(new PreparedManualOccurrence(occurrence, evaluation.Value.RuntimeState));
    }

    private async Task<JobBulkOperationResult> ExecuteBatchOccurrenceOperationAsync(
        IReadOnlyList<Guid> occurrenceIds,
        string reason,
        Func<Guid, string, CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        var failures = new List<JobBulkOperationFailureModel>();
        var succeededCount = 0;
        foreach (var occurrenceId in occurrenceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation(occurrenceId, reason, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                succeededCount++;
                continue;
            }

            var occurrence = await storeProvider.Occurrences.GetAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
            failures.Add(new JobBulkOperationFailureModel
            {
                OccurrenceId = occurrenceId,
                JobName = occurrence?.JobName,
                Message = BuildMessage(result.Errors, result.Messages),
            });
        }

        return new JobBulkOperationResult
        {
            RequestedCount = occurrenceIds.Count,
            SucceededCount = succeededCount,
            FailedCount = failures.Count,
            Failures = failures,
        };
    }

    private async Task<Result<JobBulkOperationResult>> ExecuteOccurrenceOperationAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason,
        Func<Guid, string, CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        var selectedIds = occurrenceIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (selectedIds.Length == 0)
        {
            return Result<JobBulkOperationResult>.Failure().WithError(new ValidationError("At least one occurrence id must be provided."));
        }

        return Result<JobBulkOperationResult>.Success(
            await this.ExecuteBatchOccurrenceOperationAsync(selectedIds, reason, operation, cancellationToken).ConfigureAwait(false));
    }

    private static JobDispatchOptions BuildBatchItemOptions(JobBatch batch, JobBatchDispatchRequest request, JobBatchDispatchItem item, int index)
    {
        var operationIdempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey, batch.IdempotencyKey);
        return new JobDispatchOptions
        {
            TriggerName = item.Options?.TriggerName,
            CorrelationId = string.IsNullOrWhiteSpace(item.Options?.CorrelationId)
                ? batch.CorrelationId
                : item.Options.CorrelationId,
            IdempotencyKey = NormalizeIdempotencyKey(item.Options?.IdempotencyKey, $"{operationIdempotencyKey}:item:{index}"),
            Properties = MergeProperties(request.Properties, item.Options?.Properties),
            Durable = true,
        };
    }

    private static PropertyBag BuildBatchItemProperties(JobBatch batch, JobBatchDispatchItem item, int index)
    {
        var properties = new PropertyBag
        {
            ["batch:id"] = batch.ExternalBatchId ?? batch.BatchId.ToString("N"),
            ["batch:sequence"] = (item.Sequence ?? index + 1).ToString(),
        };

        if (!string.IsNullOrWhiteSpace(item.SourceStep))
        {
            properties["batch:sourceStep"] = item.SourceStep.Trim();
        }

        return properties;
    }

    private static PropertyBag CloneProperties(PropertyBag properties)
        => properties?.Clone() ?? new PropertyBag();

    private static PropertyBag MergeProperties(
        PropertyBag primary,
        PropertyBag secondary)
    {
        var properties = CloneProperties(primary);
        properties.Merge(secondary);
        return properties;
    }

    private static string NormalizeBatchId(string batchId)
    {
        return string.IsNullOrWhiteSpace(batchId) ? Guid.NewGuid().ToString("N") : batchId.Trim();
    }

    private static string NormalizeCorrelationId(string correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey, string fallback)
    {
        return string.IsNullOrWhiteSpace(idempotencyKey) ? fallback : idempotencyKey.Trim();
    }

    private static JobBatchStatus ResolveBatchStatus(JobBatch batch, IReadOnlyList<JobBatchOccurrence> memberships)
    {
        if (batch.ArchivedDate.HasValue || batch.Status == JobBatchStatus.Archived)
        {
            return JobBatchStatus.Archived;
        }

        if (memberships.Count == 0)
        {
            return batch.CancellationRequestedDate.HasValue ? JobBatchStatus.Cancelled : JobBatchStatus.Completed;
        }

        if (memberships.Any(x => !IsTerminalOccurrenceStatus(x.ChildStatus)))
        {
            return JobBatchStatus.Processing;
        }

        if (batch.CancellationRequestedDate.HasValue && memberships.All(x => x.ChildStatus is JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived))
        {
            return JobBatchStatus.Cancelled;
        }

        if (memberships.Any(x => x.ChildStatus is JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled))
        {
            return batch.CompletionPolicy == JobBatchCompletionPolicy.AllowPartialCompletion
                ? JobBatchStatus.CompletedWithFailures
                : JobBatchStatus.Failed;
        }

        return JobBatchStatus.Completed;
    }

    private static bool IsTerminalBatchStatus(JobBatchStatus status)
    {
        return status is JobBatchStatus.Completed or JobBatchStatus.CompletedWithFailures or JobBatchStatus.Failed or JobBatchStatus.Cancelled or JobBatchStatus.Archived;
    }

    private sealed record PreparedManualOccurrence(JobOccurrence Occurrence, JobTriggerRuntimeState RuntimeState);

    private sealed record PreparedBatchChild(
        JobOccurrence Occurrence,
        JobTriggerRuntimeState RuntimeState,
        string JobName,
        string TriggerName,
        int Sequence,
        bool Created);

    private sealed record PreparedBatchChildren(IReadOnlyList<PreparedBatchChild> Children);

    private static JobOccurrenceDependency ResolveDependencyState(JobOccurrenceDependency dependency, JobOccurrence prerequisite, DateTimeOffset nowUtc)
    {
        string reason;
        JobDependencyStatus status;

        if (prerequisite is null)
        {
            status = JobDependencyStatus.Pending;
            reason = $"Waiting for prerequisite occurrence '{dependency.PrerequisiteOccurrenceId}' to become available.";
        }
        else if (dependency.RequiredStatuses.Contains(prerequisite.Status))
        {
            status = JobDependencyStatus.Satisfied;
            reason = $"Prerequisite occurrence '{dependency.PrerequisiteOccurrenceId}' satisfied the dependency with status '{prerequisite.Status}'.";
        }
        else if (IsTerminalOccurrenceStatus(prerequisite.Status))
        {
            status = dependency.FailurePolicy switch
            {
                JobDependencyFailurePolicy.Skip => JobDependencyStatus.Skipped,
                JobDependencyFailurePolicy.Cancel => JobDependencyStatus.Cancelled,
                _ => JobDependencyStatus.Failed,
            };
            reason = $"Prerequisite occurrence '{dependency.PrerequisiteOccurrenceId}' reached terminal status '{prerequisite.Status}' at {nowUtc:O} without satisfying the dependency.";
        }
        else
        {
            status = JobDependencyStatus.Pending;
            reason = BuildBlockedReason(dependency.PrerequisiteOccurrenceId, dependency.RequiredStatuses);
        }

        return dependency with
        {
            Status = status,
            Reason = reason,
            UpdatedDate = nowUtc,
        };
    }

    private static string GetDependencyEventName(JobDependencyStatus status)
    {
        return status switch
        {
            JobDependencyStatus.Pending => "DependencyPending",
            JobDependencyStatus.Satisfied => "DependencySatisfied",
            JobDependencyStatus.Failed => "DependencyFailed",
            JobDependencyStatus.Skipped => "DependencySkipped",
            JobDependencyStatus.Cancelled => "DependencyCancelled",
            _ => "DependencyUpdated",
        };
    }

    private static string BuildBlockedReason(Guid prerequisiteOccurrenceId, IReadOnlyList<JobOccurrenceStatus> requiredStatuses)
    {
        var required = requiredStatuses?.Count > 0
            ? string.Join(", ", requiredStatuses)
            : JobOccurrenceStatus.Completed.ToString();
        return $"Waiting for prerequisite occurrence '{prerequisiteOccurrenceId}' to reach one of: {required}.";
    }

    private static JobTriggerDefinition ResolveChainTrigger(JobDefinition successor, JobChainDefinition chain)
    {
        if (successor is null)
        {
            throw new InvalidOperationException($"The chained successor job '{chain.SuccessorJobName}' is not registered.");
        }

        if (!string.IsNullOrWhiteSpace(chain.SuccessorTriggerName))
        {
            return successor.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, chain.SuccessorTriggerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The chained successor job '{successor.JobName}' does not define trigger '{chain.SuccessorTriggerName}'.");
        }

        var manualTriggers = successor.Triggers.Where(x => x.TriggerType == JobTriggerType.Manual).ToArray();
        return manualTriggers.Length switch
        {
            1 => manualTriggers[0],
            0 => throw new InvalidOperationException($"The chained successor job '{successor.JobName}' requires exactly one manual trigger when no successor trigger name is configured."),
            _ => throw new InvalidOperationException($"The chained successor job '{successor.JobName}' has multiple manual triggers. Configure the successor trigger explicitly."),
        };
    }

    private static bool IsTerminalOccurrenceStatus(JobOccurrenceStatus status)
    {
        return status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived;
    }

    private async Task AppendRegistrationHistoryAsync(
        string jobName,
        string triggerName,
        string eventName,
        string message,
        CancellationToken cancellationToken)
    {
        await storeProvider.ExecutionHistory.AppendAsync(
            new JobExecutionHistoryEntry
            {
                HistoryId = Guid.NewGuid(),
                OccurrenceId = Guid.Empty,
                ExecutionId = null,
                JobName = jobName,
                TriggerName = triggerName,
                SchedulerInstanceId = this.schedulerInstanceId,
                EventName = eventName,
                Message = message,
                RecordedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendHistoryAsync(
        JobOccurrence occurrence,
        Guid? executionId,
        string eventName,
        JobOccurrenceStatus? occurrenceStatus,
        JobExecutionStatus? executionStatus,
        string message,
        CancellationToken cancellationToken)
    {
        await storeProvider.ExecutionHistory.AppendAsync(
            new JobExecutionHistoryEntry
            {
                HistoryId = Guid.NewGuid(),
                OccurrenceId = occurrence.OccurrenceId,
                ExecutionId = executionId,
                JobName = occurrence.JobName,
                TriggerName = occurrence.TriggerName,
                SchedulerInstanceId = this.schedulerInstanceId,
                EventName = eventName,
                OccurrenceStatus = occurrenceStatus,
                ExecutionStatus = executionStatus,
                Message = message,
                RecordedAt = timeProvider.GetUtcNow(),
                Properties = occurrence.Properties,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendBatchOperationHistoryAsync(
        JobBatch batch,
        string eventName,
        string reason,
        JobBulkOperationResult result,
        CancellationToken cancellationToken)
    {
        await this.AppendBatchHistoryAsync(batch, eventName, reason ?? eventName, cancellationToken, new PropertyBag
        {
            ["requestedCount"] = result.RequestedCount,
            ["succeededCount"] = result.SucceededCount,
            ["failedCount"] = result.FailedCount,
        }).ConfigureAwait(false);
    }

    private async Task AppendBatchHistoryAsync(
        JobBatch batch,
        string eventName,
        string message,
        CancellationToken cancellationToken,
        PropertyBag properties = null)
    {
        if (batch is null)
        {
            return;
        }

        await storeProvider.BatchHistory.AppendAsync(
            new JobBatchHistoryEntry
            {
                HistoryId = Guid.NewGuid(),
                BatchId = batch.BatchId,
                ExternalBatchId = batch.ExternalBatchId,
                SchedulerInstanceId = this.schedulerInstanceId,
                EventName = eventName,
                BatchStatus = batch.Status,
                Message = message,
                Properties = properties ?? new PropertyBag(),
                RecordedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private JobDefinition ResolveJobDefinition(Type jobType)
    {
        return registrations.GetDefinitions().FirstOrDefault(x => x.JobType == jobType);
    }

    private JobDefinition ResolveJobDefinition(string jobName)
    {
        return registrations.GetDefinitions().FirstOrDefault(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExecutionEnabled(
        JobDefinition definition,
        string triggerName,
        IReadOnlyDictionary<string, JobRuntimeState> runtimeStates,
        IReadOnlyDictionary<(string JobName, string TriggerName), JobTriggerRuntimeState> triggerStates)
    {
        var trigger = definition.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase));
        if (trigger is null)
        {
            return false;
        }

        runtimeStates.TryGetValue(definition.JobName, out var jobState);
        if (jobState is { Enabled: false } || (jobState is not { Enabled: true } && !definition.Enabled) || jobState is { Paused: true })
        {
            return false;
        }

        triggerStates.TryGetValue((definition.JobName, trigger.TriggerName), out var triggerState);
        return (triggerState is { Enabled: true } || trigger.Enabled)
            && triggerState is not { Enabled: false }
            && triggerState is not { Paused: true };
    }

    private static int GetEffectivePriority(JobDefinition definition, string triggerName)
    {
        return definition.Triggers.FirstOrDefault(x => string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase))?.Priority ?? definition.Priority;
    }

    private async Task<JobLeaseRecord> AcquireLeaseAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        using var activity = JobSchedulerInstrumentation.StartLeaseActivity("jobs.lease.acquire", this.schedulerInstanceId, occurrenceId, this.schedulerInstanceId);
        var lease = await storeProvider.Leases.TryAcquireAsync(occurrenceId, this.schedulerInstanceId, this.options.LeaseDuration, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("jobs.operation.success", lease is not null);
        if (lease is not null)
        {
            JobSchedulerInstrumentation.RecordLeaseAcquired(this.schedulerInstanceId, occurrenceId, lease.SchedulerInstanceId);
        }

        return lease;
    }

    private async Task<bool> VerifyLeaseOwnershipAsync(ActiveExecutionState active, Guid occurrenceId, CancellationToken cancellationToken)
    {
        var owned = await storeProvider.Leases.VerifyOwnershipAsync(
            occurrenceId,
            active.Lease.SchedulerInstanceId,
            active.Lease.OwnershipToken,
            cancellationToken).ConfigureAwait(false);

        if (!owned)
        {
            active.LeaseLost = true;
        }

        return owned;
    }

    private async Task ReleaseLeaseAsync(ActiveExecutionState active, Guid occurrenceId, CancellationToken cancellationToken)
    {
        await storeProvider.Leases.ReleaseAsync(
            occurrenceId,
            active.Lease.SchedulerInstanceId,
            active.Lease.OwnershipToken,
            cancellationToken).ConfigureAwait(false);
    }

    private Task StartLeaseRenewalLoopAsync(Guid occurrenceId, ActiveExecutionState active, CancellationToken cancellationToken)
    {
        if (this.options.LeaseRenewalInterval <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                while (!active.CompletionSource.IsCancellationRequested)
                {
                    await Task.Delay(this.options.LeaseRenewalInterval, timeProvider, active.CompletionSource.Token).ConfigureAwait(false);
                    using var activity = JobSchedulerInstrumentation.StartLeaseActivity("jobs.lease.renew", this.schedulerInstanceId, occurrenceId, active.Lease.SchedulerInstanceId);
                    var renewed = await storeProvider.Leases.RenewAsync(
                        occurrenceId,
                        active.Lease.SchedulerInstanceId,
                        active.Lease.OwnershipToken,
                        this.options.LeaseDuration,
                        cancellationToken).ConfigureAwait(false);

                    if (renewed is null)
                    {
                        activity?.SetTag("jobs.operation.success", false);
                        active.LeaseLost = true;
                        active.CancellationSource.Cancel();
                        return;
                    }

                    activity?.SetTag("jobs.operation.success", true);
                    JobSchedulerInstrumentation.RecordLeaseRenewed(this.schedulerInstanceId, occurrenceId, renewed.SchedulerInstanceId);
                    active.Lease = renewed;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when execution completes.
            }
        }, CancellationToken.None);
    }

    private void CompleteActiveExecution(Guid occurrenceId, ActiveExecutionState active)
    {
        active.CompletionSource.Cancel();
        this.activeExecutions.TryRemove(occurrenceId, out _);
    }

    private static JobOccurrenceStatus GetResumedOccurrenceStatus(JobOccurrence occurrence, DateTimeOffset nowUtc)
    {
        var resumeStatus = occurrence.ResumeStatus ?? (occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled);
        return resumeStatus switch
        {
            JobOccurrenceStatus.Pending => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.Scheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.RetryScheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.RetryScheduled,
            JobOccurrenceStatus.Due => JobOccurrenceStatus.Due,
            JobOccurrenceStatus.Blocked => JobOccurrenceStatus.Blocked,
            _ => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
        };
    }

    private static JobOccurrenceStatus GetRecoveredOccurrenceStatus(JobOccurrence occurrence, DateTimeOffset nowUtc)
    {
        return occurrence.Status switch
        {
            JobOccurrenceStatus.Running => JobOccurrenceStatus.Due,
            JobOccurrenceStatus.RetryScheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.RetryScheduled,
            JobOccurrenceStatus.Pending => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.Scheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.Due => JobOccurrenceStatus.Due,
            JobOccurrenceStatus.Blocked => JobOccurrenceStatus.Blocked,
            _ => occurrence.Status,
        };
    }

    private static bool IsTerminalExecutionStatus(JobExecutionStatus status)
    {
        return status is JobExecutionStatus.Completed or JobExecutionStatus.Failed or JobExecutionStatus.TimedOut or JobExecutionStatus.Cancelled or JobExecutionStatus.Interrupted;
    }

    private static IReadOnlyList<string> MergeMessages(
        IEnumerable<string> contextMessages,
        IEnumerable<string> resultMessages,
        IEnumerable<string> errorMessages)
    {
        return contextMessages
            .Concat(resultMessages ?? [])
            .Concat(errorMessages ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildMessage(IEnumerable<IResultError> errors, IEnumerable<string> messages)
    {
        return MergeMessages([], messages, errors?.Select(x => x.Message)).LastOrDefault();
    }

    private static Result TraceManagementResult(Activity activity, string operation, Result result, string jobName = null, string triggerName = null, Guid? occurrenceId = null)
    {
        activity?.SetTag("jobs.operation.success", result.IsSuccess);
        if (!result.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, BuildMessage(result.Errors, result.Messages));
        }

        JobSchedulerInstrumentation.RecordManagementOperation(operation, result.IsSuccess, jobName, triggerName, occurrenceId);
        return result;
    }

    private static Result<T> TraceManagementResult<T>(Activity activity, string operation, Result<T> result, string jobName = null, string triggerName = null, Guid? occurrenceId = null)
    {
        activity?.SetTag("jobs.operation.success", result.IsSuccess);
        if (!result.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, BuildMessage(result.Errors, result.Messages));
        }

        JobSchedulerInstrumentation.RecordManagementOperation(operation, result.IsSuccess, jobName, triggerName, occurrenceId);
        return result;
    }

    private static TimeSpan? GetRetryDelay(JobRetryPolicy retryPolicy, int nextAttemptNumber)
    {
        if (retryPolicy?.Delay is null)
        {
            return null;
        }

        if (!retryPolicy.UseExponentialBackoff)
        {
            return retryPolicy.Delay;
        }

        var exponent = Math.Max(0, nextAttemptNumber - 2);
        return TimeSpan.FromMilliseconds(retryPolicy.Delay.Value.TotalMilliseconds * Math.Pow(2, exponent));
    }

    private static string GetFailureExecutionEventName(JobExecutionStatus status)
    {
        return status == JobExecutionStatus.TimedOut ? "ExecutionTimedOut" : "ExecutionFailed";
    }

    private static string GetFailureOccurrenceEventName(JobExecutionStatus status)
    {
        return status == JobExecutionStatus.TimedOut ? "OccurrenceTimedOut" : "OccurrenceFailed";
    }

    private static string GetCancellationExecutionEventName(JobExecutionStatus status)
    {
        return status == JobExecutionStatus.Interrupted ? "ExecutionInterrupted" : "ExecutionCancelled";
    }

    private static string GetCancellationOccurrenceEventName(JobExecutionStatus status)
    {
        return status == JobExecutionStatus.Interrupted ? "OccurrenceInterrupted" : "OccurrenceCancelled";
    }

    private sealed record PreparedDispatch(JobDispatchResult DispatchResult, JobExecutionResult ExecutionResult, Exception ExecutionFailure);

    private sealed class ActiveExecutionState(Guid executionId, JobLeaseRecord lease, string jobName, string triggerName)
    {
        public Guid ExecutionId { get; } = executionId;

        public JobLeaseRecord Lease { get; set; } = lease;

        public string JobName { get; } = jobName;

        public string TriggerName { get; } = triggerName;

        public CancellationTokenSource CancellationSource { get; } = new();

        public CancellationTokenSource CompletionSource { get; } = new();

        public bool CancelRequested { get; set; }

        public bool InterruptRequested { get; set; }

        public bool LeaseLost { get; set; }

        public string Reason { get; set; }
    }
}
