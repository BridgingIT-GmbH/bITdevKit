// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the accepted result of a batch create, dispatch, or attach operation.
/// </summary>
public class JobBatchDispatchResult
{
    /// <summary>
    /// Gets or sets the batch identifier.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the current batch status.
    /// </summary>
    public JobBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the accepted child count.
    /// </summary>
    public int AcceptedCount { get; set; }

    /// <summary>
    /// Gets or sets the accepted child occurrence identifiers.
    /// </summary>
    public IReadOnlyList<Guid> OccurrenceIds { get; set; } = [];
}