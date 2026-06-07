// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Application.Orchestrations;
/// <summary>
/// Stores orchestration types registered for dependency injection.
/// </summary>
public class OrchestrationRegistrationStore
{
    private readonly HashSet<Type> orchestrationTypes = [];
    private readonly Dictionary<string, Type> orchestrationNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered orchestration types.
    /// </summary>
    /// <returns>The registered orchestration types.</returns>
    public IReadOnlyList<Type> GetRegisteredTypes()
    {
        return this.orchestrationTypes.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Adds an orchestration type to the registration store.
    /// </summary>
    /// <param name="orchestrationType">The orchestration type to add.</param>
    public void Add(Type orchestrationType)
    {
        ArgumentNullException.ThrowIfNull(orchestrationType);
        this.orchestrationTypes.Add(orchestrationType);
        this.orchestrationNames.TryAdd(orchestrationType.PrettyName(false), orchestrationType);
    }

    /// <summary>
    /// Removes an orchestration type from the registration store.
    /// </summary>
    /// <param name="orchestrationType">The orchestration type to remove.</param>
    public void Remove(Type orchestrationType)
    {
        ArgumentNullException.ThrowIfNull(orchestrationType);

        this.orchestrationTypes.Remove(orchestrationType);
        foreach (var name in this.orchestrationNames
            .Where(item => item.Value == orchestrationType)
            .Select(item => item.Key)
            .ToArray())
        {
            this.orchestrationNames.Remove(name);
        }
    }

    /// <summary>
    /// Determines whether the specified orchestration type has been registered.
    /// </summary>
    /// <param name="orchestrationType">The orchestration type to check.</param>
    /// <returns><c>true</c> when the type exists in the store; otherwise, <c>false</c>.</returns>
    public bool Contains(Type orchestrationType)
    {
        ArgumentNullException.ThrowIfNull(orchestrationType);
        return this.orchestrationTypes.Contains(orchestrationType);
    }

    /// <summary>
    /// Associates a resolved orchestration definition name with a registered orchestration type.
    /// </summary>
    /// <param name="orchestrationName">The runtime orchestration name.</param>
    /// <param name="orchestrationType">The orchestration type.</param>
    public void RegisterName(string orchestrationName, Type orchestrationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationName);
        ArgumentNullException.ThrowIfNull(orchestrationType);

        this.orchestrationNames[orchestrationName] = orchestrationType;
    }

    /// <summary>
    /// Tries to resolve a registered orchestration type by its runtime name.
    /// </summary>
    /// <param name="orchestrationName">The runtime orchestration name.</param>
    /// <param name="orchestrationType">The resolved orchestration type when found.</param>
    /// <returns><c>true</c> when the orchestration type was resolved; otherwise <c>false</c>.</returns>
    public bool TryGetByName(string orchestrationName, out Type orchestrationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationName);
        return this.orchestrationNames.TryGetValue(orchestrationName, out orchestrationType);
    }

    /// <summary>
    /// Gets the orchestration data type associated with a registered orchestration.
    /// </summary>
    /// <param name="orchestrationType">The orchestration type.</param>
    /// <returns>The orchestration data type.</returns>
    public Type GetDataType(Type orchestrationType)
    {
        ArgumentNullException.ThrowIfNull(orchestrationType);

        var current = orchestrationType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Orchestration<>))
            {
                return current.GetGenericArguments()[0];
            }

            current = current.BaseType;
        }

        throw new InvalidOperationException($"Type '{orchestrationType.FullName}' does not inherit from Orchestration<TData>.");
    }
}

