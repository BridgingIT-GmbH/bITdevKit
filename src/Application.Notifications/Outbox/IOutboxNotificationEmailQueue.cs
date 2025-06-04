namespace BridgingIT.DevKit.Application.Notifications;

public interface IOutboxNotificationEmailQueue
{
    void Enqueue(string messageId);
}
