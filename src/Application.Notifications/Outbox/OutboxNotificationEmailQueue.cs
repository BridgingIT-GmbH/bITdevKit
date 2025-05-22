namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class OutboxNotificationEmailQueue : IOutboxNotificationEmailQueue
{
    private readonly ILogger<OutboxNotificationEmailQueue> logger;
    private readonly ActionBlock<string> messageIds;

    public OutboxNotificationEmailQueue(ILoggerFactory loggerFactory, Action<string> action)
    {
        this.logger = loggerFactory?.CreateLogger<OutboxNotificationEmailQueue>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailQueue>();
        this.messageIds = new ActionBlock<string>(action, new ExecutionDataflowBlockOptions
        {
            CancellationToken = CancellationToken.None,
            MaxDegreeOfParallelism = 1,
            EnsureOrdered = true
        });
    }

    public void Enqueue(string messageId)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            this.logger.LogWarning("{LogKey} Attempted to enqueue null or empty message ID", "NOT");
            return;
        }

        this.logger.LogDebug("{LogKey} Outbox notification email queued (id={MessageId})", "NOT", messageId);
        this.messageIds.Post(messageId);
    }
}