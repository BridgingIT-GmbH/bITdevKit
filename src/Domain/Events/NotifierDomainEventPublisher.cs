// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Represents a domain event publisher capable of sending domain events as notifications.
/// </summary>
public partial class NotifierDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<NotifierDomainEventPublisher> logger;

    private readonly INotifier notifier;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotifierDomainEventPublisher" /> class.
    ///     Publishes domain events using the mediator pattern, also logging the events in the process.
    /// </summary>
    public NotifierDomainEventPublisher(ILoggerFactory loggerFactory, INotifier notifier)
    {
        EnsureArg.IsNotNull(notifier, nameof(notifier));

        this.logger = loggerFactory?.CreateLogger<NotifierDomainEventPublisher>() ??
            NullLoggerFactory.Instance.CreateLogger<NotifierDomainEventPublisher>();
        this.notifier = notifier;
    }

    /// <summary>
    ///     Sends a domain event using the mediator, logging the event details in the process.
    /// </summary>
    /// <param name="event">The domain event to send.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public async Task<IResult> Send(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is not null)
        {
            TypedLogger.LogSend(this.logger, Constants.LogKey, @event.EventId.ToString("N"), @event.GetType().Name);

            await this.notifier.PublishDynamicAsync(@event, new PublishOptions(), cancellationToken).AnyContext();
        }

        return Result.Success();
    }

    /// <summary>
    ///     Provides logging functionality for publishing domain events.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        ///     Logs the sending of a domain event with provided parameters.
        /// </summary>
        /// <param name="logger">The logger instance used for logging.</param>
        /// <param name="logKey">A key that indicates the logging domain.</param>
        /// <param name="domainEventId">The unique identifier of the domain event.</param>
        /// <param name="domainEventType">The type of the domain event.</param>
        [LoggerMessage(0, LogLevel.Information, "{LogKey} publish domain event (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogSend(ILogger logger, string logKey, string domainEventId, string domainEventType);
    }
}