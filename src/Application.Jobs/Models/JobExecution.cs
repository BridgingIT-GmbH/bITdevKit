// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one persisted execution attempt for an occurrence.
/// </summary>
public sealed record JobExecution
{
    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets the stable trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets the attempt number.
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Gets the persisted execution status.
    /// </summary>
    public JobExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the scheduler instance id that created the execution attempt.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// Gets the execution completion time.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// Gets the failure or completion message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}