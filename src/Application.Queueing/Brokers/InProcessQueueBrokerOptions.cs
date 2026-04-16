namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the runtime options for <see cref="InProcessQueueBroker"/>.
/// </summary>
public class InProcessQueueBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the enqueuer behaviors executed when messages are enqueued.
    /// </summary>
    public IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors executed when messages are processed.
    /// </summary>
    public IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the factory used to resolve queue message handlers.
    /// </summary>
    public IQueueMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer used to persist and restore queue message payloads.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets the delay in milliseconds between processing cycles.
    /// </summary>
    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    /// Gets or sets the optional expiration applied to queued messages.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Gets or sets the prefix applied to generated queue names.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the suffix applied to generated queue names.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages that may be processed concurrently.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether queue processing must preserve message order.
    /// </summary>
    public bool EnsureOrdered { get; set; } = true;
}
