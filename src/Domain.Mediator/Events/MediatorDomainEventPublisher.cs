// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     This class is responsible for publishing domain events using a mediator.
/// </summary>
public partial class MediatorDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<MediatorDomainEventPublisher> logger;

    private readonly IMediator mediator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MediatorDomainEventPublisher" /> class.
    ///     Publishes domain events using the mediator pattern, also logging the events in the process.
    /// </summary>
    public MediatorDomainEventPublisher(ILoggerFactory loggerFactory, IMediator mediator)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        this.logger = loggerFactory?.CreateLogger<MediatorDomainEventPublisher>() ??
            NullLoggerFactory.Instance.CreateLogger<MediatorDomainEventPublisher>();
        this.mediator = mediator;
    }

    /// <summary>
    ///     Sends a domain event using the mediator, logging the event details in the process.
    /// </summary>
    /// <param name="event">The domain event to send.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public async Task Send(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is not null)
        {
            TypedLogger.LogSend(this.logger, Constants.LogKey, @event.EventId, @event.GetType().Name);

            await this.mediator.Publish(@event, cancellationToken).AnyContext();
        }
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
        [LoggerMessage(0,
            LogLevel.Information,
            "{LogKey} publish domain event (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogSend(ILogger logger, string logKey, Guid domainEventId, string domainEventType);
    }
}