// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Defines durable retry behavior for orchestration activities.
/// </summary>
public sealed record OrchestrationRetryPolicy
{
    /// <summary>
    /// Gets the maximum number of execution attempts including the initial attempt.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the base delay applied between retry attempts.
    /// </summary>
    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the backoff mode used to compute retry delays.
    /// </summary>
    public OrchestrationRetryBackoffMode BackoffMode { get; init; } = OrchestrationRetryBackoffMode.FixedDelay;

    /// <summary>
    /// Gets the factor used when <see cref="BackoffMode"/> is exponential.
    /// </summary>
    public double ExponentialFactor { get; init; } = 2d;

    /// <summary>
    /// Calculates the delay for a retry attempt.
    /// </summary>
    /// <param name="retryAttempt">The 1-based retry attempt number after the initial execution.</param>
    /// <returns>The computed delay before the retry is scheduled.</returns>
    public TimeSpan GetDelay(int retryAttempt)
    {
        if (retryAttempt <= 0 || this.BackoffMode == OrchestrationRetryBackoffMode.Immediate || this.Delay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (this.BackoffMode == OrchestrationRetryBackoffMode.FixedDelay)
        {
            return this.Delay;
        }

        var factor = Math.Pow(this.ExponentialFactor <= 1d ? 2d : this.ExponentialFactor, retryAttempt - 1);
        var ticks = Math.Min(TimeSpan.MaxValue.Ticks, (long)(this.Delay.Ticks * factor));
        return TimeSpan.FromTicks(ticks);
    }
}

/// <summary>
/// Specifies how orchestration activity retry delays are calculated.
/// </summary>
public enum OrchestrationRetryBackoffMode
{
    /// <summary>
    /// Retries immediately without delay.
    /// </summary>
    Immediate,

    /// <summary>
    /// Retries using a constant delay.
    /// </summary>
    FixedDelay,

    /// <summary>
    /// Retries using an exponentially increasing delay.
    /// </summary>
    Exponential,
}

/// <summary>
/// Configures advanced behavior for an orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Applies a retry policy to the activity.
    /// </summary>
    /// <param name="policy">The retry policy to apply.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationActivityBuilder<TData> Retry(OrchestrationRetryPolicy policy);

    /// <summary>
    /// Registers a compensation activity that is executed if the orchestration compensates.
    /// </summary>
    /// <typeparam name="TActivity">The compensation activity type.</typeparam>
    /// <returns>The current builder.</returns>
    IOrchestrationActivityBuilder<TData> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Registers an inline compensation activity that is executed if the orchestration compensates.
    /// </summary>
    /// <param name="executeAsync">The compensation delegate.</param>
    /// <param name="name">The optional compensation activity name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationActivityBuilder<TData> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);
}

internal sealed class OrchestrationActivityBuilder<TData>(OrchestrationActivityDefinition<TData> definition) : IOrchestrationActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    public IOrchestrationActivityBuilder<TData> Retry(OrchestrationRetryPolicy policy)
    {
        definition.RetryPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public IOrchestrationActivityBuilder<TData> CompensateWith<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        definition.Compensation = new OrchestrationCompensationDefinition<TData>(
            typeof(TActivity).PrettyName(false),
            serviceProvider => ActivatorUtilities.CreateInstance<TActivity>(serviceProvider));
        return this;
    }

    public IOrchestrationActivityBuilder<TData> CompensateWith(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        definition.Compensation = new OrchestrationCompensationDefinition<TData>(
            string.IsNullOrWhiteSpace(name) ? $"{definition.Name}Compensation" : name,
            _ => new InlineOrchestrationActivity<TData>(executeAsync));
        return this;
    }
}

/// <summary>
/// Describes an activity used to compensate previously completed work.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public record OrchestrationCompensationDefinition<TData>(
    string Name,
    Func<IServiceProvider, IOrchestrationActivity<TData>> Factory)
    where TData : class, IOrchestrationData;

/// <summary>
/// Builds a parallel orchestration activity composed of multiple branches.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationParallelBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds a named branch to the parallel activity.
    /// </summary>
    /// <param name="name">The branch name.</param>
    /// <param name="configure">The branch configuration callback.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationParallelBuilder<TData> Branch(string name, Action<IOrchestrationBranchBuilder<TData>> configure);

    /// <summary>
    /// Completes the parallel activity when all branches complete.
    /// </summary>
    /// <returns>The current builder.</returns>
    IOrchestrationParallelBuilder<TData> JoinAll();

    /// <summary>
    /// Completes the parallel activity when any branch completes.
    /// </summary>
    /// <returns>The current builder.</returns>
    IOrchestrationParallelBuilder<TData> JoinAny();
}

/// <summary>
/// Builds a single branch within a parallel orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationBranchBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds an activity to the branch.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current builder.</returns>
    IOrchestrationBranchBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Adds an inline activity to the branch.
    /// </summary>
    /// <param name="executeAsync">The activity delegate.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationBranchBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);
}

/// <summary>
/// Builds a looping orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationLoopBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Repeats the loop body while the condition evaluates to <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The continuation condition.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationLoopBuilder<TData> While(Func<OrchestrationContext<TData>, bool> condition);

    /// <summary>
    /// Repeats the loop body until the condition evaluates to <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The completion condition.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationLoopBuilder<TData> DoUntil(Func<OrchestrationContext<TData>, bool> condition);

    /// <summary>
    /// Limits the number of loop iterations.
    /// </summary>
    /// <param name="count">The maximum iteration count.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationLoopBuilder<TData> MaxIterations(int count);

    /// <summary>
    /// Adds an activity to the loop body.
    /// </summary>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    /// <returns>The current builder.</returns>
    IOrchestrationLoopBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>;

    /// <summary>
    /// Adds an inline activity to the loop body.
    /// </summary>
    /// <param name="executeAsync">The activity delegate.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationLoopBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null);
}

