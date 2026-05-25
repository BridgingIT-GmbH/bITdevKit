// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Describes the condition used by dispatch-and-wait orchestration execution.
/// </summary>
public class OrchestrationWaitFor
{
    /// <summary>
    /// Gets or sets a value indicating whether waiting should end when the orchestration reaches a terminal state.
    /// </summary>
    public bool Completion { get; set; }

    /// <summary>
    /// Gets or sets the orchestration states that satisfy the wait condition.
    /// </summary>
    public IReadOnlyList<string> States { get; set; }

    /// <summary>
    /// Gets or sets the orchestration outcomes that satisfy the wait condition.
    /// </summary>
    public IReadOnlyList<string> Outcomes { get; set; }
}

/// <summary>
/// Provides helpers for creating <see cref="OrchestrationWaitFor"/> values.
/// </summary>
public static class WaitFor
{
    /// <summary>
    /// Creates a wait condition that completes when the orchestration reaches a terminal state.
    /// </summary>
    /// <returns>The wait condition.</returns>
    public static OrchestrationWaitFor Completion()
    {
        return new OrchestrationWaitFor { Completion = true };
    }

    /// <summary>
    /// Creates a wait condition that completes when the orchestration reaches any of the specified states.
    /// </summary>
    /// <param name="states">The states to match.</param>
    /// <returns>The wait condition.</returns>
    public static OrchestrationWaitFor State(params string[] states)
    {
        return new OrchestrationWaitFor { States = states ?? [] };
    }

    /// <summary>
    /// Creates a wait condition that completes when the orchestration produces any of the specified outcomes.
    /// </summary>
    /// <param name="outcomes">The outcomes to match.</param>
    /// <returns>The wait condition.</returns>
    public static OrchestrationWaitFor Outcome(params string[] outcomes)
    {
        return new OrchestrationWaitFor { Outcomes = outcomes ?? [] };
    }
}

/// <summary>
/// Represents the result of inline orchestration execution.
/// </summary>
public class OrchestrationExecuteResult
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the current orchestration status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the current orchestration state.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the effective orchestration outcome.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets the orchestration correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the serialized orchestration context.
    /// </summary>
    public string ContextJson { get; set; }
}

/// <summary>
/// Represents the result returned by dispatch-and-wait operations.
/// </summary>
public class OrchestrationWaitResult
{
    /// <summary>
    /// Gets or sets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the current orchestration status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the current orchestration state.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the effective orchestration outcome.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the wait operation timed out.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets the completion timestamp when the orchestration has completed.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; set; }
}
