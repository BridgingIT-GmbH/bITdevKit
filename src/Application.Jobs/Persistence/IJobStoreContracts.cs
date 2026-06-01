// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Persists runtime state for registered jobs.
/// </summary>
public interface IJobRuntimeStateStore
{
    Task<JobRuntimeState> GetAsync(string jobName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobRuntimeState>> ListAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(JobRuntimeState state, CancellationToken cancellationToken = default);

    Task RemoveAsync(string jobName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists runtime state for registered triggers.
/// </summary>
public interface IJobTriggerRuntimeStateStore
{
    Task<JobTriggerRuntimeState> GetAsync(string jobName, string triggerName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>> ListAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(string jobName, string triggerName, JobTriggerRuntimeState state, CancellationToken cancellationToken = default);

    Task RemoveAsync(string jobName, string triggerName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists materialized occurrences.
/// </summary>
public interface IJobOccurrenceStore
{
    Task<bool> TryCreateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default);

    Task<JobOccurrence> GetAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    Task<JobOccurrence> GetByKeyAsync(string occurrenceKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOccurrence>> ListAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists execution attempts.
/// </summary>
public interface IJobExecutionStore
{
    Task CreateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    Task<JobExecution> GetAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobExecution>> ListByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    Task UpdateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    Task<int> RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists occurrence dependencies.
/// </summary>
public interface IJobOccurrenceDependencyStore
{
    Task AddAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default);

    Task UpdateAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOccurrenceDependency>> ListByDependentAsync(Guid dependentOccurrenceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOccurrenceDependency>> ListByPrerequisiteAsync(Guid prerequisiteOccurrenceId, CancellationToken cancellationToken = default);

    Task<int> RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists batch records and membership links.
/// </summary>
public interface IJobBatchStore
{
    Task<bool> TryCreateAsync(JobBatch batch, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    Task<bool> TryCreateWithChildrenAsync(
        JobBatch batch,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default);

    Task<JobBatch> GetAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatch>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatchOccurrence>> ListOccurrencesAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task AttachAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    Task<bool> TryAttachChildrenAsync(
        Guid batchId,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default);

    Task ReplaceOccurrencesAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    Task UpdateAsync(JobBatch batch, CancellationToken cancellationToken = default);

    Task<int> RemoveOccurrencesAsync(IReadOnlyCollection<Guid> occurrenceIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists lease properties.
/// </summary>
public interface IJobLeaseStore
{
    Task<JobLeaseRecord> GetAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobLeaseRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<JobLeaseRecord> TryAcquireAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task<JobLeaseRecord> RenewAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyOwnershipAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobLeaseRecord>> ListExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    Task UpsertAsync(JobLeaseRecord lease, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists append-oriented execution history.
/// </summary>
public interface IJobExecutionHistoryStore
{
    Task AppendAsync(JobExecutionHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobExecutionHistoryEntry>> ListAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists append-oriented batch history.
/// </summary>
public interface IJobBatchHistoryStore
{
    Task AppendAsync(JobBatchHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatchHistoryEntry>> ListAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists durably accepted event-trigger input records.
/// </summary>
public interface IJobAcceptedEventStore
{
    Task<bool> TryAcceptAsync(JobAcceptedEvent acceptedEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobAcceptedEvent>> ListPendingAsync(
        string source,
        Type eventDataType,
        DateTimeOffset? afterAcceptedUtc,
        Guid? afterAcceptedEventId,
        int take,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves previous execution attempts.
/// </summary>
public interface IJobPreviousExecutionStore
{
    Task<JobExecution> GetPreviousExecutionAsync(Guid occurrenceId, Guid currentExecutionId, CancellationToken cancellationToken = default);

    Task<JobExecution> GetPreviousSuccessfulExecutionAsync(string jobName, string triggerName, DateTimeOffset beforeUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exposes provider-neutral query access to persisted Jobs runtime records.
/// </summary>
public interface IJobSchedulerQueryStore
{
    Task<IReadOnlyList<JobOccurrence>> ListOccurrencesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobExecution>> ListExecutionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobExecutionHistoryEntry>> ListExecutionHistoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOccurrenceDependency>> ListDependenciesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatch>> ListBatchesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatchHistoryEntry>> ListBatchHistoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobBatchOccurrence>> ListBatchOccurrencesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobLeaseRecord>> ListLeasesAsync(CancellationToken cancellationToken = default);
}
