// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an event that occurs during compensation execution lifecycle.
/// </summary>
public class SagaCompensationEvent
{
    /// <summary>
    /// Gets the name/label of the compensation step.
    /// </summary>
    public string StepName { get; init; }

    /// <summary>
    /// Gets the type of compensation event.
    /// </summary>
    public CompensationEventType EventType { get; init; }

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the exception that occurred during compensation execution, if any.
    /// Only populated for Failed and Retried event types.
    /// </summary>
    public Exception Error { get; init; }

    /// <summary>
    /// Gets the execution duration for this compensation step.
    /// Only populated for Succeeded and Failed event types.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets the saga context associated with this event.
    /// </summary>
    public SagaContext Context { get; init; }
}

/// <summary>
/// Defines the types of events that can occur during compensation execution.
/// </summary>
public enum CompensationEventType
{
    /// <summary>
    /// Compensation was registered with the saga.
    /// </summary>
    Registered,

    /// <summary>
    /// Compensation execution has started.
    /// </summary>
    Started,

    /// <summary>
    /// Compensation execution completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Compensation execution failed with an exception.
    /// </summary>
    Failed,

    /// <summary>
    /// Compensation was skipped because its condition was not met.
    /// </summary>
    Skipped
}

/// <summary>
/// Represents a handler for compensation events.
/// </summary>
/// <param name="compensationEvent">The compensation event that occurred.</param>
/// <returns>A task representing the asynchronous event handling operation.</returns>
public delegate Task CompensationEventHandler(SagaCompensationEvent compensationEvent);
