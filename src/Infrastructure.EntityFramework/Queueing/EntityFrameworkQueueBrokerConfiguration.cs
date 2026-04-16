namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Provides configuration-bound settings for the Entity Framework queue broker.
/// </summary>
public class EntityFrameworkQueueBrokerConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the broker runtime is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether queue messages are saved immediately on enqueue.
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Gets or sets the startup delay before worker processing begins.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the interval between worker processing cycles.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets an optional delay applied before each worker processing cycle.
    /// </summary>
    public TimeSpan ProcessingDelay { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages processed in a single cycle.
    /// </summary>
    public int ProcessingCount { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts allowed per message.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration of a worker lease on a queue message.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the interval used to renew active leases.
    /// </summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the default expiration window applied to queued messages.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the age after which terminal queue messages may be archived automatically.
    /// </summary>
    public TimeSpan? AutoArchiveAfter { get; set; }

    /// <summary>
    /// Gets or sets the aggregate statuses eligible for automatic archiving.
    /// </summary>
    public IEnumerable<QueueMessageStatus> AutoArchiveStatuses { get; set; } =
    [
        QueueMessageStatus.Succeeded,
        QueueMessageStatus.DeadLettered,
        QueueMessageStatus.Expired
    ];

    /// <summary>
    /// Gets or sets the queue name prefix.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the queue name suffix.
    /// </summary>
    public string QueueNameSuffix { get; set; }
}