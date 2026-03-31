// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents mutable runtime state for an executing pipeline.
/// </summary>
/// <param name="executionId">The execution identifier.</param>
/// <param name="result">The initial carried result.</param>
public class PipelineRunState(Guid executionId, Result result)
{
    /// <summary>
    /// Gets the execution identifier.
    /// </summary>
    public Guid ExecutionId { get; } = executionId;

    /// <summary>
    /// Gets or sets the latest carried result.
    /// </summary>
    public Result Result { get; set; } = result;

    /// <summary>
    /// Gets or sets the current execution status.
    /// </summary>
    public PipelineExecutionStatus Status { get; set; } = PipelineExecutionStatus.Completed;
}

internal readonly record struct PipelineRunResult(Result Result, PipelineExecutionStatus Status);
