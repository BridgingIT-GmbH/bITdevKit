// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a persisted scheduled unit of work created by trigger materialization or dispatch.
/// </summary>
public sealed record JobOccurrence
{
    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets the deterministic occurrence key.
    /// </summary>
    public string OccurrenceKey { get; init; }

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets the stable trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets the originating trigger type.
    /// </summary>
    public JobTriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets the persisted occurrence status.
    /// </summary>
    public JobOccurrenceStatus Status { get; init; } = JobOccurrenceStatus.Pending;

    /// <summary>
    /// Gets the due UTC instant.
    /// </summary>
    public DateTimeOffset DueUtc { get; init; }

    /// <summary>
    /// Gets the schedule-derived UTC instant when applicable.
    /// </summary>
    public DateTimeOffset? ScheduledUtc { get; init; }

    /// <summary>
    /// Gets the payload data.
    /// </summary>
    public object Data { get; init; }

    /// <summary>
    /// Gets the payload data type.
    /// </summary>
    public Type DataType { get; init; } = typeof(Unit);

    /// <summary>
    /// Gets the persisted properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets the causation identifier.
    /// </summary>
    public string CausationId { get; init; }

    /// <summary>
    /// Gets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the status to restore when a paused occurrence resumes.
    /// </summary>
    public JobOccurrenceStatus? ResumeStatus { get; init; }

    /// <summary>
    /// Gets the current blocked reason when the occurrence is dependency-blocked.
    /// </summary>
    public string BlockedReason { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}