// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents a configured orchestration state.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="name">The state name.</param>
public class OrchestrationStateDefinition<TData>(string name)
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Gets the state name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the activities that execute within the state.
    /// </summary>
    public List<OrchestrationActivityDefinition<TData>> Activities { get; } = [];

    /// <summary>
    /// Gets the outgoing transitions for the state.
    /// </summary>
    public List<OrchestrationTransitionDefinition<TData>> Transitions { get; } = [];

    /// <summary>
    /// Gets the signal-driven behaviors configured for the state.
    /// </summary>
    public List<OrchestrationSignalDefinition<TData>> SignalHandlers { get; } = [];

    /// <summary>
    /// Gets the signal waits configured for the state.
    /// </summary>
    public List<OrchestrationSignalDefinition<TData>> WaitingSignals { get; } = [];

    /// <summary>
    /// Gets the durable timers configured for the state.
    /// </summary>
    public List<OrchestrationTimerDefinition<TData>> Timers { get; } = [];

    /// <summary>
    /// Gets the configured terminal directive kind.
    /// </summary>
    public OrchestrationTerminalDirectiveKind TerminalDirectiveKind { get; private set; }

    /// <summary>
    /// Gets the optional terminal directive reason.
    /// </summary>
    public string TerminalDirectiveReason { get; private set; }

    /// <summary>
    /// Sets the terminal directive for the state.
    /// </summary>
    /// <param name="kind">The terminal directive kind.</param>
    /// <param name="reason">The optional human-readable reason.</param>
    public void SetDirective(OrchestrationTerminalDirectiveKind kind, string reason)
    {
        if (this.TerminalDirectiveKind != OrchestrationTerminalDirectiveKind.None)
        {
            throw new InvalidOperationException($"State '{this.Name}' already defines terminal behavior.");
        }

        this.TerminalDirectiveKind = kind;
        this.TerminalDirectiveReason = reason;
    }
}
