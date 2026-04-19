namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Serializes immediate outbox processing requests so notification emails are handled in order.
/// </summary>
public class OutboxNotificationEmailQueue : IOutboxNotificationEmailQueue
{
    private readonly ILogger<OutboxNotificationEmailQueue> logger;
    private readonly ActionBlock<string> notificationIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxNotificationEmailQueue" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="action">The action used to process queued identifiers.</param>
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

    /// <summary>
    /// Enqueues the notification email for immediate processing.
    /// </summary>
    /// <param name="notificationId">The notification email identifier.</param>
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
