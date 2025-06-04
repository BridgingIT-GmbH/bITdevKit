namespace BridgingIT.DevKit.Application.Notifications;

public interface IOutboxNotificationEmailWorker
{
    Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default);

    Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default);
}
