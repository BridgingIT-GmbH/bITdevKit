// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes a compensation operation including its action, condition, and metadata.
/// </summary>
internal class SagaCompensationDescriptor
{
    /// <summary>
    /// Gets the order in which this compensation was registered (0-based index).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the name/label for this compensation step.
    /// Used for logging, tracing, and diagnostics.
    /// </summary>
    public string StepName { get; init; }

    /// <summary>
    /// Gets the compensation action to execute during rollback.
    /// </summary>
    public Func<CancellationToken, Task> Action { get; init; }

    /// <summary>
    /// Gets the optional condition that determines if this compensation should execute.
    /// If null, the compensation always executes.
    /// If returns false, the compensation is skipped.
    /// </summary>
    public Func<CancellationToken, Task<bool>> Condition { get; init; }

    /// <summary>
    /// Gets the timestamp when this compensation was registered.
    /// </summary>
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when this compensation started executing.
    /// </summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the duration of compensation execution.
    /// </summary>
    public TimeSpan? ExecutionDuration { get; set; }

    /// <summary>
    /// Gets or sets the exception that occurred during compensation execution.
    /// </summary>
    public Exception ExecutionError { get; set; }

    /// <summary>
    /// Gets or sets the execution status of this compensation.
    /// </summary>
    public SagaCompensationStatus Status { get; set; } = SagaCompensationStatus.Pending;
}

/// <summary>
/// Represents the execution status of a compensation.
/// </summary>
public enum SagaCompensationStatus
{
    /// <summary>
    /// Compensation is registered but not yet executed.
    /// </summary>
    Pending,

    /// <summary>
    /// Compensation is currently executing.
    /// </summary>
    Executing,

    /// <summary>
    /// Compensation completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Compensation execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Compensation was skipped because its condition was not met.
    /// </summary>
    Skipped
}