/// <summary>
/// Builds an orchestration definition from code-first state declarations.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationDefinitionBuilder<TData> : IOrchestrationBuilder<TData>
    where TData : class, IOrchestrationData
{
    private readonly string orchestrationName;
    private readonly List<OrchestrationStateDefinition<TData>> states = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationDefinitionBuilder{TData}"/> class.
    /// </summary>
    /// <param name="name">The orchestration definition name.</param>
    public OrchestrationDefinitionBuilder(string name)
    {
        this.orchestrationName = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Adds a state to the orchestration definition.
    /// </summary>
    /// <param name="name">The unique state name.</param>
    /// <param name="configure">The state configuration callback.</param>
    /// <returns>The current builder instance.</returns>
    public IOrchestrationBuilder<TData> State(string name, Action<IOrchestrationStateBuilder<TData>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (this.states.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Orchestration '{this.orchestrationName}' already defines a state named '{name}'.");
        }

        var state = new OrchestrationStateDefinition<TData>(name);
        configure(new OrchestrationStateBuilder<TData>(state));
        this.states.Add(state);

        return this;
    }

    /// <summary>
    /// Validates and creates the immutable orchestration definition.
    /// </summary>
    /// <returns>The built orchestration definition.</returns>
    public OrchestrationDefinition<TData> Build()
    {
        if (this.states.Count == 0)
        {
            throw new InvalidOperationException($"Orchestration '{this.orchestrationName}' must define at least one state.");
        }

        var stateNames = new HashSet<string>(this.states.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var state in this.states)
        {
            if (state.TerminalDirectiveKind != OrchestrationTerminalDirectiveKind.None && state.Transitions.Count > 0)
            {
                throw new InvalidOperationException($"State '{state.Name}' in orchestration '{this.orchestrationName}' cannot define both terminal behavior and outgoing transitions.");
            }

            if (state.Activities.Count == 0 &&
                state.Transitions.Count == 0 &&
                state.SignalHandlers.Count == 0 &&
                state.WaitingSignals.Count == 0 &&
                state.Timers.Count == 0 &&
                state.TerminalDirectiveKind == OrchestrationTerminalDirectiveKind.None)
            {
                throw new InvalidOperationException($"State '{state.Name}' in orchestration '{this.orchestrationName}' has no activities, transitions, or terminal behavior.");
            }

            foreach (var transition in state.Transitions)
            {
                if (!stateNames.Contains(transition.TargetState))
                {
                    throw new InvalidOperationException($"State '{state.Name}' in orchestration '{this.orchestrationName}' targets unknown state '{transition.TargetState}'.");
                }
            }

            foreach (var signal in state.SignalHandlers.Concat(state.WaitingSignals).Where(signal => !string.IsNullOrWhiteSpace(signal.TargetState)))
            {
                if (!stateNames.Contains(signal.TargetState))
                {
                    throw new InvalidOperationException($"State '{state.Name}' in orchestration '{this.orchestrationName}' defines a signal target state '{signal.TargetState}' that does not exist.");
                }
            }

            foreach (var timer in state.Timers.Where(timer => !string.IsNullOrWhiteSpace(timer.TargetState)))
            {
                if (!stateNames.Contains(timer.TargetState))
                {
                    throw new InvalidOperationException($"State '{state.Name}' in orchestration '{this.orchestrationName}' defines a timer target state '{timer.TargetState}' that does not exist.");
                }
            }
        }

        return new OrchestrationDefinition<TData>(this.orchestrationName, this.states[0].Name, this.states.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Describes an activity configured for a state.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="Name">The configured activity name.</param>
/// <param name="Factory">The factory used to create the activity instance.</param>
public record OrchestrationActivityDefinition<TData>(
    string Name,
    Func<IServiceProvider, IOrchestrationActivity<TData>> Factory)
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Gets or sets the optional retry policy applied to the activity.
    /// </summary>
    public OrchestrationRetryPolicy RetryPolicy { get; set; }

    /// <summary>
    /// Gets or sets the optional compensation activity registered after successful execution.
    /// </summary>
    public OrchestrationCompensationDefinition<TData> Compensation { get; set; }
}

/// <summary>
/// Describes a transition from one state to another.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="TargetState">The state to move to when the condition evaluates to <c>true</c>.</param>
/// <param name="Condition">The transition condition evaluated against the current context.</param>
public record OrchestrationTransitionDefinition<TData>(
    string TargetState,
    Func<OrchestrationContext<TData>, bool> Condition)
    where TData : class, IOrchestrationData;

/// <summary>
/// Provides the fluent authoring surface for a signal-driven state behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationSignalBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds a class-based activity to run when the signal is processed.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current signal builder.</returns>
    IOrchestrationSignalBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Adds an inline activity to run when the signal is processed.
    /// </summary>
    /// <param name="executeAsync">The inline activity delegate.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current signal builder.</returns>
    IOrchestrationSignalBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);

    /// <summary>
    /// Configures the target state after the signal has been processed.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current signal builder.</returns>
    IOrchestrationSignalBuilder<TData> TransitionTo(string targetState);
}

/// <summary>
/// Provides the fluent authoring surface for a typed signal-driven state behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TPayload">The signal payload type.</typeparam>
public interface IOrchestrationSignalBuilder<TData, TPayload> : IOrchestrationSignalBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Maps the typed signal payload into the orchestration context.
    /// </summary>
    /// <param name="map">The payload mapping callback.</param>
    /// <returns>The current signal builder.</returns>
    IOrchestrationSignalBuilder<TData, TPayload> MapToContext(Action<OrchestrationContext<TData>, TPayload> map);
}

/// <summary>
/// Provides the fluent authoring surface for a durable timer.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationTimerBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Configures the target state after the timer becomes due.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current timer builder.</returns>
    IOrchestrationTimerBuilder<TData> TransitionTo(string targetState);
}

/// <summary>
/// Describes a configured signal-driven orchestration behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationSignalDefinition<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSignalDefinition{TData}"/> class.
    /// </summary>
    /// <param name="signalName">The signal name.</param>
    /// <param name="payloadType">The optional payload type.</param>
    public OrchestrationSignalDefinition(string signalName, Type payloadType = null)
    {
        this.SignalName = signalName ?? throw new ArgumentNullException(nameof(signalName));
        this.PayloadType = payloadType;
    }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string SignalName { get; }

    /// <summary>
    /// Gets the optional payload type.
    /// </summary>
    public Type PayloadType { get; }

    /// <summary>
    /// Gets the inline or class-based activities that run when the signal is processed.
    /// </summary>
    public List<OrchestrationActivityDefinition<TData>> Activities { get; } = [];

    /// <summary>
    /// Gets or sets the payload mapping callback.
    /// </summary>
    public Action<OrchestrationContext<TData>, object> MapToContextAction { get; set; }

    /// <summary>
    /// Gets or sets the target state.
    /// </summary>
    public string TargetState { get; set; }
}

