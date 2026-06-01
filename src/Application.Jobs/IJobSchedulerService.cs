// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the core runtime dispatch and control surface for jobs.
/// </summary>
/// <example>
/// <code>
/// var result = await scheduler.DispatchAndWaitAsync&lt;CleanupJob&gt;();
/// if (result.IsSuccess)
/// {
///     Console.WriteLine(result.Value.Status);
/// }
/// </code>
/// </example>
public interface IJobSchedulerService
{
    /// <summary>
    /// Dispatches the specified job type using its configured manual trigger.
    /// </summary>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <param name="data">The optional dispatch payload.</param>
    /// <param name="options">The optional dispatch options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted dispatch result.</returns>
    Task<Result<JobDispatchResult>> DispatchAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default)
        where TJob : class, IJob;

    /// <summary>
    /// Dispatches the specified job name using its configured manual trigger.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="data">The optional dispatch payload.</param>
    /// <param name="options">The optional dispatch options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted dispatch result.</returns>
    Task<Result<JobDispatchResult>> DispatchAsync(
        string jobName,
        object data = null,
        JobDispatchOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches the specified job type and waits until the inline execution reaches a terminal state.
    /// </summary>
    /// <typeparam name="TJob">The job type.</typeparam>
    /// <param name="data">The optional dispatch payload.</param>
    /// <param name="options">The optional dispatch options.</param>
    /// <param name="timeout">The optional caller wait timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal execution result.</returns>
    Task<Result<JobExecutionResult>> DispatchAndWaitAsync<TJob>(
        object data = null,
        JobDispatchOptions options = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TJob : class, IJob;

    /// <summary>
    /// Dispatches the specified job name and waits until the inline execution reaches a terminal state.
    /// </summary>
    /// <param name="jobName">The stable job name.</param>
    /// <param name="data">The optional dispatch payload.</param>
    /// <param name="options">The optional dispatch options.</param>
    /// <param name="timeout">The optional caller wait timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal execution result.</returns>
    Task<Result<JobExecutionResult>> DispatchAndWaitAsync(
        string jobName,
        object data = null,
        JobDispatchOptions options = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of an occurrence.
    /// </summary>
    Task<Result> CancelOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests interruption of a running occurrence.
    /// </summary>
    Task<Result> InterruptOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables a registered job through durable runtime state.
    /// </summary>
    Task<Result> EnableJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables a registered job through durable runtime state.
    /// </summary>
    Task<Result> DisableJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a registered job without mutating its code-first definition.
    /// </summary>
    Task<Result> PauseJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a previously paused registered job.
    /// </summary>
    Task<Result> ResumeJobAsync(
        string jobName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a registered trigger without mutating its code-first definition.
    /// </summary>
    Task<Result> PauseTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a previously paused registered trigger.
    /// </summary>
    Task<Result> ResumeTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables a registered trigger through durable runtime state.
    /// </summary>
    Task<Result> EnableTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables a registered trigger through durable runtime state.
    /// </summary>
    Task<Result> DisableTriggerAsync(
        string jobName,
        string triggerName,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an eligible occurrence before a new attempt starts.
    /// </summary>
    Task<Result> PauseOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a previously paused occurrence.
    /// </summary>
    Task<Result> ResumeOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests retry of an eligible failed occurrence.
    /// </summary>
    Task<Result> RetryOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives an occurrence.
    /// </summary>
    Task<Result> ArchiveOccurrenceAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the persisted lease for an occurrence so it can be recovered.
    /// </summary>
    Task<Result> ReleaseOccurrenceLeaseAsync(
        Guid occurrenceId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries the selected eligible failed occurrences.
    /// </summary>
    Task<Result<JobBulkOperationResult>> RetryOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the selected eligible occurrences.
    /// </summary>
    Task<Result<JobBulkOperationResult>> CancelOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the selected eligible occurrences.
    /// </summary>
    Task<Result<JobBulkOperationResult>> ArchiveOccurrencesAsync(
        IReadOnlyCollection<Guid> occurrenceIds,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an empty or pre-described batch record.
    /// </summary>
    Task<Result<JobBatchDispatchResult>> CreateBatchAsync(
        JobBatchCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a batch and dispatches child occurrences as one accepted operation.
    /// </summary>
    Task<Result<JobBatchDispatchResult>> DispatchBatchAsync(
        JobBatchDispatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches additional child occurrences to an existing batch.
    /// </summary>
    Task<Result<JobBatchDispatchResult>> AttachToBatchAsync(
        string batchId,
        JobBatchDispatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries eligible failed child occurrences for a batch.
    /// </summary>
    Task<Result<JobBulkOperationResult>> RetryBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels eligible child occurrences for a batch.
    /// </summary>
    Task<Result<JobBulkOperationResult>> CancelBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses eligible child occurrences for a batch.
    /// </summary>
    Task<Result> PauseBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes eligible child occurrences for a batch.
    /// </summary>
    Task<Result> ResumeBatchAsync(
        string batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a batch and eligible retained child occurrences.
    /// </summary>
    Task<Result> ArchiveBatchAsync(
        string batchId,
        string reason = null,
        CancellationToken cancellationToken = default);
}