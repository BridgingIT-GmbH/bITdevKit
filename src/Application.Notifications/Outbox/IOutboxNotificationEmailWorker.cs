namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Processes persisted notification emails from the outbox store.
/// </summary>
public interface IOutboxNotificationEmailWorker
{
    /// <summary>
    /// Processes a specific notification email or the next available batch.
    /// </summary>
    /// <param name="messageId">The optional notification email identifier to prioritize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges persisted notification emails from the outbox.
    /// </summary>
    /// <param name="processedOnly"><see langword="true" /> to purge only processed rows; otherwise all rows are purged.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default);
}
