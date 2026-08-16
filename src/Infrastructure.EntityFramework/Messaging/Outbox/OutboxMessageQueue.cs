// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Threading.Tasks.Dataflow;

/// <summary>
/// Represents outbox message queue.
/// </summary>
public partial class OutboxMessageQueue : IOutboxMessageQueue
{
    private readonly ActionBlock<string> messageIds;

    /// <summary>
    /// Initializes a new instance of the <c>OutboxMessageQueue</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="action">The action to invoke.</param>
    public OutboxMessageQueue(ILoggerFactory loggerFactory, Action<string> action = null)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());

        if (action is not null)
        {
            this.messageIds = new ActionBlock<string>(action,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = CancellationToken.None,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true
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

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    /// <param name="messageId">The message id used by the operation.</param>
    public virtual void Enqueue(string messageId)
    {
        TypedLogger.LogQueue(this.Logger, "MSG", messageId);

        this.messageIds.Post(messageId);
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the queue operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="messageId">The message id used by the operation.</param>
        [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] outbox message queued (id={MessageId})")]
        public static partial void LogQueue(ILogger logger, string logKey, string messageId);

        /// <summary>
        /// Writes a log entry for the dequeued operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="messageId">The message id used by the operation.</param>
        [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] outbox message dequeued (id={MessageId})")]
        public static partial void LogDequeued(ILogger logger, string logKey, string messageId);
    }
}
