// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Handles unhandled scheduler exceptions in a centralized way.
/// </summary>
public interface IJobSchedulerExceptionHandler
{
    /// <summary>
    /// Handles an unhandled scheduler exception.
    /// </summary>
    Task HandleAsync(JobSchedulerExceptionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes an unhandled scheduler exception.
/// </summary>
public sealed record JobSchedulerExceptionContext
{
    /// <summary>
    /// Gets the scheduler instance identifier.
    /// </summary>
    public required string SchedulerInstanceId { get; init; }

    /// <summary>
    /// Gets the origin of the exception.
    /// </summary>
    public JobSchedulerExceptionSource Source { get; init; }

    /// <summary>
    /// Gets the unhandled exception.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets the resolved job definition when the exception came from a job execution.
    /// </summary>
    public JobDefinition Definition { get; init; }

    /// <summary>
    /// Gets the resolved trigger definition when the exception came from a job execution.
    /// </summary>
    public JobTriggerDefinition Trigger { get; init; }

    /// <summary>
    /// Gets the occurrence identifier when available.
    /// </summary>
    public Guid? OccurrenceId { get; init; }

    /// <summary>
    /// Gets the execution identifier when available.
    /// </summary>
    public Guid? ExecutionId { get; init; }
}

/// <summary>
/// Identifies the scheduler component that raised an unhandled exception.
/// </summary>
public enum JobSchedulerExceptionSource
{
    /// <summary>
    /// The exception happened during a job execution attempt.
    /// </summary>
    Execution = 0,

    /// <summary>
    /// The exception happened in the hosted background scheduler loop.
    /// </summary>
    BackgroundService = 1,
}