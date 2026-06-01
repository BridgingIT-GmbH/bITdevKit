// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the execution context supplied to a job run.
/// </summary>
/// <example>
/// <code>
/// public sealed class CleanupJob : IJob
/// {
///     public Task&lt;IResult&gt; ExecuteAsync(
///         IJobExecutionContext context,
///         CancellationToken cancellationToken = default)
///     {
///         context.Messages.Add($"Running {context.JobName}.");
///         return Task.FromResult&lt;IResult&gt;(Result.Success());
///     }
/// }
/// </code>
/// </example>
public interface IJobExecutionContext
{
    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    string JobName { get; }

    /// <summary>
    /// Gets the trigger name that created the current occurrence.
    /// </summary>
    string TriggerName { get; }

    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    Guid OccurrenceId { get; }

    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    Guid ExecutionId { get; }

    /// <summary>
    /// Gets the execution attempt number.
    /// </summary>
    int AttemptNumber { get; }

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the idempotency key.
    /// </summary>
    string IdempotencyKey { get; }

    /// <summary>
    /// Gets the scheduled time when the occurrence is time-based.
    /// </summary>
    DateTimeOffset? ScheduledUtc { get; }

    /// <summary>
    /// Gets the due time of the current occurrence.
    /// </summary>
    DateTimeOffset DueUtc { get; }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    DateTimeOffset StartedUtc { get; }

    /// <summary>
    /// Gets the raw job data object.
    /// </summary>
    object Data { get; }

    /// <summary>
    /// Gets the declared data type for the current job execution.
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// Gets the immutable execution properties.
    /// </summary>
    PropertyBag Properties { get; }

    /// <summary>
    /// Gets the mutable execution messages.
    /// </summary>
    ICollection<string> Messages { get; }

    /// <summary>
    /// Gets the mutable execution items.
    /// </summary>
    IDictionary<string, object> Items { get; }

    /// <summary>
    /// Gets the previous execution attempt for the same occurrence when available.
    /// </summary>
    JobExecutionContextSnapshot PreviousExecution { get; }

    /// <summary>
    /// Gets the most recent successful execution for the same job and trigger when available.
    /// </summary>
    JobExecutionContextSnapshot PreviousSuccessfulExecution { get; }

    /// <summary>
    /// Gets the cancellation token associated with the current execution.
    /// </summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// Represents a typed execution context supplied to a job run.
/// </summary>
/// <typeparam name="TData">The typed data contract.</typeparam>
/// <example>
/// <code>
/// public sealed class ExportCustomersJob : IJob&lt;ExportCustomersRequest&gt;
/// {
///     public Task&lt;IResult&gt; ExecuteAsync(
///         IJobExecutionContext&lt;ExportCustomersRequest&gt; context,
///         CancellationToken cancellationToken = default)
///     {
///         var profile = context.Data.Profile;
///         return Task.FromResult&lt;IResult&gt;(Result.Success($"Exporting {profile}."));
///     }
/// }
/// </code>
/// </example>
public interface IJobExecutionContext<TData> : IJobExecutionContext
{
    /// <summary>
    /// Gets the typed data payload for the current job execution.
    /// </summary>
    new TData Data { get; }
}