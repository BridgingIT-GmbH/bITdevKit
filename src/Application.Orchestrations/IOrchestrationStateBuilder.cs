// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Provides the fluent authoring surface for a single orchestration state.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationStateBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds a class-based activity to the state.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Adds and configures a class-based activity.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <param name="configure">The activity configuration callback.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Activity<TActivity>(Action<IOrchestrationActivityBuilder<TData>> configure)
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Adds an inline activity to the state.
    /// </summary>
    /// <param name="executeAsync">The inline activity delegate.</param>
    /// <param name="name">An optional activity name.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    /// <summary>
    /// Adds and configures an inline activity.
    /// </summary>
    /// <param name="executeAsync">The inline activity delegate.</param>
    /// <param name="configure">The activity configuration callback.</param>
    /// <param name="name">An optional activity name.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        Action<IOrchestrationActivityBuilder<TData>> configure,
        string name = null);

    /// <summary>
    /// Adds a signal-driven behavior for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder.</returns>
    IOrchestrationSignalBuilder<TData> WhenSignal(string signalName);

    /// <summary>
    /// Adds and configures a signal-driven behavior for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> WhenSignal(string signalName, Action<IOrchestrationSignalBuilder<TData>> configure);

    /// <summary>
    /// Adds a typed signal-driven behavior for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder.</returns>
    IOrchestrationSignalBuilder<TData, TPayload> WhenSignal<TPayload>(string signalName);

    /// <summary>
    /// Adds and configures a typed signal-driven behavior for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> WhenSignal<TPayload>(string signalName, Action<IOrchestrationSignalBuilder<TData, TPayload>> configure);

    /// <summary>
    /// Adds a declarative signal wait for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder.</returns>
    IOrchestrationSignalBuilder<TData> WaitForSignal(string signalName);

    /// <summary>
    /// Adds and configures a declarative signal wait for the current state.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> WaitForSignal(string signalName, Action<IOrchestrationSignalBuilder<TData>> configure);

    /// <summary>
    /// Adds a typed declarative signal wait for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <returns>The signal builder.</returns>
    IOrchestrationSignalBuilder<TData, TPayload> WaitForSignal<TPayload>(string signalName);

    /// <summary>
    /// Adds and configures a typed declarative signal wait for the current state.
    /// </summary>
    /// <typeparam name="TPayload">The signal payload type.</typeparam>
    /// <param name="signalName">The signal name.</param>
    /// <param name="configure">The signal configuration callback.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> WaitForSignal<TPayload>(string signalName, Action<IOrchestrationSignalBuilder<TData, TPayload>> configure);

    /// <summary>
    /// Adds a durable timeout for the current state.
    /// </summary>
    /// <param name="delay">The timeout delay.</param>
    /// <returns>The timer builder.</returns>
    IOrchestrationTimerBuilder<TData> TimeoutAfter(TimeSpan delay);

    /// <summary>
    /// Adds an explicit transition to another state.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <param name="condition">An optional transition condition. When omitted, the transition is unconditional.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> TransitionTo(string targetState, Func<OrchestrationContext<TData>, bool> condition = null);

    /// <summary>
    /// Marks the state as a successful terminal state.
    /// </summary>
    /// <param name="reason">An optional completion reason.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Complete(string reason = null);

    /// <summary>
    /// Marks the state as a cancelled terminal state.
    /// </summary>
    /// <param name="reason">An optional cancellation reason.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Cancel(string reason = null);

    /// <summary>
    /// Marks the state as a terminated terminal state.
    /// </summary>
    /// <param name="reason">An optional termination reason.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Terminate(string reason = null);

    /// <summary>
    /// Marks the state as a waiting state.
    /// </summary>
    /// <param name="reason">An optional waiting reason.</param>
    /// <returns>The current state builder.</returns>
    IOrchestrationStateBuilder<TData> Wait(string reason = null);
}