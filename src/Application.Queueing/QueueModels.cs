namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Describes capabilities supported by the active queue broker.
/// </summary>
/// <example>
/// <code>
/// if (summary.Capabilities.SupportsPauseResume)
/// {
///     await queueBrokerService.PauseQueueAsync("OrderQueue", cancellationToken);
/// }
/// </code>
/// </example>
public class QueueBrokerCapabilities
{
    public bool SupportsDurableStorage { get; set; }

    public bool SupportsRetry { get; set; }

    public bool SupportsArchive { get; set; }

    public bool SupportsLeaseManagement { get; set; }

    public bool SupportsCircuitManagement { get; set; }

    public bool SupportsPauseResume { get; set; }

    public bool SupportsWaitingMessageInspection { get; set; }
}

/// <summary>
/// Provides a summary view of queue broker runtime state.
/// </summary>
/// <example>
/// <code>
/// var summary = await queueBrokerService.GetSummaryAsync(cancellationToken);
/// Console.WriteLine($"Pending: {summary.Pending}, Waiting: {summary.WaitingForHandler}");
/// </code>
/// </example>
public class QueueBrokerSummary
{
    /// <summary>
    /// Gets or sets the total number of tracked messages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of pending messages.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of waiting-for-handler messages.
    /// </summary>
    public int WaitingForHandler { get; set; }

    /// <summary>
    /// Gets or sets the number of processing messages.
    /// </summary>
    public int Processing { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded messages.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of failed messages.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of dead-lettered messages.
    /// </summary>
    public int DeadLettered { get; set; }

    /// <summary>
    /// Gets or sets the number of expired messages.
    /// </summary>
    public int Expired { get; set; }

    /// <summary>
    /// Gets or sets the paused queues.
    /// </summary>
    public IReadOnlyCollection<string> PausedQueues { get; set; } = [];

    /// <summary>
    /// Gets or sets the paused message types.
    /// </summary>
    public IReadOnlyCollection<string> PausedTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the broker capabilities.
    /// </summary>
    public QueueBrokerCapabilities Capabilities { get; set; } = new();
}

/// <summary>
/// Describes a registered queue subscription.
/// </summary>
/// <example>
/// <code>
/// var subscriptions = await queueBrokerService.GetSubscriptionsAsync(cancellationToken);
/// foreach (var subscription in subscriptions)
/// {
///     Console.WriteLine($"{subscription.MessageType} -> {subscription.HandlerType}");
/// }
/// </code>
/// </example>
public class QueueSubscriptionInfo
{
    /// <summary>
    /// Gets or sets the derived queue name.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the message type name.
    /// </summary>
    public string MessageType { get; set; }

    /// <summary>
    /// Gets or sets the handler type name.
    /// </summary>
    public string HandlerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is paused.
    /// </summary>
    public bool IsQueuePaused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message type is paused.
    /// </summary>
    public bool IsMessageTypePaused { get; set; }
}

/// <summary>
/// Describes a tracked queue message.
/// </summary>
/// <example>
/// <code>
/// var waiting = await queueBrokerService.GetWaitingMessagesAsync(take: 10, cancellationToken);
/// var oldest = waiting.FirstOrDefault();
/// </code>
/// </example>
public class QueueMessageInfo
{
    /// <summary>
    /// Gets or sets the internal message identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the persisted message type token.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the currently registered handler type name.
    /// </summary>
    public string RegisteredHandlerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the archive timestamp.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the queue message status.
    /// </summary>
    public QueueMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of processing attempts performed so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the lease owner.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when processing began for the current attempt.
    /// </summary>
    public DateTimeOffset? ProcessingStartedDate { get; set; }

    /// <summary>
    /// Gets or sets the processed timestamp.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the latest error.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the queue message properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents the stored serialized content of a queue message.
/// </summary>
public class QueueMessageContentInfo
{
    /// <summary>
    /// Gets or sets the queue message primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the persisted message type token.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the serialized queue payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the payload hash.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the archive timestamp.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }
}

/// <summary>
/// Represents aggregate statistics for queue messages.
/// </summary>
public class QueueMessageStats
{
    /// <summary>
    /// Gets or sets the total number of matching messages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of pending messages.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of waiting-for-handler messages.
    /// </summary>
    public int WaitingForHandler { get; set; }

    /// <summary>
    /// Gets or sets the number of currently processing messages.
    /// </summary>
    public int Processing { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded messages.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of failed messages.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of dead-lettered messages.
    /// </summary>
    public int DeadLettered { get; set; }

    /// <summary>
    /// Gets or sets the number of expired messages.
    /// </summary>
    public int Expired { get; set; }

    /// <summary>
    /// Gets or sets the number of archived messages.
    /// </summary>
    public int Archived { get; set; }

    /// <summary>
    /// Gets or sets the number of currently leased messages.
    /// </summary>
    public int Leased { get; set; }

    /// <summary>
    /// Gets or sets the paused queues.
    /// </summary>
    public IReadOnlyCollection<string> PausedQueues { get; set; } = [];

    /// <summary>
    /// Gets or sets the paused message types.
    /// </summary>
    public IReadOnlyCollection<string> PausedTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the currently open circuits.
    /// </summary>
    public IReadOnlyCollection<string> OpenCircuits { get; set; } = [];
}
