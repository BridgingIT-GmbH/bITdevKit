// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Threading.Tasks.Dataflow;

/// <summary>
/// Provides an in-process queue for immediate outbox domain event dispatch requests.
/// </summary>
public partial class OutboxDomainEventQueue : IOutboxDomainEventQueue
{
    private readonly ActionBlock<string> eventIds;

    /// <summary>
    /// Initializes a new queue instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used for diagnostics.</param>
    /// <param name="action">The action invoked for each queued event identifier.</param>
    public OutboxDomainEventQueue(ILoggerFactory loggerFactory, Action<string> action = null)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());

        if (action is not null)
        {
            this.eventIds = new ActionBlock<string>(action,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = CancellationToken.None,
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true
                });
        }
    }

    //public OutboxDomainEventQueue(ILoggerFactory loggerFactory, IOutboxDomainEventWorker worker)
    //{
    //    this.Logger = loggerFactory?.CreateLogger(this.GetType()) ?? NullLoggerFactory.Instance.CreateLogger(this.GetType());

    //    this.eventIds = new ActionBlock<string>(eventId => // dequeued
    //    {
    //        TypedLogger.LogDequeued(this.Logger, "MSG", eventId);

    //        worker?.ProcessAsync(eventId);
    //    }, new ExecutionDataflowBlockOptions
    //    {
    //        CancellationToken = CancellationToken.None,
    //        MaxDegreeOfParallelism = 1,
    //        EnsureOrdered = true
    //    });
    //}

    /// <summary>
    /// Gets the logger used by the queue implementation.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Enqueues a persisted domain event identifier for immediate processing.
    /// </summary>
    /// <param name="eventId">The persisted domain event identifier.</param>
    /// <example>
    /// <code>
    /// eventQueue.Enqueue(domainEvent.EventId.ToString());
    /// </code>
    /// </example>
    public virtual void Enqueue(string eventId)
    {
        TypedLogger.LogQueue(this.Logger, "DOM", eventId);

        this.eventIds.Post(eventId);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} outbox domain event queued (id={EventId})")]
        public static partial void LogQueue(ILogger logger, string logKey, string eventId);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} outbox domain event dequeued (id={EventId})")]
        public static partial void LogDequeued(ILogger logger, string logKey, string eventId);
    }
}
