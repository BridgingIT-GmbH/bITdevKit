// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the lifecycle status of a materialized job occurrence.
/// </summary>
public enum JobOccurrenceStatus
{
    /// <summary>
    /// The occurrence was created but not yet promoted to active scheduling.
    /// </summary>
    Materialized = 0, // TODO: rename to Created or Pending

    /// <summary>
    /// The occurrence is scheduled for future execution.
    /// </summary>
    Scheduled = 1,

    /// <summary>
    /// The occurrence is eligible to be leased and executed.
    /// </summary>
    Due = 2,

    /// <summary>
    /// The occurrence is waiting for prerequisites or an operational unblock.
    /// </summary>
    Blocked = 3,

    /// <summary>
    /// The occurrence is currently executing.
    /// </summary>
    Running = 4,

    /// <summary>
    /// The occurrence is waiting for a retry attempt.
    /// </summary>
    RetryScheduled = 5,

    /// <summary>
    /// The occurrence completed successfully.
    /// </summary>
    Completed = 6,

    /// <summary>
    /// The occurrence reached a failed terminal state.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// The occurrence was cancelled before successful completion.
    /// </summary>
    Cancelled = 8,

    /// <summary>
    /// The occurrence is paused by an operator or runtime policy.
    /// </summary>
    Paused = 9,

    /// <summary>
    /// The occurrence is retained only for history or support purposes.
    /// </summary>
    Archived = 10,
}