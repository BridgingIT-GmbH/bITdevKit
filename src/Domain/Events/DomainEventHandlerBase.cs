// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     A base class for handling domain events. Implementations of specific domain event handlers
///     should derive from this class and provide logic for handling events of type <typeparamref name="TEvent" />.
/// </summary>
/// <typeparam name="TEvent">The type of the domain event.</typeparam>
public abstract partial class DomainEventHandlerBase<TEvent> : IDomainEventHandler<TEvent>, INotificationHandler<TEvent>
    where TEvent : class, IDomainEvent
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainEventHandlerBase{TEvent}" /> class.
    ///     Abstract base class representing a handler for domain events.
    ///     Includes common logic for handling domain events, such as logging.
    /// </summary>
    protected DomainEventHandlerBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    ///     Provides a logging mechanism for the derived class.
    /// </summary>
    /// <remarks>
    ///     The logger is created using the <see cref="ILoggerFactory" /> provided during the instantiation
    ///     of the derived class. If a <see cref="ILoggerFactory" /> is not provided, a null logger will be used.
    /// </remarks>
    protected ILogger Logger { get; }

    /// <summary>
    ///     Determines if the given domain event can be handled by this handler.
    /// </summary>
    /// <param name="notification">The domain event notification to check.</param>
    /// <returns>True if the event can be handled, otherwise false.</returns>
    public abstract bool CanHandle(TEvent notification);

    /// <summary>
    ///     Handles the given domain event.
    /// </summary>
    /// <param name="event">The domain event to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task Handle(TEvent @event, CancellationToken cancellationToken)
    {
        using (this.Logger.BeginScope(new Dictionary<string, object>
        {
            ["DomainEventId"] = @event.EventId.ToString("N"),
            ["DomainEventType"] = @event.GetType().Name
        }))
        {
            try
            {
                EnsureArg.IsNotNull(@event, nameof(@event));

                if (!this.CanHandle(@event))
                {
                    // TODO: log when not handled?
                    return;
                }

                TypedLogger.LogProcessing(this.Logger, Constants.LogKey, @event.EventId.ToString("N"), @event.GetType().Name);
                var watch = ValueStopwatch.StartNew();

                await this.Process(@event, cancellationToken).AnyContext();

                TypedLogger.LogProcessed(this.Logger, Constants.LogKey, @event.EventId.ToString("N"), @event.GetType().Name, watch.GetElapsedMilliseconds());
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex,
                    "[{LogKey}] event processing error (eventId={DomainEventId}, eventType={DomainEventType}): {ErrorMessage}",
                    Constants.LogKey,
                    @event.EventId,
                    @event.GetType().Name.Split(',')[0],
                    ex.Message);

                throw;
            }
        }
    }

    /// <summary>
    /// Handles a domain event using the supplied publishing options.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public virtual async Task<Result> HandleAsync(TEvent @event, PublishOptions options, CancellationToken cancellationToken) // notifier handler interface
    {
        await this.Process(@event, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    ///     Processes the specified domain event notification with the possibility of cancellation.
    /// </summary>
    /// <param name="event">The domain event notification to process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task Process(TEvent @event, CancellationToken cancellationToken);

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the processing operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="domainEventId">The domain event id used by the operation.</param>
        /// <param name="domainEventType">The domain event type used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] event processing (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string domainEventId, string domainEventType);

        /// <summary>
        /// Writes a log entry for the processed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="domainEventId">The domain event id used by the operation.</param>
        /// <param name="domainEventType">The domain event type used by the operation.</param>
        /// <param name="timeElapsed">The time elapsed used by the operation.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] event processed (eventId={DomainEventId}, eventType={DomainEventType}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string domainEventId, string domainEventType, long timeElapsed);
    }
}
