// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the terminal result of one inline execution attempt flow.
/// </summary>
public sealed class JobExecutionResult
{
    /// <summary>
    /// Gets or sets the stable job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the execution identifier.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public JobExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether execution ended due to timeout.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets the start time in UTC.
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the completion time in UTC.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the execution messages.
    /// </summary>
    public IReadOnlyList<string> Messages { get; set; } = [];
}