/// <summary>
/// Configures an activity that starts and optionally waits on a child orchestration.
/// </summary>
/// <typeparam name="TData">The parent orchestration data type.</typeparam>
/// <typeparam name="TChildData">The child orchestration data type.</typeparam>
public interface IOrchestrationStartChildActivityBuilder<TData, TChildData>
    where TData : class, IOrchestrationData
    where TChildData : class, IOrchestrationData
{
    /// <summary>
    /// Sets the factory used to create the child orchestration data.
    /// </summary>
    /// <param name="factory">The child data factory.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationStartChildActivityBuilder<TData, TChildData> WithData(Func<OrchestrationContext<TData>, TChildData> factory);

    /// <summary>
    /// Stores the started child instance identifier on the parent context.
    /// </summary>
    /// <param name="assign">The assignment callback.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationStartChildActivityBuilder<TData, TChildData> StoreInstanceId(Action<OrchestrationContext<TData>, Guid> assign);

    /// <summary>
    /// Waits until the child orchestration reaches a terminal status.
    /// </summary>
    /// <returns>The current builder.</returns>
    IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForCompletion();

    /// <summary>
    /// Waits until the child orchestration reaches any of the specified states.
    /// </summary>
    /// <param name="states">The states to match.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForStates(params string[] states);

    /// <summary>
    /// Waits until the child orchestration produces any of the specified outcomes.
    /// </summary>
    /// <param name="outcomes">The outcomes to match.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForOutcomes(params string[] outcomes);
}

/// <summary>
/// Represents an approval decision payload supplied by a signal.
/// </summary>
public record ApprovalDecisionSignal
{
    /// <summary>
    /// Gets the user who resolved the approval.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the resolver comment.
    /// </summary>
    public string Comment { get; init; }

    /// <summary>
    /// Gets the optional decision reason.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets the resolution timestamp.
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; init; }
}

/// <summary>
/// Represents a human task completion or cancellation payload supplied by a signal.
/// </summary>
public record HumanTaskResolutionSignal
{
    /// <summary>
    /// Gets the user who resolved the task.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the task outcome.
    /// </summary>
    public string Outcome { get; init; }

    /// <summary>
    /// Gets the resolver comment.
    /// </summary>
    public string Comment { get; init; }

    /// <summary>
    /// Gets the resolution timestamp.
    /// </summary>
    public DateTimeOffset? ResolvedUtc { get; init; }
}

/// <summary>
/// Configures an approval activity backed by orchestration signals.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationApprovalActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Sets the approval title.
    /// </summary>
    /// <param name="titleFactory">The title factory.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> Title(Func<OrchestrationContext<TData>, string> titleFactory);

    /// <summary>
    /// Sets the assigned role.
    /// </summary>
    /// <param name="role">The assigned role.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> AssignedToRole(string role);

    /// <summary>
    /// Sets the approval signal name.
    /// </summary>
    /// <param name="signalName">The approval signal name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> ApprovedSignal(string signalName);

    /// <summary>
    /// Sets the rejection signal name.
    /// </summary>
    /// <param name="signalName">The rejection signal name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> RejectedSignal(string signalName);

    /// <summary>
    /// Registers a handler for approval decisions.
    /// </summary>
    /// <param name="handler">The approval handler.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> OnApproved(Action<OrchestrationContext<TData>, ApprovalDecisionSignal> handler);

    /// <summary>
    /// Registers a handler for rejection decisions.
    /// </summary>
    /// <param name="handler">The rejection handler.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> OnRejected(Action<OrchestrationContext<TData>, ApprovalDecisionSignal> handler);

    /// <summary>
    /// Sets the target state used after approval.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> ApprovedTransition(string targetState);

    /// <summary>
    /// Sets the target state used after rejection.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationApprovalActivityBuilder<TData> RejectedTransition(string targetState);
}

/// <summary>
/// Configures a human task activity backed by orchestration signals.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationHumanTaskActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Sets the human task title.
    /// </summary>
    /// <param name="titleFactory">The title factory.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> Title(Func<OrchestrationContext<TData>, string> titleFactory);

    /// <summary>
    /// Sets the task description.
    /// </summary>
    /// <param name="description">The task description.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> Description(string description);

    /// <summary>
    /// Sets the assigned role.
    /// </summary>
    /// <param name="role">The assigned role.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> AssignedToRole(string role);

    /// <summary>
    /// Sets the completion signal name.
    /// </summary>
    /// <param name="signalName">The completion signal name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> CompletedSignal(string signalName);

    /// <summary>
    /// Sets the cancellation signal name.
    /// </summary>
    /// <param name="signalName">The cancellation signal name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> CancelledSignal(string signalName);

    /// <summary>
    /// Registers a handler for task completion.
    /// </summary>
    /// <param name="handler">The completion handler.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> OnCompleted(Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> handler);

    /// <summary>
    /// Registers a handler for task cancellation.
    /// </summary>
    /// <param name="handler">The cancellation handler.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> OnCancelled(Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> handler);

    /// <summary>
    /// Sets the target state used after completion.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> CompletedTransition(string targetState);

    /// <summary>
    /// Sets the target state used after cancellation.
    /// </summary>
    /// <param name="targetState">The target state name.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationHumanTaskActivityBuilder<TData> CancelledTransition(string targetState);
}

