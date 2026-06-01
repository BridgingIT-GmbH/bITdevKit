// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one persisted batch membership link.
/// </summary>
public sealed record JobBatchOccurrence
{
    /// <summary>
    /// Gets the batch identifier.
    /// </summary>
    public Guid BatchId { get; init; }

    /// <summary>
    /// Gets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; init; }

    /// <summary>
    /// Gets the projected child occurrence status.
    /// </summary>
    public JobOccurrenceStatus ChildStatus { get; init; }

    /// <summary>
    /// Gets the optional ordering sequence.
    /// </summary>
    public int? Sequence { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}