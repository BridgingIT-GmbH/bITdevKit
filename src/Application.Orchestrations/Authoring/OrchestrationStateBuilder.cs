// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds a single orchestration state.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="state">The state definition being configured.</param>
public class OrchestrationStateBuilder<TData>(OrchestrationStateDefinition<TData> state) : IOrchestrationStateBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds a class-based activity to the state.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        state.Activities.Add(CreateActivityDefinition<TActivity>());

        return this;
    }

    /// <summary>
    /// Adds and configures a class-based activity.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <param name="configure">The activity configuration callback.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Activity<TActivity>(Action<IOrchestrationActivityBuilder<TData>> configure)
        where TActivity : class, IOrchestrationActivity<TData>
    {
        ArgumentNullException.ThrowIfNull(configure);

        var definition = CreateActivityDefinition<TActivity>();
        configure(new OrchestrationActivityBuilder<TData>(definition));
        state.Activities.Add(definition);

        return this;
    }

    /// <summary>
    /// Adds an inline activity to the state.
    /// </summary>
    /// <param name="executeAsync">The activity delegate.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        state.Activities.Add(this.CreateInlineActivityDefinition(executeAsync, name));

        return this;
    }

    /// <summary>
    /// Adds and configures an inline activity.
    /// </summary>
    /// <param name="executeAsync">The activity delegate.</param>
    /// <param name="configure">The activity configuration callback.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        Action<IOrchestrationActivityBuilder<TData>> configure,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(configure);

        var definition = this.CreateInlineActivityDefinition(executeAsync, name);
        configure(new OrchestrationActivityBuilder<TData>(definition));
        state.Activities.Add(definition);

        return this;
    }

    /// <summary>
    /// Adds a signal-driven behavior for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder instance.</returns>
    public IOrchestrationSignalBuilder<TData> WhenSignal(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var definition = new OrchestrationSignalDefinition<TData>(signalName);
        state.SignalHandlers.Add(definition);

        return new OrchestrationSignalBuilder<TData>(definition);
    }

    /// <summary>
    /// Adds and configures a signal-driven behavior for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> WhenSignal(string signalName, Action<IOrchestrationSignalBuilder<TData>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(this.WhenSignal(signalName));
        return this;
    }

    /// <summary>
    /// Adds a typed signal-driven behavior for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder instance.</returns>
    public IOrchestrationSignalBuilder<TData, TPayload> WhenSignal<TPayload>(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var definition = new OrchestrationSignalDefinition<TData>(signalName, typeof(TPayload));
        state.SignalHandlers.Add(definition);

        return new OrchestrationSignalBuilder<TData, TPayload>(definition);
    }

    /// <summary>
    /// Adds and configures a typed signal-driven behavior for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> WhenSignal<TPayload>(string signalName, Action<IOrchestrationSignalBuilder<TData, TPayload>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(this.WhenSignal<TPayload>(signalName));
        return this;
    }

    /// <summary>
    /// Adds a declarative signal wait for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder instance.</returns>
    public IOrchestrationSignalBuilder<TData> WaitForSignal(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var definition = new OrchestrationSignalDefinition<TData>(signalName);
        state.WaitingSignals.Add(definition);

        return new OrchestrationSignalBuilder<TData>(definition);
    }

    /// <summary>
    /// Adds and configures a declarative signal wait for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> WaitForSignal(string signalName, Action<IOrchestrationSignalBuilder<TData>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(this.WaitForSignal(signalName));
        return this;
    }

    /// <summary>
    /// Adds a typed declarative signal wait for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder instance.</returns>
    public IOrchestrationSignalBuilder<TData, TPayload> WaitForSignal<TPayload>(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var definition = new OrchestrationSignalDefinition<TData>(signalName, typeof(TPayload));
        state.WaitingSignals.Add(definition);

        return new OrchestrationSignalBuilder<TData, TPayload>(definition);
    }

    /// <summary>
    /// Adds and configures a typed declarative signal wait for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> WaitForSignal<TPayload>(string signalName, Action<IOrchestrationSignalBuilder<TData, TPayload>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(this.WaitForSignal<TPayload>(signalName));
        return this;
    }

    /// <summary>
    /// Adds a durable timeout for the current state.
    /// </summary>
    /// <param name="delay">The timeout delay.</param>
    /// <returns>The timer builder instance.</returns>
    public IOrchestrationTimerBuilder<TData> TimeoutAfter(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Timeout delay must be greater than zero.");
        }

        var definition = new OrchestrationTimerDefinition<TData>(delay);
        state.Timers.Add(definition);

        return new OrchestrationTimerBuilder<TData>(definition);
    }

    /// <summary>
    /// Adds a transition to another state.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <param name="condition">The optional transition condition.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> TransitionTo(string targetState, Func<OrchestrationContext<TData>, bool> condition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);

        state.Transitions.Add(new OrchestrationTransitionDefinition<TData>(targetState, condition ?? (_ => true)));

        return this;
    }

    /// <summary>
    /// Marks the state as a completion state.
    /// </summary>
    /// <param name="reason">The optional completion reason.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Complete(string reason = null)
    {
        state.SetDirective(OrchestrationTerminalDirectiveKind.Complete, reason);
        return this;
    }

    /// <summary>
    /// Marks the state as a cancellation state.
    /// </summary>
    /// <param name="reason">The optional cancellation reason.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Cancel(string reason = null)
    {
        state.SetDirective(OrchestrationTerminalDirectiveKind.Cancel, reason);
        return this;
    }

    /// <summary>
    /// Marks the state as a termination state.
    /// </summary>
    /// <param name="reason">The optional termination reason.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Terminate(string reason = null)
    {
        state.SetDirective(OrchestrationTerminalDirectiveKind.Terminate, reason);
        return this;
    }

    /// <summary>
    /// Marks the state as a waiting state.
    /// </summary>
    /// <param name="reason">The optional waiting reason.</param>
    /// <returns>The current state builder instance.</returns>
    public IOrchestrationStateBuilder<TData> Wait(string reason = null)
    {
        state.SetDirective(OrchestrationTerminalDirectiveKind.Wait, reason);
        return this;
    }

    private static OrchestrationActivityDefinition<TData> CreateActivityDefinition<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        return new OrchestrationActivityDefinition<TData>(
            typeof(TActivity).PrettyName(false),
            serviceProvider => ActivatorUtilities.CreateInstance<TActivity>(serviceProvider));
    }

    private OrchestrationActivityDefinition<TData> CreateInlineActivityDefinition(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name)
    {
        return new OrchestrationActivityDefinition<TData>(
            string.IsNullOrWhiteSpace(name) ? $"InlineActivity{state.Activities.Count + 1}" : name,
            _ => new InlineOrchestrationActivity<TData>(executeAsync));
    }
}
