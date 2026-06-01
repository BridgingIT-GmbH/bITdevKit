// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a persisted batch grouping for child occurrences.
/// </summary>
public sealed record JobBatch
{
    /// <summary>
    /// Gets the batch identifier.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets the public batch identifier.
    /// </summary>
    public string ExternalBatchId { get; init; }

    /// <summary>
    /// Gets the display description.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the batch roll-up status.
    /// </summary>
    public JobBatchStatus Status { get; init; } = JobBatchStatus.Created;

    /// <summary>
    /// Gets the completion policy.
    /// </summary>
    public JobBatchCompletionPolicy CompletionPolicy { get; init; } = JobBatchCompletionPolicy.RequireAllSucceeded;

    /// <summary>
    /// Gets safe properties.
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
    /// Gets the accepted child count.
    /// </summary>
    public int AcceptedCount { get; init; }

    /// <summary>
    /// Gets the terminal completed child count.
    /// </summary>
    public int SucceededCount { get; init; }

    /// <summary>
    /// Gets the failed child count.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets the cancelled child count.
    /// </summary>
    public int CancelledCount { get; init; }

    /// <summary>
    /// Gets the archived child count.
    /// </summary>
    public int ArchivedCount { get; init; }

    /// <summary>
    /// Gets the cancellation acceptance timestamp when batch cancellation has been requested.
    /// </summary>
    public DateTimeOffset? CancellationRequestedDate { get; init; }

    /// <summary>
    /// Gets the archived timestamp when the batch is retained as archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; init; }

    /// <summary>
    /// Gets the roll-up completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedDate { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}