namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Signals the outbox worker that a notification email should be processed as soon as possible.
/// </summary>
public interface IOutboxNotificationEmailQueue
{
    /// <summary>
    /// Enqueues the specified notification email identifier for immediate outbox processing.
    /// </summary>
    /// <param name="messageId">The notification email identifier.</param>
    void Enqueue(string messageId);
}
