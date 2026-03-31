// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the handle returned when a pipeline is started in the background.
/// </summary>
public class PipelineExecutionHandle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineExecutionHandle"/> class.
    /// </summary>
    /// <param name="executionId">The background execution identifier.</param>
    public PipelineExecutionHandle(Guid executionId)
    {
        this.ExecutionId = executionId;
    }

    /// <summary>
    /// Gets the background execution identifier.
    /// </summary>
    public Guid ExecutionId { get; }
}