internal sealed class OrchestrationParallelBuilder<TData>(OrchestrationParallelDefinition<TData> definition) : IOrchestrationParallelBuilder<TData>
    where TData : class, IOrchestrationData
{
    public IOrchestrationParallelBuilder<TData> Branch(string name, Action<IOrchestrationBranchBuilder<TData>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var branch = new OrchestrationParallelBranchDefinition<TData>(name);
        configure(new OrchestrationBranchBuilder<TData>(branch));
        definition.Branches.Add(branch);
        return this;
    }

    public IOrchestrationParallelBuilder<TData> JoinAll()
    {
        definition.JoinMode = OrchestrationParallelJoinMode.All;
        return this;
    }

    public IOrchestrationParallelBuilder<TData> JoinAny()
    {
        definition.JoinMode = OrchestrationParallelJoinMode.Any;
        return this;
    }
}

internal sealed class OrchestrationBranchBuilder<TData>(OrchestrationParallelBranchDefinition<TData> definition) : IOrchestrationBranchBuilder<TData>
    where TData : class, IOrchestrationData
{
    public IOrchestrationBranchBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            typeof(TActivity).PrettyName(false),
            serviceProvider => ActivatorUtilities.CreateInstance<TActivity>(serviceProvider)));
        return this;
    }

    public IOrchestrationBranchBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            string.IsNullOrWhiteSpace(name) ? $"{definition.Name}Activity{definition.Activities.Count + 1}" : name,
            _ => new InlineOrchestrationActivity<TData>(executeAsync)));
        return this;
    }
}

internal sealed class OrchestrationLoopBuilder<TData>(OrchestrationLoopDefinition<TData> definition) : IOrchestrationLoopBuilder<TData>
    where TData : class, IOrchestrationData
{
    public IOrchestrationLoopBuilder<TData> While(Func<OrchestrationContext<TData>, bool> condition)
    {
        definition.Mode = OrchestrationLoopMode.While;
        definition.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public IOrchestrationLoopBuilder<TData> DoUntil(Func<OrchestrationContext<TData>, bool> condition)
    {
        definition.Mode = OrchestrationLoopMode.DoUntil;
        definition.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public IOrchestrationLoopBuilder<TData> MaxIterations(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        definition.MaxIterations = count;
        return this;
    }

    public IOrchestrationLoopBuilder<TData> Activity<TActivity>()
        where TActivity : class, IOrchestrationActivity<TData>
    {
        definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            typeof(TActivity).PrettyName(false),
            serviceProvider => ActivatorUtilities.CreateInstance<TActivity>(serviceProvider)));
        return this;
    }

    public IOrchestrationLoopBuilder<TData> Activity(
        Func<OrchestrationContext<TData>, CancellationToken, Task<OrchestrationOutcome>> executeAsync,
        string name = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        definition.Activities.Add(new OrchestrationActivityDefinition<TData>(
            string.IsNullOrWhiteSpace(name) ? $"{definition.Name}Activity{definition.Activities.Count + 1}" : name,
            _ => new InlineOrchestrationActivity<TData>(executeAsync)));
        return this;
    }
}

