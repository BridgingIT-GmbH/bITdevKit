// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the engine-managed execution metadata attached to a pipeline context.
/// </summary>
public class PipelineExecutionContext
{
    /// <summary>
    /// Gets or sets the logical pipeline name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the pipeline execution.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier associated with the execution.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution started.
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution completed.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the current step name.
    /// </summary>
    public string CurrentStepName { get; set; }

    /// <summary>
    /// Gets or sets the number of executed steps.
    /// </summary>
    public int ExecutedStepCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of steps in the resolved definition.
    /// </summary>
    public int TotalStepCount { get; set; }

    /// <summary>
    /// Gets the execution duration when the pipeline has completed.
    /// </summary>
    public TimeSpan? Duration => this.CompletedUtc is { } completedUtc
        ? completedUtc - this.StartedUtc
        : null;

    /// <summary>
    /// Gets the execution-scoped property bag for unstructured metadata.
    /// </summary>
    public PropertyBag Items { get; } = new();
}
