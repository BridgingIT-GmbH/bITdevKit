// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one append-oriented persisted lifecycle record.
/// </summary>
public sealed record JobExecutionHistoryEntry
{
    /// <summary>
    /// Gets the history record identifier.
    /// </summary>
    public Guid HistoryId { get; init; }

    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets the execution identifier when the record is execution-scoped.
    /// </summary>
    public Guid? ExecutionId { get; init; }

    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets the stable trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets the scheduler instance id that recorded the entry.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets the lifecycle event name.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets the occurrence status when applicable.
    /// </summary>
    public JobOccurrenceStatus? OccurrenceStatus { get; init; }

    /// <summary>
    /// Gets the execution status when applicable.
    /// </summary>
    public JobExecutionStatus? ExecutionStatus { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets the recorded UTC timestamp.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the user identity that recorded the entry when available.
    /// </summary>
    public string RecordedBy { get; init; }

    /// <summary>
    /// Gets entry properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();
}