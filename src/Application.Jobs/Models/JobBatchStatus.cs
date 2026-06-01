// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the persisted roll-up status of a job batch.
/// </summary>
public enum JobBatchStatus
{
    /// <summary>
    /// The batch was created but child work has not started.
    /// </summary>
    Created = 0,

    /// <summary>
    /// At least one child occurrence is active or pending within the batch.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// All child occurrences completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The batch completed but one or more child occurrences failed.
    /// </summary>
    CompletedWithFailures = 3,

    /// <summary>
    /// The batch ended in a failed terminal state.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The batch was cancelled.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// The batch is retained only for history or support purposes.
    /// </summary>
    Archived = 6,
}