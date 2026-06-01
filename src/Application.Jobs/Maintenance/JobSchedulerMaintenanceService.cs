// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the default provider-neutral maintenance implementation for scheduler state.
/// </summary>
public sealed class JobSchedulerMaintenanceService(
    TimeProvider timeProvider,
    JobRegistrationStore registrations,
    IJobStoreProvider storeProvider,
    JobSchedulerService scheduler) : IJobSchedulerMaintenanceService
{
    public async Task<JobMaintenanceReport> ArchiveOccurrencesAsync(JobArchiveOccurrencesJobData options, CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("archive-occurrences", options?.JobName, options?.TriggerName);
        options ??= new JobArchiveOccurrencesJobData();

        var statuses = (options.Statuses is { Count: > 0 } ? options.Statuses :
        [
            JobOccurrenceStatus.Completed,
            JobOccurrenceStatus.Failed,
            JobOccurrenceStatus.Cancelled,
        ])
            .Where(static x => x is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled)
            .Distinct()
            .ToArray();
        var cutoffUtc = timeProvider.GetUtcNow().Subtract(options.RetentionWindow);
        var matched = (await storeProvider.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => statuses.Contains(x.Status))
            .Where(x => x.UpdatedDate <= cutoffUtc)
            .Where(x => string.IsNullOrWhiteSpace(options.JobName) || string.Equals(x.JobName, options.JobName, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(options.TriggerName) || string.Equals(x.TriggerName, options.TriggerName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.UpdatedDate)
            .ThenBy(x => x.OccurrenceId)
            .ToArray();
        var selected = matched.Take(Math.Max(1, options.BatchSize)).ToArray();
        var affectedIds = selected.Select(x => x.OccurrenceId.ToString("N")).ToArray();

        if (options.DryRun || selected.Length == 0)
        {
            return new JobMaintenanceReport(
                "jobs-archive-occurrences",
                options.DryRun,
                matched.Length,
                0,
                Math.Max(0, matched.Length - selected.Length),
                affectedIds,
                [
                    $"cutoff={cutoffUtc:O}",
                    string.IsNullOrWhiteSpace(options.JobName) ? "job=<any>" : $"job={options.JobName}",
                    string.IsNullOrWhiteSpace(options.TriggerName) ? "trigger=<any>" : $"trigger={options.TriggerName}",
                    $"statuses={string.Join(",", statuses)}",
                    $"would archive {selected.Length} occurrence(s)"
                ]);
        }

        var processed = 0;
        foreach (var occurrence in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await scheduler.ArchiveOccurrenceAsync(
                occurrence.OccurrenceId,
                $"Archived by maintenance job after retention window '{options.RetentionWindow}'.",
                cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                processed++;
            }
        }

        var report = new JobMaintenanceReport(
            "jobs-archive-occurrences",
            false,
            matched.Length,
            processed,
            Math.Max(0, matched.Length - selected.Length),
            affectedIds,
            [
                $"cutoff={cutoffUtc:O}",
                string.IsNullOrWhiteSpace(options.JobName) ? "job=<any>" : $"job={options.JobName}",
                string.IsNullOrWhiteSpace(options.TriggerName) ? "trigger=<any>" : $"trigger={options.TriggerName}",
                $"statuses={string.Join(",", statuses)}",
                $"archivedOccurrences={processed}"
            ]);
        activity?.SetTag("jobs.maintenance.matched", matched.Length);
        activity?.SetTag("jobs.maintenance.processed", processed);
        JobSchedulerInstrumentation.RecordManagementOperation("archive-occurrences", true, options.JobName, options.TriggerName);
        return report;
    }

    public async Task<JobMaintenanceReport> PurgeOccurrencesAsync(JobPurgeOccurrencesRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("purge-occurrences", request?.JobName, request?.TriggerName);
        request ??= new JobPurgeOccurrencesRequest();

        var occurrences = await storeProvider.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var matched = occurrences
            .Where(x => IsTerminalOccurrenceStatus(x.Status))
            .Where(x => !request.OlderThan.HasValue || x.UpdatedDate <= request.OlderThan.Value)
            .Where(x => request.Statuses is null || request.Statuses.Count == 0 || request.Statuses.Contains(x.Status))
            .Where(x => string.IsNullOrWhiteSpace(request.JobName) || string.Equals(x.JobName, request.JobName, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(request.TriggerName) || string.Equals(x.TriggerName, request.TriggerName, StringComparison.OrdinalIgnoreCase))
            .Where(x => !request.IsArchived.HasValue || request.IsArchived.Value == (x.Status == JobOccurrenceStatus.Archived))
            .OrderBy(x => x.UpdatedDate)
            .ThenBy(x => x.OccurrenceId)
            .ToArray();
        var selected = matched.Take(Math.Max(1, request.BatchSize)).ToArray();
        var affectedIds = selected.Select(x => x.OccurrenceId.ToString("N")).ToArray();

        if (request.DryRun || selected.Length == 0)
        {
            return new JobMaintenanceReport(
                "jobs-purge-occurrences",
                request.DryRun,
                matched.Length,
                0,
                Math.Max(0, matched.Length - selected.Length),
                affectedIds,
                [
                    request.OlderThan.HasValue ? $"olderThan={request.OlderThan:O}" : "olderThan=<none>",
                    string.IsNullOrWhiteSpace(request.JobName) ? "job=<any>" : $"job={request.JobName}",
                    string.IsNullOrWhiteSpace(request.TriggerName) ? "trigger=<any>" : $"trigger={request.TriggerName}",
                    request.IsArchived.HasValue ? $"isArchived={request.IsArchived.Value}" : "isArchived=<any>",
                    $"would purge {selected.Length} occurrence(s)"
                ]);
        }

        var selectedIds = selected.Select(x => x.OccurrenceId).ToArray();
        var historyIds = (await storeProvider.Queries.ListExecutionHistoryAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => selectedIds.Contains(x.OccurrenceId))
            .Select(x => x.HistoryId)
            .ToArray();
        var batchOccurrenceLinks = (await storeProvider.Queries.ListBatchOccurrencesAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => selectedIds.Contains(x.OccurrenceId))
            .ToArray();
        var affectedBatchIds = batchOccurrenceLinks.Select(x => x.BatchId).Distinct().ToArray();

        var removedHistoryCount = historyIds.Length == 0
            ? 0
            : await storeProvider.ExecutionHistory.PurgeAsync(DateTimeOffset.MaxValue, historyIds, cancellationToken).ConfigureAwait(false);
        var removedExecutionCount = 0;
        var removedDependencyCount = 0;
        foreach (var occurrence in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            removedExecutionCount += await storeProvider.Executions.RemoveByOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
            removedDependencyCount += await storeProvider.Dependencies.RemoveByOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
            await storeProvider.Leases.RemoveAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        }

        var removedBatchMembershipCount = await storeProvider.Batches.RemoveOccurrencesAsync(selectedIds, cancellationToken).ConfigureAwait(false);
        foreach (var occurrence in selected)
        {
            await storeProvider.Occurrences.RemoveAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
        }

        foreach (var batchId in affectedBatchIds)
        {
            await this.RefreshBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        }

        var report = new JobMaintenanceReport(
            "jobs-purge-occurrences",
            false,
            matched.Length,
            selected.Length,
            Math.Max(0, matched.Length - selected.Length),
            affectedIds,
            [
                request.OlderThan.HasValue ? $"olderThan={request.OlderThan:O}" : "olderThan=<none>",
                $"purgedOccurrences={selected.Length}",
                $"purgedExecutions={removedExecutionCount}",
                $"purgedDependencies={removedDependencyCount}",
                $"purgedHistory={removedHistoryCount}",
                $"purgedBatchMemberships={removedBatchMembershipCount}",
                $"affectedBatches={affectedBatchIds.Length}"
            ]);
            activity?.SetTag("jobs.maintenance.matched", matched.Length);
            activity?.SetTag("jobs.maintenance.processed", selected.Length);
            JobSchedulerInstrumentation.RecordManagementOperation("purge-occurrences", true, request.JobName, request.TriggerName);
            return report;
    }

    public async Task<JobMaintenanceReport> PurgeHistoryAsync(JobPurgeHistoryJobData options, CancellationToken cancellationToken = default)
    {
            using var activity = JobSchedulerInstrumentation.StartManagementActivity("purge-history");
        options ??= new JobPurgeHistoryJobData();
        var cutoffUtc = timeProvider.GetUtcNow().Subtract(options.RetentionWindow);
        var archivedOccurrenceIds = (await storeProvider.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => x.Status == JobOccurrenceStatus.Archived && x.UpdatedDate <= cutoffUtc)
            .Select(x => x.OccurrenceId)
            .ToHashSet();
        var history = await storeProvider.Queries.ListExecutionHistoryAsync(cancellationToken).ConfigureAwait(false);
        var matched = history
            .Where(x => archivedOccurrenceIds.Contains(x.OccurrenceId) && x.RecordedAt <= cutoffUtc)
            .OrderBy(x => x.RecordedAt)
            .ThenBy(x => x.HistoryId)
            .ToArray();

        var selected = matched.Take(Math.Max(1, options.BatchSize)).ToArray();
        var processed = options.DryRun || selected.Length == 0
            ? 0
            : await storeProvider.ExecutionHistory.PurgeAsync(cutoffUtc, selected.Select(x => x.HistoryId).ToArray(), cancellationToken).ConfigureAwait(false);

        var report = new JobMaintenanceReport(
            "jobs-purge-history",
            options.DryRun,
            matched.Length,
            processed,
            Math.Max(0, matched.Length - selected.Length),
            selected.Select(x => x.HistoryId.ToString("N")).ToArray(),
            [$"cutoff={cutoffUtc:O}", $"archivedOccurrences={archivedOccurrenceIds.Count}", options.DryRun ? $"would purge {selected.Length} history entries" : $"purged {processed} history entries"]);
        activity?.SetTag("jobs.maintenance.matched", matched.Length);
        activity?.SetTag("jobs.maintenance.processed", processed);
        JobSchedulerInstrumentation.RecordManagementOperation("purge-history", true);
        return report;
    }

    public async Task<JobMaintenanceReport> ReleaseExpiredLeasesAsync(JobReleaseExpiredLeasesJobData options, CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("release-expired-leases");
        options ??= new JobReleaseExpiredLeasesJobData();
        var nowUtc = timeProvider.GetUtcNow();
        var expiredLeases = (await storeProvider.Leases.ListExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false))
            .OrderBy(x => x.ExpiresUtc)
            .ThenBy(x => x.OccurrenceId)
            .Take(Math.Max(1, options.BatchSize))
            .ToArray();
        var affectedIds = new List<string>();
        var processed = 0;

        foreach (var lease in expiredLeases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var occurrence = await storeProvider.Occurrences.GetAsync(lease.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (options.DryRun)
            {
                affectedIds.Add(lease.OccurrenceId.ToString("N"));
                continue;
            }

            await storeProvider.Leases.ReleaseAsync(lease.OccurrenceId, lease.SchedulerInstanceId, lease.OwnershipToken, cancellationToken).ConfigureAwait(false);
            if (occurrence is null || occurrence.Status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived or JobOccurrenceStatus.Paused)
            {
                processed++;
                affectedIds.Add(lease.OccurrenceId.ToString("N"));
                continue;
            }

            var updated = occurrence with
            {
                Status = GetRecoveredOccurrenceStatus(occurrence, nowUtc),
                UpdatedDate = nowUtc,
            };
            await storeProvider.Occurrences.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(updated, "LeaseExpired", lease.SchedulerInstanceId, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(updated, "OccurrenceRecovered", null, cancellationToken).ConfigureAwait(false);
            processed++;
            affectedIds.Add(updated.OccurrenceId.ToString("N"));
        }

        var report = new JobMaintenanceReport(
            "jobs-release-expired-leases",
            options.DryRun,
            expiredLeases.Length,
            processed,
            0,
            affectedIds,
            [options.DryRun ? $"would process {expiredLeases.Length} expired leases" : $"processed {processed} expired leases"]);
        activity?.SetTag("jobs.maintenance.matched", expiredLeases.Length);
        activity?.SetTag("jobs.maintenance.processed", processed);
        JobSchedulerInstrumentation.RecordManagementOperation("release-expired-leases", true);
        return report;
    }

    public async Task<JobMaintenanceReport> RecoverStuckOccurrencesAsync(JobRecoverStuckOccurrencesJobData options, CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("recover-stuck-occurrences");
        options ??= new JobRecoverStuckOccurrencesJobData();
        var nowUtc = timeProvider.GetUtcNow();
        var cutoffUtc = nowUtc.Subtract(options.StuckFor);
        var leases = (await storeProvider.Leases.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(x => x.OccurrenceId, x => x);
        var candidates = (await storeProvider.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => x.Status is JobOccurrenceStatus.Running or JobOccurrenceStatus.Materialized or JobOccurrenceStatus.Scheduled or JobOccurrenceStatus.Due or JobOccurrenceStatus.RetryScheduled)
            .Where(x => x.UpdatedDate <= cutoffUtc)
            .Where(x => !leases.TryGetValue(x.OccurrenceId, out var lease) || lease.ExpiresUtc <= nowUtc)
            .OrderBy(x => x.UpdatedDate)
            .ThenBy(x => x.OccurrenceId)
            .Take(Math.Max(1, options.BatchSize))
            .ToArray();
        var affectedIds = new List<string>();
        var processed = 0;

        foreach (var occurrence in candidates)
        {
            if (options.DryRun)
            {
                affectedIds.Add(occurrence.OccurrenceId.ToString("N"));
                continue;
            }

            if (leases.TryGetValue(occurrence.OccurrenceId, out var lease) && lease.ExpiresUtc <= nowUtc)
            {
                await storeProvider.Leases.ReleaseAsync(lease.OccurrenceId, lease.SchedulerInstanceId, lease.OwnershipToken, cancellationToken).ConfigureAwait(false);
            }

            var updated = occurrence with
            {
                Status = GetRecoveredOccurrenceStatus(occurrence, nowUtc),
                UpdatedDate = nowUtc,
            };
            await storeProvider.Occurrences.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(updated, "OccurrenceRecoveredStuck", $"Recovered stale occurrence older than {options.StuckFor}.", cancellationToken).ConfigureAwait(false);
            processed++;
            affectedIds.Add(updated.OccurrenceId.ToString("N"));
        }

        var report = new JobMaintenanceReport(
            "jobs-recover-stuck-occurrences",
            options.DryRun,
            candidates.Length,
            processed,
            0,
            affectedIds,
            [$"cutoff={cutoffUtc:O}", options.DryRun ? $"would recover {candidates.Length} stuck occurrences" : $"recovered {processed} stuck occurrences"]);
        activity?.SetTag("jobs.maintenance.matched", candidates.Length);
        activity?.SetTag("jobs.maintenance.processed", processed);
        JobSchedulerInstrumentation.RecordManagementOperation("recover-stuck-occurrences", true);
        return report;
    }

    public async Task<JobMaintenanceReport> DetectOrphanedRuntimeStateAsync(JobDetectOrphanedRuntimeStateJobData options, CancellationToken cancellationToken = default)
    {
        using var activity = JobSchedulerInstrumentation.StartManagementActivity("detect-orphaned-runtime-state");
        options ??= new JobDetectOrphanedRuntimeStateJobData();
        var definitions = registrations.GetDefinitions();
        var jobNames = definitions.Select(x => x.JobName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var triggerNames = definitions
            .SelectMany(x => x.Triggers.Select(t => (x.JobName, t.TriggerName)))
            .ToHashSet();
        var orphanedJobs = (await storeProvider.RuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => !jobNames.Contains(x.JobName))
            .OrderBy(x => x.JobName)
            .Select(x => x.JobName)
            .ToArray();
        var orphanedTriggers = (await storeProvider.TriggerRuntimeStates.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => !triggerNames.Contains((x.JobName, x.TriggerName)))
            .OrderBy(x => x.JobName)
            .ThenBy(x => x.TriggerName)
            .Select(x => (x.JobName, x.TriggerName))
            .ToArray();
        var affectedIds = orphanedJobs.Select(x => $"job:{x}")
            .Concat(orphanedTriggers.Select(x => $"trigger:{x.JobName}/{x.TriggerName}"))
            .Take(Math.Max(1, options.BatchSize))
            .ToArray();
        var processed = 0;

        if (!options.DryRun)
        {
            foreach (var entry in affectedIds)
            {
                if (entry.StartsWith("job:", StringComparison.Ordinal))
                {
                    await storeProvider.RuntimeStates.RemoveAsync(entry[4..], cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var pair = entry[8..].Split('/', 2);
                    await storeProvider.TriggerRuntimeStates.RemoveAsync(pair[0], pair[1], cancellationToken).ConfigureAwait(false);
                }

                processed++;
            }
        }

        var matched = orphanedJobs.Length + orphanedTriggers.Length;
        var report = new JobMaintenanceReport(
            "jobs-detect-orphaned-runtime-state",
            options.DryRun,
            matched,
            processed,
            Math.Max(0, matched - affectedIds.Length),
            affectedIds,
            [options.DryRun ? $"would remove {affectedIds.Length} orphaned runtime-state rows" : $"removed {processed} orphaned runtime-state rows"]);
        activity?.SetTag("jobs.maintenance.matched", matched);
        activity?.SetTag("jobs.maintenance.processed", processed);
        JobSchedulerInstrumentation.RecordManagementOperation("detect-orphaned-runtime-state", true);
        return report;
    }

    private async Task AppendHistoryAsync(JobOccurrence occurrence, string eventName, string message, CancellationToken cancellationToken)
    {
        await storeProvider.ExecutionHistory.AppendAsync(
            new JobExecutionHistoryEntry
            {
                HistoryId = Guid.NewGuid(),
                OccurrenceId = occurrence.OccurrenceId,
                JobName = occurrence.JobName,
                TriggerName = occurrence.TriggerName,
                SchedulerInstanceId = scheduler.SchedulerInstanceId,
                EventName = eventName,
                OccurrenceStatus = occurrence.Status,
                Message = message,
                RecordedAt = timeProvider.GetUtcNow(),
                Properties = occurrence.Properties,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await storeProvider.Batches.GetAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return;
        }

        var memberships = await storeProvider.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
        var refreshedMemberships = new List<JobBatchOccurrence>(memberships.Count);
        foreach (var membership in memberships)
        {
            var occurrence = await storeProvider.Occurrences.GetAsync(membership.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (occurrence is null)
            {
                continue;
            }

            refreshedMemberships.Add(membership with
            {
                ChildStatus = occurrence.Status,
                UpdatedDate = occurrence.UpdatedDate,
            });
        }

        await storeProvider.Batches.ReplaceOccurrencesAsync(batch.BatchId, refreshedMemberships, cancellationToken).ConfigureAwait(false);

        var status = ResolveBatchStatus(batch, refreshedMemberships);
        await storeProvider.Batches.UpdateAsync(
            batch with
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
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static JobOccurrenceStatus GetRecoveredOccurrenceStatus(JobOccurrence occurrence, DateTimeOffset nowUtc)
    {
        return occurrence.Status switch
        {
            JobOccurrenceStatus.Running => JobOccurrenceStatus.Due,
            JobOccurrenceStatus.RetryScheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.RetryScheduled,
            JobOccurrenceStatus.Materialized => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.Scheduled => occurrence.DueUtc <= nowUtc ? JobOccurrenceStatus.Due : JobOccurrenceStatus.Scheduled,
            JobOccurrenceStatus.Due => JobOccurrenceStatus.Due,
            JobOccurrenceStatus.Blocked => JobOccurrenceStatus.Blocked,
            _ => occurrence.Status,
        };
    }

    private static JobBatchStatus ResolveBatchStatus(JobBatch batch, IReadOnlyList<JobBatchOccurrence> memberships)
    {
        if (batch.ArchivedDate.HasValue || batch.Status == JobBatchStatus.Archived)
        {
            return JobBatchStatus.Archived;
        }

        if (memberships.Count == 0)
        {
            return batch.CancellationRequestedDate.HasValue ? JobBatchStatus.Cancelled : JobBatchStatus.Created;
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
        => status is JobBatchStatus.Completed or JobBatchStatus.CompletedWithFailures or JobBatchStatus.Failed or JobBatchStatus.Cancelled or JobBatchStatus.Archived;

    private static bool IsTerminalOccurrenceStatus(JobOccurrenceStatus status)
        => status is JobOccurrenceStatus.Completed or JobOccurrenceStatus.Failed or JobOccurrenceStatus.Cancelled or JobOccurrenceStatus.Archived;
}