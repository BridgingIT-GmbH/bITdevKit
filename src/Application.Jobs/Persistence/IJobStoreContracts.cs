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
    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobRuntimeState> GetAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobRuntimeState>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the upsert operation.
    /// </summary>
    /// <param name="state">The state used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpsertAsync(JobRuntimeState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string jobName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists runtime state for registered triggers.
/// </summary>
public interface IJobTriggerRuntimeStateStore
{
    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="triggerName">The trigger name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobTriggerRuntimeState> GetAsync(string jobName, string triggerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the upsert operation.
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="triggerName">The trigger name used by the operation.</param>
    /// <param name="state">The state used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpsertAsync(string jobName, string triggerName, JobTriggerRuntimeState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="triggerName">The trigger name used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string jobName, string triggerName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists materialized occurrences.
/// </summary>
public interface IJobOccurrenceStore
{
    /// <summary>
    /// Executes the try create operation.
    /// </summary>
    /// <param name="occurrence">The occurrence used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> TryCreateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobOccurrence> GetAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets by key.
    /// </summary>
    /// <param name="occurrenceKey">The occurrence key used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobOccurrence> GetByKeyAsync(string occurrenceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobOccurrence>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="occurrence">The occurrence used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists execution attempts.
/// </summary>
public interface IJobExecutionStore
{
    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="execution">The execution used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="executionId">The execution id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobExecution> GetAsync(Guid executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list by occurrence operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobExecution>> ListByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="execution">The execution used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes by occurrence.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists occurrence dependencies.
/// </summary>
public interface IJobOccurrenceDependencyStore
{
    /// <summary>
    /// Adds .
    /// </summary>
    /// <param name="dependency">The dependency used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="dependency">The dependency used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list by dependent operation.
    /// </summary>
    /// <param name="dependentOccurrenceId">The dependent occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobOccurrenceDependency>> ListByDependentAsync(Guid dependentOccurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list by prerequisite operation.
    /// </summary>
    /// <param name="prerequisiteOccurrenceId">The prerequisite occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobOccurrenceDependency>> ListByPrerequisiteAsync(Guid prerequisiteOccurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes by occurrence.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists batch records and membership links.
/// </summary>
public interface IJobBatchStore
{
    /// <summary>
    /// Executes the try create operation.
    /// </summary>
    /// <param name="batch">The batch used by the operation.</param>
    /// <param name="occurrences">The occurrences used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> TryCreateAsync(JobBatch batch, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the try create with children operation.
    /// </summary>
    /// <param name="batch">The batch used by the operation.</param>
    /// <param name="childOccurrences">The child occurrences used by the operation.</param>
    /// <param name="memberships">The memberships used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> TryCreateWithChildrenAsync(
        JobBatch batch,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobBatch> GetAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatch>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list occurrences operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatchOccurrence>> ListOccurrencesAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the attach operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="occurrences">The occurrences used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AttachAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the try attach children operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="childOccurrences">The child occurrences used by the operation.</param>
    /// <param name="memberships">The memberships used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> TryAttachChildrenAsync(
        Guid batchId,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the replace occurrences operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="occurrences">The occurrences used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReplaceOccurrencesAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="batch">The batch used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(JobBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes occurrences.
    /// </summary>
    /// <param name="occurrenceIds">The occurrence ids used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> RemoveOccurrencesAsync(IReadOnlyCollection<Guid> occurrenceIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists lease properties.
/// </summary>
public interface IJobLeaseStore
{
    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobLeaseRecord> GetAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobLeaseRecord>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the try acquire operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="schedulerInstanceId">The scheduler instance id used by the operation.</param>
    /// <param name="duration">The duration used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobLeaseRecord> TryAcquireAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the renew operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="schedulerInstanceId">The scheduler instance id used by the operation.</param>
    /// <param name="ownershipToken">The ownership token used by the operation.</param>
    /// <param name="duration">The duration used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobLeaseRecord> RenewAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the verify ownership operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="schedulerInstanceId">The scheduler instance id used by the operation.</param>
    /// <param name="ownershipToken">The ownership token used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> VerifyOwnershipAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the release operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="schedulerInstanceId">The scheduler instance id used by the operation.</param>
    /// <param name="ownershipToken">The ownership token used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> ReleaseAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list expired operation.
    /// </summary>
    /// <param name="asOfUtc">The as of utc used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobLeaseRecord>> ListExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the upsert operation.
    /// </summary>
    /// <param name="lease">The lease used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpsertAsync(JobLeaseRecord lease, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes .
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists append-oriented execution history.
/// </summary>
public interface IJobExecutionHistoryStore
{
    /// <summary>
    /// Executes the append operation.
    /// </summary>
    /// <param name="entry">The entry used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AppendAsync(JobExecutionHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobExecutionHistoryEntry>> ListAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the purge operation.
    /// </summary>
    /// <param name="olderThanUtc">The older than utc used by the operation.</param>
    /// <param name="historyIds">The history ids used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists append-oriented batch history.
/// </summary>
public interface IJobBatchHistoryStore
{
    /// <summary>
    /// Executes the append operation.
    /// </summary>
    /// <param name="entry">The entry used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AppendAsync(JobBatchHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list operation.
    /// </summary>
    /// <param name="batchId">The batch id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatchHistoryEntry>> ListAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the purge operation.
    /// </summary>
    /// <param name="olderThanUtc">The older than utc used by the operation.</param>
    /// <param name="historyIds">The history ids used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists durably accepted event-trigger input records.
/// </summary>
public interface IJobAcceptedEventStore
{
    /// <summary>
    /// Executes the try accept operation.
    /// </summary>
    /// <param name="acceptedEvent">The accepted event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> TryAcceptAsync(JobAcceptedEvent acceptedEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list pending operation.
    /// </summary>
    /// <param name="source">The source sequence.</param>
    /// <param name="eventDataType">The event data type used by the operation.</param>
    /// <param name="afterAcceptedUtc">The after accepted utc used by the operation.</param>
    /// <param name="afterAcceptedEventId">The after accepted event id used by the operation.</param>
    /// <param name="take">The take used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
    /// <summary>
    /// Gets previous execution.
    /// </summary>
    /// <param name="occurrenceId">The occurrence id used by the operation.</param>
    /// <param name="currentExecutionId">The current execution id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobExecution> GetPreviousExecutionAsync(Guid occurrenceId, Guid currentExecutionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets previous successful execution.
    /// </summary>
    /// <param name="jobName">The job name used by the operation.</param>
    /// <param name="triggerName">The trigger name used by the operation.</param>
    /// <param name="beforeUtc">The before utc used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<JobExecution> GetPreviousSuccessfulExecutionAsync(string jobName, string triggerName, DateTimeOffset beforeUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exposes provider-neutral query access to persisted Jobs runtime records.
/// </summary>
public interface IJobSchedulerQueryStore
{
    /// <summary>
    /// Executes the list occurrences operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobOccurrence>> ListOccurrencesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list executions operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobExecution>> ListExecutionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list execution history operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobExecutionHistoryEntry>> ListExecutionHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list dependencies operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobOccurrenceDependency>> ListDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list batches operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatch>> ListBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list batch history operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatchHistoryEntry>> ListBatchHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list batch occurrences operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobBatchOccurrence>> ListBatchOccurrencesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the list leases operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<JobLeaseRecord>> ListLeasesAsync(CancellationToken cancellationToken = default);
}
