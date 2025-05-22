namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;

public interface INotificationService<TMessage> where TMessage : class, INotificationMessage
{
    Task<Result> SendAsync(TMessage message, NotificationSendOptions options, CancellationToken cancellationToken);
    Task<Result> QueueAsync(TMessage message, CancellationToken cancellationToken);
}