internal sealed class OrchestrationStartChildActivityBuilder<TData, TChildData> : IOrchestrationStartChildActivityBuilder<TData, TChildData>
    where TData : class, IOrchestrationData
    where TChildData : class, IOrchestrationData
{
    public Func<OrchestrationContext<TData>, TChildData> DataFactory { get; private set; }

    public Action<OrchestrationContext<TData>, Guid> StoreInstanceIdAction { get; private set; }

    public OrchestrationWaitFor WaitFor { get; private set; }

    public IOrchestrationStartChildActivityBuilder<TData, TChildData> WithData(Func<OrchestrationContext<TData>, TChildData> factory)
    {
        this.DataFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public IOrchestrationStartChildActivityBuilder<TData, TChildData> StoreInstanceId(Action<OrchestrationContext<TData>, Guid> assign)
    {
        this.StoreInstanceIdAction = assign ?? throw new ArgumentNullException(nameof(assign));
        return this;
    }

    public IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForCompletion()
    {
        this.WaitFor = BridgingIT.DevKit.Application.Orchestrations.WaitFor.Completion();
        return this;
    }

    public IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForStates(params string[] states)
    {
        this.WaitFor = BridgingIT.DevKit.Application.Orchestrations.WaitFor.State(states);
        return this;
    }

    public IOrchestrationStartChildActivityBuilder<TData, TChildData> WaitForOutcomes(params string[] outcomes)
    {
        this.WaitFor = BridgingIT.DevKit.Application.Orchestrations.WaitFor.Outcome(outcomes);
        return this;
    }
}

internal sealed class OrchestrationApprovalActivityBuilder<TData> : IOrchestrationApprovalActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    public Func<OrchestrationContext<TData>, string> TitleFactory { get; private set; }

    public string AssignedRole { get; private set; }

    public string ApprovedSignalName { get; private set; } = "Approved";

    public string RejectedSignalName { get; private set; } = "Rejected";

    public Action<OrchestrationContext<TData>, ApprovalDecisionSignal> ApprovedHandler { get; private set; }

    public Action<OrchestrationContext<TData>, ApprovalDecisionSignal> RejectedHandler { get; private set; }

    public string ApprovedTargetState { get; private set; }

    public string RejectedTargetState { get; private set; }

    public IOrchestrationApprovalActivityBuilder<TData> Title(Func<OrchestrationContext<TData>, string> titleFactory)
    {
        this.TitleFactory = titleFactory ?? throw new ArgumentNullException(nameof(titleFactory));
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> AssignedToRole(string role)
    {
        this.AssignedRole = role;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> ApprovedSignal(string signalName)
    {
        this.ApprovedSignalName = signalName;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> RejectedSignal(string signalName)
    {
        this.RejectedSignalName = signalName;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> OnApproved(Action<OrchestrationContext<TData>, ApprovalDecisionSignal> handler)
    {
        this.ApprovedHandler = handler;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> OnRejected(Action<OrchestrationContext<TData>, ApprovalDecisionSignal> handler)
    {
        this.RejectedHandler = handler;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> ApprovedTransition(string targetState)
    {
        this.ApprovedTargetState = targetState;
        return this;
    }

    public IOrchestrationApprovalActivityBuilder<TData> RejectedTransition(string targetState)
    {
        this.RejectedTargetState = targetState;
        return this;
    }
}

internal sealed class OrchestrationHumanTaskActivityBuilder<TData> : IOrchestrationHumanTaskActivityBuilder<TData>
    where TData : class, IOrchestrationData
{
    public Func<OrchestrationContext<TData>, string> TitleFactory { get; private set; }

    public string DescriptionText { get; private set; }

    public string AssignedRole { get; private set; }

    public string CompletedSignalName { get; private set; } = "TaskCompleted";

    public string CancelledSignalName { get; private set; } = "TaskCancelled";

    public Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> CompletedHandler { get; private set; }

    public Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> CancelledHandler { get; private set; }

    public string CompletedTargetState { get; private set; }

    public string CancelledTargetState { get; private set; }

    public IOrchestrationHumanTaskActivityBuilder<TData> Title(Func<OrchestrationContext<TData>, string> titleFactory)
    {
        this.TitleFactory = titleFactory ?? throw new ArgumentNullException(nameof(titleFactory));
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> Description(string description)
    {
        this.DescriptionText = description;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> AssignedToRole(string role)
    {
        this.AssignedRole = role;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> CompletedSignal(string signalName)
    {
        this.CompletedSignalName = signalName;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> CancelledSignal(string signalName)
    {
        this.CancelledSignalName = signalName;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> OnCompleted(Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> handler)
    {
        this.CompletedHandler = handler;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> OnCancelled(Action<OrchestrationContext<TData>, HumanTaskResolutionSignal> handler)
    {
        this.CancelledHandler = handler;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> CompletedTransition(string targetState)
    {
        this.CompletedTargetState = targetState;
        return this;
    }

    public IOrchestrationHumanTaskActivityBuilder<TData> CancelledTransition(string targetState)
    {
        this.CancelledTargetState = targetState;
        return this;
    }
}

internal enum OrchestrationParallelJoinMode
{
    All,
    Any,
}

internal sealed record OrchestrationParallelDefinition<TData>(string Name)
    where TData : class, IOrchestrationData
{
    public List<OrchestrationParallelBranchDefinition<TData>> Branches { get; } = [];

    public OrchestrationParallelJoinMode JoinMode { get; set; } = OrchestrationParallelJoinMode.All;
}

internal sealed record OrchestrationParallelBranchDefinition<TData>(string Name)
    where TData : class, IOrchestrationData
{
    public List<OrchestrationActivityDefinition<TData>> Activities { get; } = [];
}

internal enum OrchestrationLoopMode
{
    While,
    DoUntil,
}

internal sealed record OrchestrationLoopDefinition<TData>(string Name)
    where TData : class, IOrchestrationData
{
    public OrchestrationLoopMode Mode { get; set; } = OrchestrationLoopMode.While;

    public Func<OrchestrationContext<TData>, bool> Condition { get; set; }

    public int? MaxIterations { get; set; }

    public List<OrchestrationActivityDefinition<TData>> Activities { get; } = [];
}

internal sealed record OrchestrationChildExecutionState
{
    public Guid? InstanceId { get; init; }

    public string Status { get; init; }

    public string CurrentState { get; init; }

    public string Outcome { get; init; }
}

internal sealed record OrchestrationParallelExecutionState
{
    public Dictionary<string, OrchestrationParallelBranchState> Branches { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool JoinResolved { get; set; }
}

internal sealed record OrchestrationParallelBranchState
{
    public int ActivityIndex { get; set; }

    public bool Completed { get; set; }
}

internal sealed record OrchestrationLoopExecutionState
{
    public int Iteration { get; set; }

    public int ActivityIndex { get; set; }

    public bool Started { get; set; }

    public bool Completed { get; set; }
}

internal sealed record OrchestrationApprovalState
{
    public string Title { get; init; }

    public string AssignedRole { get; init; }

    public string Status { get; init; }

    public DateTimeOffset RequestedUtc { get; init; }

    public DateTimeOffset? ResolvedUtc { get; init; }

    public string ResolvedBy { get; init; }

    public string Comment { get; init; }

    public string Reason { get; init; }
}

internal sealed record OrchestrationHumanTaskState
{
    public string Title { get; init; }

    public string Description { get; init; }

    public string AssignedRole { get; init; }

    public string Status { get; init; }

    public DateTimeOffset RequestedUtc { get; init; }

    public DateTimeOffset? ResolvedUtc { get; init; }

    public string ResolvedBy { get; init; }

    public string Comment { get; init; }

    public string Outcome { get; init; }
}

/// <summary>
/// Provides built-in workflow helpers for common orchestration patterns.
/// </summary>
public static partial class OrchestrationWorkflowExtensions
{
    private const string ApprovalPrefix = OrchestrationRuntimeMetadata.ApprovalPrefix;
    private const string ChildPrefix = OrchestrationRuntimeMetadata.ChildPrefix;
    private const string HumanTaskPrefix = OrchestrationRuntimeMetadata.HumanTaskPrefix;
    private const string LoopPrefix = OrchestrationRuntimeMetadata.LoopPrefix;
    private const string ParallelPrefix = OrchestrationRuntimeMetadata.ParallelPrefix;

    /// <summary>
    /// Adds an activity that logs a message and continues execution.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="messageFactory">The message factory.</param>
    /// <param name="level">The log level.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> LogActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Func<OrchestrationContext<TData>, string> messageFactory,
        LogLevel level = LogLevel.Information,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(messageFactory);

        return builder.Activity(
            (context, cancellationToken) =>
            {
                context.Services.GetService<ILoggerFactory>()?
                    .CreateLogger("Orchestration.LogActivity")
                    .Log(level, "{Message}", messageFactory(context));
                return Task.FromResult(OrchestrationOutcome.Continue());
            },
            name ?? "LogActivity");
    }

    /// <summary>
    /// Adds an activity that mutates the orchestration context and continues execution.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="transform">The context mutation callback.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> TransformActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<OrchestrationContext<TData>> transform,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(transform);

        return builder.Activity(
            (context, cancellationToken) =>
            {
                transform(context);
                return Task.FromResult(OrchestrationOutcome.Continue());
            },
            name ?? "TransformActivity");
    }

    /// <summary>
    /// Adds an activity that computes the next orchestration outcome.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="decide">The decision callback.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> DecisionActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Func<OrchestrationContext<TData>, OrchestrationOutcome> decide,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(decide);

        return builder.Activity(
            (context, cancellationToken) => Task.FromResult(decide(context) ?? OrchestrationOutcome.Continue()),
            name ?? "DecisionActivity");
    }

    /// <summary>
    /// Adds an activity that places the orchestration into a waiting state.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="delay">The optional delay before resuming.</param>
    /// <param name="reason">The optional wait reason.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> WaitActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        TimeSpan? delay = null,
        string reason = null,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Activity(
            (context, cancellationToken) => Task.FromResult(delay.HasValue ? OrchestrationOutcome.Wait(delay.Value, reason) : OrchestrationOutcome.Wait(reason)),
            name ?? "WaitActivity");
    }

    /// <summary>
    /// Adds an activity that starts a child orchestration and optionally waits for it.
    /// </summary>
    /// <typeparam name="TData">The parent orchestration data type.</typeparam>
    /// <typeparam name="TChildOrchestration">The child orchestration type.</typeparam>
    /// <typeparam name="TChildData">The child orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="configure">The child activity configuration.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> StartChildOrchestrationActivity<TData, TChildOrchestration, TChildData>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationStartChildActivityBuilder<TData, TChildData>> configure,
        string name = null)
        where TData : class, IOrchestrationData
        where TChildOrchestration : Orchestration<TChildData>
        where TChildData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new OrchestrationStartChildActivityBuilder<TData, TChildData>();
        configure(settings);
        if (settings.DataFactory is null)
        {
            throw new InvalidOperationException("Child orchestration activities require configured child data.");
        }

        var activityName = name ?? $"Start{typeof(TChildOrchestration).Name}";
        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var runtime = context.Services.GetRequiredService<IOrchestrationService>();
                var queries = context.Services.GetRequiredService<IOrchestrationQueryStore>();
                var executionSettings = context.Services.GetService<OrchestrationExecutionSettings>();
                var executor = context.Services.GetService<InMemoryOrchestrationExecutor>();
                var history = context.Services.GetService<IOrchestrationHistoryStore>();
                var key = OrchestrationRuntimeMetadata.BuildHelperKey(ChildPrefix, context, activityName);
                var state = context.Properties.TryGet<OrchestrationChildExecutionState>(key, out var childState)
                    ? childState
                    : null;

                if (state?.InstanceId is null)
                {
                    var dispatch = await runtime.DispatchAsync<TChildOrchestration, TChildData>(settings.DataFactory(context), cancellationToken).ConfigureAwait(false);
                    if (dispatch.IsFailure)
                    {
                        return OrchestrationOutcome.Terminate(dispatch.Errors.FirstOrDefault()?.Message ?? "Child orchestration dispatch failed.");
                    }

                    settings.StoreInstanceIdAction?.Invoke(context, dispatch.Value);
                    state = new OrchestrationChildExecutionState { InstanceId = dispatch.Value };
                    context.Properties[key] = state;
                    await AppendHistoryAsync(history, context.InstanceId, "ChildOrchestrationStarted", context.CurrentState, activityName, dispatch.Value.ToString(), cancellationToken).ConfigureAwait(false);
                }

                if (executionSettings?.EnableBackgroundExecution == false && executor is not null)
                {
                    await executor.ContinueInstanceAsync(state.InstanceId!.Value, cancellationToken).ConfigureAwait(false);
                }

                var snapshot = await queries.GetInstanceAsync(state.InstanceId!.Value, cancellationToken).ConfigureAwait(false);
                if (snapshot is null)
                {
                    return OrchestrationOutcome.Terminate($"Child orchestration '{state.InstanceId}' was not found.");
                }

                var outcome = await GetOutcomeAsync<TChildData>(queries, snapshot, state.InstanceId.Value, context.Services, cancellationToken).ConfigureAwait(false);
                context.Properties[key] = state with
                {
                    Status = snapshot.Status.ToString(),
                    CurrentState = snapshot.CurrentState,
                    Outcome = outcome,
                };

                if (settings.WaitFor is null || MatchesWaitCondition(snapshot, outcome, settings.WaitFor))
                {
                    await AppendHistoryAsync(history, context.InstanceId, "ChildOrchestrationCompleted", context.CurrentState, activityName, state.InstanceId.ToString(), cancellationToken).ConfigureAwait(false);
                    return OrchestrationOutcome.Continue();
                }

                return OrchestrationOutcome.Wait(TimeSpan.FromMilliseconds(20), $"Waiting for child orchestration '{state.InstanceId}'.");
            },
            activityName);
    }

    /// <summary>
    /// Adds an activity that executes multiple branches in parallel.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="configure">The parallel activity configuration.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> Parallel<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationParallelBuilder<TData>> configure,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var definition = new OrchestrationParallelDefinition<TData>(name ?? "Parallel");
        configure(new OrchestrationParallelBuilder<TData>(definition));
        if (definition.Branches.Count == 0)
        {
            throw new InvalidOperationException("Parallel activities require at least one branch.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var key = OrchestrationRuntimeMetadata.BuildHelperKey(ParallelPrefix, context, definition.Name);
                var state = context.Properties.TryGet<OrchestrationParallelExecutionState>(key, out var parallelState)
                    ? parallelState
                    : new OrchestrationParallelExecutionState
                    {
                        Branches = definition.Branches.ToDictionary(
                            branch => branch.Name,
                            _ => new OrchestrationParallelBranchState(),
                            StringComparer.OrdinalIgnoreCase),
                    };

                context.Properties[key] = state;

                while (true)
                {
                    if (state.JoinResolved)
                    {
                        context.Properties[key] = state;
                        return OrchestrationOutcome.Continue();
                    }

                    var progressed = false;
                    foreach (var branch in definition.Branches)
                    {
                        var branchState = state.Branches[branch.Name];
                        if (branchState.Completed)
                        {
                            continue;
                        }

                        if (branchState.ActivityIndex >= branch.Activities.Count)
                        {
                            branchState.Completed = true;
                            context.Properties[key] = state;
                            await CheckpointAsync(
                                    context,
                                    $"Persist parallel branch '{branch.Name}' completion",
                                    cancellationToken,
                                    CreateHistoryEntry(context, "ParallelBranchCompleted", definition.Name, branch.Name))
                                .ConfigureAwait(false);
                            progressed = true;
                            continue;
                        }

                        var activity = branch.Activities[branchState.ActivityIndex];
                        await BeginActivityMutationAsync(
                                context,
                                $"Execute parallel branch activity '{branch.Name}:{activity.Name}'",
                                cancellationToken)
                            .ConfigureAwait(false);
                        var outcome = await activity.Factory(context.Services).ExecuteAsync(context, cancellationToken).ConfigureAwait(false) ?? OrchestrationOutcome.Continue();

                        if (outcome.Kind == OrchestrationOutcomeKind.Continue)
                        {
                            branchState.ActivityIndex++;
                            var historyEntries = new List<OrchestrationHistoryEntry>
                            {
                                CreateHistoryEntry(context, "ParallelBranchActivityExecuted", activity.Name, branch.Name),
                            };

                            progressed = true;
                            if (branchState.ActivityIndex >= branch.Activities.Count)
                            {
                                branchState.Completed = true;
                                historyEntries.Add(CreateHistoryEntry(context, "ParallelBranchCompleted", definition.Name, branch.Name));
                            }

                            context.Properties[key] = state;
                            await CheckpointAsync(
                                    context,
                                    $"Persist parallel branch '{branch.Name}' progress",
                                    cancellationToken,
                                    historyEntries)
                                .ConfigureAwait(false);
                            continue;
                        }

                        if (outcome.Kind == OrchestrationOutcomeKind.Wait)
                        {
                            context.Properties[key] = state;
                            return outcome.Delay.HasValue ? OrchestrationOutcome.Wait(outcome.Delay.Value, outcome.Reason) : OrchestrationOutcome.Wait(outcome.Reason);
                        }

                        if (outcome.Kind == OrchestrationOutcomeKind.Retry)
                        {
                            context.Properties[key] = state;
                            return OrchestrationOutcome.Wait(TimeSpan.Zero, $"Retrying parallel branch '{branch.Name}'.");
                        }

                        return outcome;
                    }

                    var completedCount = state.Branches.Values.Count(item => item.Completed);
                    var joinSatisfied = definition.JoinMode == OrchestrationParallelJoinMode.All
                        ? completedCount == definition.Branches.Count
                        : completedCount > 0;

                    context.Properties[key] = state;
                    if (joinSatisfied)
                    {
                        if (!state.JoinResolved)
                        {
                            state.JoinResolved = true;
                            context.Properties[key] = state;
                            await CheckpointAsync(
                                    context,
                                    $"Persist parallel join '{definition.Name}' resolution",
                                    cancellationToken,
                                    CreateHistoryEntry(context, "ParallelJoinResolved", definition.Name, definition.JoinMode.ToString()))
                                .ConfigureAwait(false);
                        }

                        return OrchestrationOutcome.Continue();
                    }

                    if (!progressed)
                    {
                        return OrchestrationOutcome.Wait(TimeSpan.Zero, $"Waiting for parallel branches '{definition.Name}'.");
                    }
                }
            },
            definition.Name);
    }

    /// <summary>
    /// Adds an activity that repeatedly executes a configured loop body.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="name">The loop activity name.</param>
    /// <param name="configure">The loop configuration.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> Loop<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        string name,
        Action<IOrchestrationLoopBuilder<TData>> configure)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var definition = new OrchestrationLoopDefinition<TData>(name);
        configure(new OrchestrationLoopBuilder<TData>(definition));
        if (definition.Condition is null)
        {
            throw new InvalidOperationException("Loop activities require a While or DoUntil condition.");
        }

        if (definition.Activities.Count == 0)
        {
            throw new InvalidOperationException("Loop activities require at least one activity.");
        }

        return builder.Activity(
            async (context, cancellationToken) =>
            {
                var key = OrchestrationRuntimeMetadata.BuildHelperKey(LoopPrefix, context, name);
                var state = context.Properties.TryGet<OrchestrationLoopExecutionState>(key, out var loopState)
                    ? loopState
                    : new OrchestrationLoopExecutionState();

                while (true)
                {
                    if (state.Completed)
                    {
                        context.Properties[key] = state;
                        return OrchestrationOutcome.Continue();
                    }

                    var shouldRun = definition.Mode == OrchestrationLoopMode.While
                        ? definition.Condition(context)
                        : !state.Started || !definition.Condition(context);

                    if (!shouldRun)
                    {
                        state.Completed = true;
                        context.Properties[key] = state;
                        await CheckpointAsync(
                                context,
                                $"Persist loop '{name}' completion",
                                cancellationToken,
                                CreateHistoryEntry(context, "LoopCompleted", name, state.Iteration.ToString()))
                            .ConfigureAwait(false);
                        return OrchestrationOutcome.Continue();
                    }

                    if (definition.MaxIterations.HasValue && state.Iteration >= definition.MaxIterations.Value)
                    {
                        return OrchestrationOutcome.Terminate($"Loop '{name}' exceeded its maximum iteration count.");
                    }

                    state.Started = true;
                    var activity = definition.Activities[state.ActivityIndex];
                    await BeginActivityMutationAsync(
                            context,
                            $"Execute loop activity '{name}:{activity.Name}'",
                            cancellationToken)
                        .ConfigureAwait(false);
                    var outcome = await activity.Factory(context.Services).ExecuteAsync(context, cancellationToken).ConfigureAwait(false) ?? OrchestrationOutcome.Continue();
                    if (outcome.Kind == OrchestrationOutcomeKind.Continue)
                    {
                        state.ActivityIndex++;
                        var historyEntries = new List<OrchestrationHistoryEntry>();
                        if (state.ActivityIndex >= definition.Activities.Count)
                        {
                            state.ActivityIndex = 0;
                            state.Iteration++;
                            historyEntries.Add(CreateHistoryEntry(context, "LoopIterationCompleted", name, state.Iteration.ToString()));
                        }

                        context.Properties[key] = state;
                        await CheckpointAsync(
                                context,
                                $"Persist loop '{name}' progress",
                                cancellationToken,
                                historyEntries)
                            .ConfigureAwait(false);
                        continue;
                    }

                    if (outcome.Kind == OrchestrationOutcomeKind.Retry)
                    {
                        context.Properties[key] = state;
                        return OrchestrationOutcome.Wait(TimeSpan.Zero, $"Retry loop '{name}'.");
                    }

                    context.Properties[key] = state;
                    return outcome;
                }
            },
            name);
    }

    /// <summary>
    /// Adds an approval helper activity backed by approval and rejection signals.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="configure">The approval activity configuration.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> ApprovalActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationApprovalActivityBuilder<TData>> configure,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var activityName = name ?? "ApprovalActivity";
        var definition = new OrchestrationApprovalActivityBuilder<TData>();
        configure(definition);

        builder.Activity(
            (context, cancellationToken) =>
            {
                var key = OrchestrationRuntimeMetadata.BuildHelperKey(ApprovalPrefix, context, activityName);
                if (!context.Properties.Contains(key))
                {
                    context.Properties[key] = new OrchestrationApprovalState
                    {
                        Title = definition.TitleFactory?.Invoke(context),
                        AssignedRole = definition.AssignedRole,
                        Status = "Pending",
                        RequestedUtc = DateTimeOffset.UtcNow,
                    };
                }

                return Task.FromResult(OrchestrationOutcome.Continue());
            },
            activityName);

        builder.WaitForSignal<ApprovalDecisionSignal>(definition.ApprovedSignalName, signal =>
        {
            signal.MapToContext((context, payload) =>
            {
                context.Properties[OrchestrationRuntimeMetadata.BuildHelperKey(ApprovalPrefix, context, activityName)] = new OrchestrationApprovalState
                {
                    Title = definition.TitleFactory?.Invoke(context),
                    AssignedRole = definition.AssignedRole,
                    Status = "Approved",
                    RequestedUtc = DateTimeOffset.UtcNow,
                    ResolvedUtc = payload.ResolvedUtc ?? DateTimeOffset.UtcNow,
                    ResolvedBy = payload.UserId,
                    Comment = payload.Comment,
                    Reason = payload.Reason,
                };

                definition.ApprovedHandler?.Invoke(context, payload);
            });

            if (!string.IsNullOrWhiteSpace(definition.ApprovedTargetState))
            {
                signal.TransitionTo(definition.ApprovedTargetState);
            }
        });

        builder.WaitForSignal<ApprovalDecisionSignal>(definition.RejectedSignalName, signal =>
        {
            signal.MapToContext((context, payload) =>
            {
                context.Properties[OrchestrationRuntimeMetadata.BuildHelperKey(ApprovalPrefix, context, activityName)] = new OrchestrationApprovalState
                {
                    Title = definition.TitleFactory?.Invoke(context),
                    AssignedRole = definition.AssignedRole,
                    Status = "Rejected",
                    RequestedUtc = DateTimeOffset.UtcNow,
                    ResolvedUtc = payload.ResolvedUtc ?? DateTimeOffset.UtcNow,
                    ResolvedBy = payload.UserId,
                    Comment = payload.Comment,
                    Reason = payload.Reason,
                };

                definition.RejectedHandler?.Invoke(context, payload);
            });

            if (!string.IsNullOrWhiteSpace(definition.RejectedTargetState))
            {
                signal.TransitionTo(definition.RejectedTargetState);
            }
        });

        return builder;
    }

    /// <summary>
    /// Adds a human-task helper activity backed by completion and cancellation signals.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="builder">The state builder.</param>
    /// <param name="configure">The human-task activity configuration.</param>
    /// <param name="name">The optional activity name.</param>
    /// <returns>The state builder.</returns>
    public static IOrchestrationStateBuilder<TData> HumanTaskActivity<TData>(
        this IOrchestrationStateBuilder<TData> builder,
        Action<IOrchestrationHumanTaskActivityBuilder<TData>> configure,
        string name = null)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var activityName = name ?? "HumanTaskActivity";
        var definition = new OrchestrationHumanTaskActivityBuilder<TData>();
        configure(definition);

        builder.Activity(
            (context, cancellationToken) =>
            {
                var key = OrchestrationRuntimeMetadata.BuildHelperKey(HumanTaskPrefix, context, activityName);
                if (!context.Properties.Contains(key))
                {
                    context.Properties[key] = new OrchestrationHumanTaskState
                    {
                        Title = definition.TitleFactory?.Invoke(context),
                        Description = definition.DescriptionText,
                        AssignedRole = definition.AssignedRole,
                        Status = "Pending",
                        RequestedUtc = DateTimeOffset.UtcNow,
                    };
                }

                return Task.FromResult(OrchestrationOutcome.Continue());
            },
            activityName);

        builder.WaitForSignal<HumanTaskResolutionSignal>(definition.CompletedSignalName, signal =>
        {
            signal.MapToContext((context, payload) =>
            {
                context.Properties[OrchestrationRuntimeMetadata.BuildHelperKey(HumanTaskPrefix, context, activityName)] = new OrchestrationHumanTaskState
                {
                    Title = definition.TitleFactory?.Invoke(context),
                    Description = definition.DescriptionText,
                    AssignedRole = definition.AssignedRole,
                    Status = "Completed",
                    RequestedUtc = DateTimeOffset.UtcNow,
                    ResolvedUtc = payload.ResolvedUtc ?? DateTimeOffset.UtcNow,
                    ResolvedBy = payload.UserId,
                    Comment = payload.Comment,
                    Outcome = payload.Outcome,
                };

                definition.CompletedHandler?.Invoke(context, payload);
            });

            if (!string.IsNullOrWhiteSpace(definition.CompletedTargetState))
            {
                signal.TransitionTo(definition.CompletedTargetState);
            }
        });

        builder.WaitForSignal<HumanTaskResolutionSignal>(definition.CancelledSignalName, signal =>
        {
            signal.MapToContext((context, payload) =>
            {
                context.Properties[OrchestrationRuntimeMetadata.BuildHelperKey(HumanTaskPrefix, context, activityName)] = new OrchestrationHumanTaskState
                {
                    Title = definition.TitleFactory?.Invoke(context),
                    Description = definition.DescriptionText,
                    AssignedRole = definition.AssignedRole,
                    Status = "Cancelled",
                    RequestedUtc = DateTimeOffset.UtcNow,
                    ResolvedUtc = payload.ResolvedUtc ?? DateTimeOffset.UtcNow,
                    ResolvedBy = payload.UserId,
                    Comment = payload.Comment,
                    Outcome = payload.Outcome,
                };

                definition.CancelledHandler?.Invoke(context, payload);
            });

            if (!string.IsNullOrWhiteSpace(definition.CancelledTargetState))
            {
                signal.TransitionTo(definition.CancelledTargetState);
            }
        });

        return builder;
    }

    private static async Task<string> GetOutcomeAsync<TData>(
        IOrchestrationQueryStore queries,
        OrchestrationInstanceSnapshot snapshot,
        Guid instanceId,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var context = await queries.GetContextAsync<TData>(instanceId, services, cancellationToken).ConfigureAwait(false);
        return OrchestrationRuntimeMetadata.GetEffectiveOutcome(snapshot.Status, context?.LastOutcome);
    }

    private static bool MatchesWaitCondition(OrchestrationInstanceSnapshot snapshot, string outcome, OrchestrationWaitFor waitFor)
    {
        if (waitFor is null)
        {
            return true;
        }

        if (waitFor.Completion && snapshot.Status is OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated or OrchestrationStatus.Failed)
        {
            return true;
        }

        if (waitFor.States?.Any(item => string.Equals(item, snapshot.CurrentState, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        if (waitFor.Outcomes?.Any(item => string.Equals(item, outcome, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        return false;
    }

    private static async Task AppendHistoryAsync(
        IOrchestrationHistoryStore history,
        Guid instanceId,
        string eventType,
        string stateName,
        string activityName,
        string details,
        CancellationToken cancellationToken)
    {
        if (history is null)
        {
            return;
        }

        await history.AppendAsync(new OrchestrationHistoryEntry
        {
            InstanceId = instanceId,
            EventType = eventType,
            StateName = stateName,
            ActivityName = activityName,
            Details = details,
        }, cancellationToken).ConfigureAwait(false);
    }

    private static OrchestrationHistoryEntry CreateHistoryEntry<TData>(
        OrchestrationContext<TData> context,
        string eventType,
        string activityName,
        string details)
        where TData : class, IOrchestrationData
    {
        return new OrchestrationHistoryEntry
        {
            InstanceId = context.InstanceId,
            EventType = eventType,
            StateName = context.CurrentState,
            ActivityName = activityName,
            Details = details,
        };
    }

    private static Task CheckpointAsync<TData>(
        OrchestrationContext<TData> context,
        string actionName,
        CancellationToken cancellationToken,
        params OrchestrationHistoryEntry[] historyEntries)
        where TData : class, IOrchestrationData
    {
        var executor = context.Services.GetRequiredService<InMemoryOrchestrationExecutor>();
        return executor.CheckpointActivityAsync(context, actionName, historyEntries, cancellationToken);
    }

    private static Task CheckpointAsync<TData>(
        OrchestrationContext<TData> context,
        string actionName,
        CancellationToken cancellationToken,
        IReadOnlyCollection<OrchestrationHistoryEntry> historyEntries)
        where TData : class, IOrchestrationData
    {
        var executor = context.Services.GetRequiredService<InMemoryOrchestrationExecutor>();
        return executor.CheckpointActivityAsync(context, actionName, historyEntries, cancellationToken);
    }

    private static Task BeginActivityMutationAsync<TData>(
        OrchestrationContext<TData> context,
        string actionName,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var executor = context.Services.GetRequiredService<InMemoryOrchestrationExecutor>();
        return executor.BeginActivityMutationAsync(context, actionName, cancellationToken);
    }
}
