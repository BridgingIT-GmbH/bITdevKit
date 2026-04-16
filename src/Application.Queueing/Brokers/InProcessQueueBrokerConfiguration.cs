namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Provides configuration-bound settings for the in-process queue broker.
/// </summary>
public class InProcessQueueBrokerConfiguration
{
    /// <summary>
    /// Gets or sets the delay in milliseconds between processing iterations.
    /// </summary>
    /// <remarks>
    /// Use <c>0</c> when tests or low-latency local processing should dispatch queued work without an artificial delay.
    /// </remarks>
    public int ProcessDelay { get; set; }

    /// <summary>
    /// Gets or sets the optional lifetime after which a queued message is treated as expired.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Gets or sets the optional prefix applied when deriving logical queue names from message types.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the optional suffix applied when deriving logical queue names from message types.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent worker loops used by the in-process broker.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether processing should preserve strict item order when possible.
    /// </summary>
    public bool EnsureOrdered { get; set; } = true;
}
