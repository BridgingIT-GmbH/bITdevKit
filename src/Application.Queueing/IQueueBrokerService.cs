namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Exposes operational information and control operations for queue brokers.
/// </summary>
/// <example>
/// <code>
/// var stats = await queueBrokerService.GetMessageStatsAsync(cancellationToken: cancellationToken);
/// var failedMessages = await queueBrokerService.GetMessagesAsync(
///     status: QueueMessageStatus.Failed,
///     take: 50,
///     cancellationToken: cancellationToken);
///
/// foreach (var message in failedMessages)
/// {
///     await queueBrokerService.RetryMessageAsync(message.Id, cancellationToken);
/// }
/// </code>
/// </example>
public interface IQueueBrokerService
{
    /// <summary>
    /// Retrieves queue messages with optional operational filters.
    /// </summary>
    /// <param name="status">Optional queue message status filter.</param>
    /// <param name="type">Optional persisted queue message type filter.</param>
    /// <param name="queueName">Optional logical queue name filter.</param>
    /// <param name="messageId">Optional logical message identifier filter.</param>
    /// <param name="lockedBy">Optional lease-owner filter.</param>
    /// <param name="isArchived">Optional archive-state filter. Defaults to <c>false</c>.</param>
    /// <param name="createdAfter">Optional lower creation-date filter.</param>
    /// <param name="createdBefore">Optional upper creation-date filter.</param>
    /// <param name="take">Optional result limit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of queue message summaries.</returns>
    Task<IEnumerable<QueueMessageInfo>> GetMessagesAsync(
        QueueMessageStatus? status = null,
        string type = null,
        string queueName = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single queue message by its primary key.
    /// </summary>
    /// <param name="id">The queue message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The queue message details, or <c>null</c> if not found.</returns>
    Task<QueueMessageInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the stored serialized content for a single queue message.
    /// </summary>
    /// <param name="id">The queue message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored queue message content, or <c>null</c> if not found.</returns>
    Task<QueueMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregate queue message statistics.
    /// </summary>
    /// <param name="startDate">Optional lower processed/created date filter.</param>
    /// <param name="endDate">Optional upper processed/created date filter.</param>
    /// <param name="isArchived">Optional archive-state filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated queue message statistics.</returns>
    Task<QueueMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of broker state.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A runtime summary containing counts, pause state, and broker capabilities.</returns>
    Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active queue subscriptions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered queue message type to handler mappings.</returns>
    Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages that are waiting because no compatible handler is currently registered.
    /// </summary>
    /// <param name="take">An optional maximum number of messages to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The waiting queue messages ordered by creation time.</returns>
    Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a retryable queue message so it can be processed again.
    /// </summary>
    /// <param name="id">The queue message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the current lease for a queue message.
    /// </summary>
    /// <param name="id">The queue message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses processing for the specified logical queue.
    /// </summary>
    /// <param name="queueName">The logical queue name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes processing for the specified logical queue.
    /// </summary>
    /// <param name="queueName">The logical queue name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses processing for the specified queue message type token.
    /// </summary>
    /// <param name="type">The queue message type token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes processing for the specified queue message type token.
    /// </summary>
    /// <param name="type">The queue message type token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the circuit state for the specified queue message type token.
    /// </summary>
    /// <param name="type">The queue message type token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetMessageTypeCircuitAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a terminal queue message as archived.
    /// </summary>
    /// <param name="id">The queue message primary key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes persisted queue messages matching the supplied filters.
    /// </summary>
    /// <param name="olderThan">Optional upper age filter.</param>
    /// <param name="statuses">Optional status filter.</param>
    /// <param name="isArchived">Optional archive-state filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<QueueMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default);
}
