// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the outcome of a bulk operation over occurrences or a batch.
/// </summary>
public sealed class JobBulkOperationResult
{
    /// <summary>
    /// Gets or sets the requested child count.
    /// </summary>
    public int RequestedCount { get; set; }

    /// <summary>
    /// Gets or sets the succeeded child count.
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// Gets or sets the failed child count.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets per-child failures.
    /// </summary>
    public IReadOnlyList<JobBulkOperationFailureModel> Failures { get; set; } = [];
}