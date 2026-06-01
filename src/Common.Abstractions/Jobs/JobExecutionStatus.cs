// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the lifecycle status of a single execution attempt.
/// </summary>
public enum JobExecutionStatus
{
    /// <summary>
    /// The execution attempt has started.
    /// </summary>
    Started = 0,

    /// <summary>
    /// The execution attempt completed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The execution attempt failed.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The execution attempt exceeded its timeout.
    /// </summary>
    TimedOut = 3,

    /// <summary>
    /// The execution attempt was cancelled.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The execution attempt was interrupted before completion.
    /// </summary>
    Interrupted = 5,

    /// <summary>
    /// The execution attempt was superseded by a retry.
    /// </summary>
    Retried = 6,
}