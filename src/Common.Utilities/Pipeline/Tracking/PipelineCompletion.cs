// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the completion payload delivered to background pipeline completion callbacks.
/// </summary>
public class PipelineCompletion
{
    /// <summary>
    /// Gets the background execution identifier.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the final execution status.
    /// </summary>
    public PipelineExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the final accumulated result.
    /// </summary>
    public Result Result { get; init; } = Result.Success();
}
