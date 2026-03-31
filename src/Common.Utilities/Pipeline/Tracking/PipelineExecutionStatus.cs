// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the lifecycle status of a background pipeline execution.
/// </summary>
public enum PipelineExecutionStatus
{
    /// <summary>
    /// The execution has been accepted but has not started yet.
    /// </summary>
    Accepted,

    /// <summary>
    /// The execution is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// The execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The execution completed with a failed result.
    /// </summary>
    Failed,

    /// <summary>
    /// The execution was cancelled.
    /// </summary>
    Cancelled
}
