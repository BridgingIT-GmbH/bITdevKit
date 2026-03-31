// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a point-in-time view of a background pipeline execution.
/// </summary>
public class PipelineExecutionSnapshot
{
    /// <summary>
    /// Gets the background execution identifier.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string PipelineName { get; init; }

    /// <summary>
    /// Gets the current execution status.
    /// </summary>
    public PipelineExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the current step name when the pipeline is running.
    /// </summary>
    public string CurrentStepName { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the execution started.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the execution completed.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>
    /// Gets the latest carried result known to the tracker.
    /// </summary>
    public Result Result { get; init; } = Result.Success();
}
