// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the shared execution context of an orchestration instance.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public class OrchestrationContext<TData>
    where TData : class, IOrchestrationData
{
    private readonly PropertyBag properties = new();

    /// <summary>
    /// Initializes a new orchestration execution context.
    /// </summary>
    /// <param name="orchestrationName">The orchestration definition name.</param>
    /// <param name="data">The orchestration data instance.</param>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="instanceId">The optional orchestration instance identifier.</param>
    /// <param name="correlationId">The optional orchestration correlation identifier.</param>
    /// <param name="startedUtc">The optional orchestration start timestamp.</param>
    public OrchestrationContext(
        string orchestrationName,
        TData data,
        IServiceProvider services,
        Guid? instanceId = null,
        string correlationId = null,
        DateTimeOffset? startedUtc = null)
    {
        this.OrchestrationName = orchestrationName;
        this.InstanceId = instanceId ?? Guid.NewGuid();
        this.CorrelationId = correlationId ?? this.InstanceId.ToString("N");
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.Status = OrchestrationStatus.Created;
        this.StartedUtc = startedUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the orchestration definition name.
    /// </summary>
    public string OrchestrationName { get; }

    /// <summary>
    /// Gets the orchestration instance identifier.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the orchestration correlation identifier.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the current orchestration data.
    /// </summary>
    public TData Data { get; }

    /// <summary>
    /// Gets the scoped service provider available to activities.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the execution-scoped property bag.
    /// </summary>
    public PropertyBag Properties => this.properties;

    /// <summary>
    /// Gets or sets the current lifecycle status.
    /// </summary>
    public OrchestrationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the current business state name.
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the current activity name.
    /// </summary>
    public string CurrentActivity { get; set; }

    /// <summary>
    /// Gets the orchestration start timestamp in UTC.
    /// </summary>
    public DateTimeOffset StartedUtc { get; }

    /// <summary>
    /// Gets or sets the orchestration completion timestamp in UTC when available.
    /// </summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the last activity outcome observed by the executor.
    /// </summary>
    public OrchestrationOutcome LastOutcome { get; set; }

    /// <summary>
    /// Gets or sets the last failure reason when execution ends in a failed state.
    /// </summary>
    public string FailureReason { get; set; }
}