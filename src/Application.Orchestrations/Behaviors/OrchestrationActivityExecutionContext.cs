// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Identifies the kind of orchestration activity execution currently being processed.
/// </summary>
public enum OrchestrationActivityExecutionKind
{
    /// <summary>
    /// A regular state activity.
    /// </summary>
    Activity,

    /// <summary>
    /// An activity executed while processing a signal.
    /// </summary>
    SignalActivity,

    /// <summary>
    /// A compensation activity.
    /// </summary>
    Compensation,
}

/// <summary>
/// Represents the metadata associated with a single orchestration activity execution attempt.
/// </summary>
public sealed class OrchestrationActivityExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationActivityExecutionContext"/> class.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="orchestrationName">The orchestration definition name.</param>
    /// <param name="correlationId">The orchestration correlation identifier.</param>
    /// <param name="stateName">The current state name.</param>
    /// <param name="activityName">The current activity name.</param>
    /// <param name="kind">The activity execution kind.</param>
    /// <param name="attempt">The 1-based attempt number.</param>
    /// <param name="services">The scoped service provider for the current orchestration execution.</param>
    /// <param name="orchestrationContext">The concrete typed orchestration context as an object reference.</param>
    public OrchestrationActivityExecutionContext(
        Guid instanceId,
        string orchestrationName,
        string correlationId,
        string stateName,
        string activityName,
        OrchestrationActivityExecutionKind kind,
        int attempt,
        IServiceProvider services,
        object orchestrationContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(orchestrationContext);

        this.InstanceId = instanceId;
        this.OrchestrationName = orchestrationName;
        this.CorrelationId = correlationId;
        this.StateName = stateName;
        this.ActivityName = activityName;
        this.Kind = kind;
        this.Attempt = attempt;
        this.Services = services;
        this.OrchestrationContext = orchestrationContext;
    }

    /// <summary>
    /// Gets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; }

    /// <summary>
    /// Gets the orchestration correlation identifier.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the current state name.
    /// </summary>
    public string StateName { get; }

    /// <summary>
    /// Gets the current activity name.
    /// </summary>
    public string ActivityName { get; }

    /// <summary>
    /// Gets the activity execution kind.
    /// </summary>
    public OrchestrationActivityExecutionKind Kind { get; }

    /// <summary>
    /// Gets the 1-based execution attempt number for the current activity.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets the scoped service provider for the current orchestration execution.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the concrete typed orchestration context as an object reference.
    /// </summary>
    public object OrchestrationContext { get; }
}

/// <summary>
/// Represents the next delegate in the orchestration behavior pipeline.
/// </summary>
/// <returns>The orchestration outcome returned by the activity pipeline.</returns>
public delegate Task<OrchestrationOutcome> OrchestrationDelegate();
