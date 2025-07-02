namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class OutboxNotificationEmailQueue : IOutboxNotificationEmailQueue
{
    private readonly ILogger<OutboxNotificationEmailQueue> logger;
    private readonly ActionBlock<string> notificationIds;

    public OutboxNotificationEmailQueue(ILoggerFactory loggerFactory, Action<string> action)
    {
        this.logger = loggerFactory?.CreateLogger<OutboxNotificationEmailQueue>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailQueue>();
        this.notificationIds = new ActionBlock<string>(action, new ExecutionDataflowBlockOptions
        {
            CancellationToken = CancellationToken.None,
            MaxDegreeOfParallelism = 1,
            EnsureOrdered = true
        });
    }

    public void Enqueue(string notificationId)
    {
        if (string.IsNullOrEmpty(notificationId))
        {
            this.logger.LogWarning("{LogKey} attempted to enqueue null or empty notification ID", Constants.LogKey);
            return;
        }

        this.logger.LogDebug("{LogKey} notification email queued (id={NotificationId})", Constants.LogKey, notificationId);
        this.notificationIds.Post(notificationId);
    }
}