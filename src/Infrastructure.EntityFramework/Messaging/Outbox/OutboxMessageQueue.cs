// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Threading.Tasks.Dataflow;

public partial class OutboxMessageQueue : IOutboxMessageQueue
{
    private readonly ActionBlock<string> messageIds;

    public OutboxMessageQueue(ILoggerFactory loggerFactory, Action<string> action = null)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());

        if (action is not null)
        {
            this.messageIds = new ActionBlock<string>(action,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = CancellationToken.None, MaxDegreeOfParallelism = 1, EnsureOrdered = true
                });
        }
    }

    //public OutboxMessageQueue(ILoggerFactory loggerFactory, IOutboxMessageWorker worker)
    //{
    //    this.Logger = loggerFactory?.CreateLogger(this.GetType()) ?? NullLoggerFactory.Instance.CreateLogger(this.GetType());

    //    this.messageIds = new ActionBlock<string>(messageId => // dequeued
    //    {
    //        TypedLogger.LogDequeued(this.Logger, "MSG", messageId);

    //        worker?.ProcessAsync(messageId);
    //    }, new ExecutionDataflowBlockOptions
    //    {
    //        CancellationToken = CancellationToken.None,
    //        MaxDegreeOfParallelism = 1,
    //        EnsureOrdered = true
    //    });
    //}

    public ILogger Logger { get; }

    public virtual void Enqueue(string messageId)
    {
        TypedLogger.LogQueue(this.Logger, "MSG", messageId);

        this.messageIds.Post(messageId);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} outbox message queued (id={MessageId})")]
        public static partial void LogQueue(ILogger logger, string logKey, string messageId);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} outbox message dequeued (id={MessageId})")]
        public static partial void LogDequeued(ILogger logger, string logKey, string messageId);
    }
}