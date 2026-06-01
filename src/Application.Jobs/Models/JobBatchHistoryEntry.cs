// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents an append-only audit event for a batch-level operation or roll-up change.
/// </summary>
public sealed record JobBatchHistoryEntry
{
    /// <summary>
    /// Gets the history identifier.
    /// </summary>
    public Guid HistoryId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the internal batch identifier.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets the public batch identifier.
    /// </summary>
    public string ExternalBatchId { get; init; }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets the batch status after the event.
    /// </summary>
    public JobBatchStatus? BatchStatus { get; init; }

    /// <summary>
    /// Gets the human-readable event message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets the scheduler instance that recorded the event.
    /// </summary>
    public string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets safe event properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the event timestamp.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; }
}