/// <summary>
/// Describes a configured durable orchestration timer.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationTimerDefinition<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationTimerDefinition{TData}"/> class.
    /// </summary>
    /// <param name="delay">The timer delay.</param>
    public OrchestrationTimerDefinition(TimeSpan delay)
    {
        this.Delay = delay;
    }

    /// <summary>
    /// Gets the timer delay.
    /// </summary>
    public TimeSpan Delay { get; }

    /// <summary>
    /// Gets or sets the target state.
    /// </summary>
    public string TargetState { get; set; }
}

/// <summary>
/// Builds signal-driven orchestration behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationSignalBuilder<TData> : IOrchestrationSignalBuilder<TData>
    where TData : class, IOrchestrationData
{
    private readonly OrchestrationSignalDefinition<TData> definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSignalBuilder{TData}"/> class.
    /// </summary>
    /// <param name="definition">The signal definition being configured.</param>
    public OrchestrationSignalBuilder(OrchestrationSignalDefinition<TData> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Adds a class-based activity to run when the signal is processed.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current signal builder.</returns>
    public IOrchestrationSignalBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        this.definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            typeof(TActivity).PrettyName(false),
            serviceProvider => ActivatorUtilities.CreateInstance<TActivity>(serviceProvider)));

        return this;
    }

    /// <summary>
    /// Adds an inline activity to run when the signal is processed.
    /// </summary>
    /// <param name="executeAsync">The inline activity delegate.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current signal builder.</returns>
    public IOrchestrationSignalBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        this.definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            string.IsNullOrWhiteSpace(name) ? $"SignalActivity{this.definition.Activities.Count + 1}" : name,
            _ => new InlineOrchestrationActivity<TData>(executeAsync)));

        return this;
    }

    /// <summary>
    /// Configures the target state after the signal has been processed.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current signal builder.</returns>
    public IOrchestrationSignalBuilder<TData> TransitionTo(string targetState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);
        this.definition.TargetState = targetState;
        return this;
    }
}

/// <summary>
/// Builds typed signal-driven orchestration behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <typeparam name="TPayload">The signal payload type.</typeparam>
public class OrchestrationSignalBuilder<TData, TPayload> : OrchestrationSignalBuilder<TData>, IOrchestrationSignalBuilder<TData, TPayload>
    where TData : class, IOrchestrationData
{
    private readonly OrchestrationSignalDefinition<TData> definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSignalBuilder{TData,TPayload}"/> class.
    /// </summary>
    /// <param name="definition">The signal definition being configured.</param>
    public OrchestrationSignalBuilder(OrchestrationSignalDefinition<TData> definition)
        : base(definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Maps the typed signal payload into the orchestration context.
    /// </summary>
    /// <param name="map">The payload mapping callback.</param>
    /// <returns>The current signal builder.</returns>
    public IOrchestrationSignalBuilder<TData, TPayload> MapToContext(Action<OrchestrationContext<TData>, TPayload> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.definition.MapToContextAction = (context, payload) => map(context, (TPayload)payload);
        return this;
    }
}

/// <summary>
/// Builds durable timer behavior.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationTimerBuilder<TData> : IOrchestrationTimerBuilder<TData>
    where TData : class, IOrchestrationData
{
    private readonly OrchestrationTimerDefinition<TData> definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationTimerBuilder{TData}"/> class.
    /// </summary>
    /// <param name="definition">The timer definition being configured.</param>
    public OrchestrationTimerBuilder(OrchestrationTimerDefinition<TData> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Configures the target state after the timer becomes due.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current timer builder.</returns>
    public IOrchestrationTimerBuilder<TData> TransitionTo(string targetState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);
        this.definition.TargetState = targetState;
        return this;
    }
}

/// <summary>
/// Adapts an inline delegate into an orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="executeAsync">The delegate that executes the activity.</param>
public class InlineOrchestrationActivity<TData>(
    Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync)
    : IOrchestrationActivity<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Executes the inline activity delegate.
    /// </summary>
    /// <param name="context">The orchestration execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The activity outcome.</returns>
    public Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<TData> context, CancellationToken cancellationToken = default)
    {
        return executeAsync(context, cancellationToken);
    }
}

/// <summary>
/// Defines the terminal directive configured for a state.
/// </summary>
public enum OrchestrationTerminalDirectiveKind
{
    /// <summary>
    /// No terminal directive is configured.
    /// </summary>
    None,

    /// <summary>
    /// The orchestration completes successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// The orchestration is canceled.
    /// </summary>
    Cancel,

    /// <summary>
    /// The orchestration is terminated.
    /// </summary>
    Terminate,

    /// <summary>
    /// The orchestration pauses in a waiting state.
    /// </summary>
    Wait,
}
