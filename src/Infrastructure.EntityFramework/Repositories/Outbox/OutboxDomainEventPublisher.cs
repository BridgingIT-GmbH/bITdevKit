// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public partial class ActiveEntityDomainEventOutboxPublishingBehavior<TEntity, TId, TContext> where TEntity : ActiveEntity<TEntity, TId>
    where TContext : DbContext, IOutboxDomainEventContext
{
    /// <summary>
    /// Persists domain events into the Entity Framework outbox instead of publishing them inline.
    /// </summary>
    public class OutboxDomainEventPublisher(ILogger logger, TContext context, IOutboxDomainEventQueue eventQueue = null, OutboxDomainEventOptions options = null) : IDomainEventPublisher
    {
        /// <summary>
        /// Stores a domain event in the outbox and optionally queues it for immediate processing.
        /// </summary>
        /// <param name="event">The domain event to persist.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>The successful outbox persistence result.</returns>
        /// <example>
        /// <code>
        /// var publisher = new ActiveEntityDomainEventOutboxPublishingBehavior&lt;Customer, Guid, AppDbContext&gt;.OutboxDomainEventPublisher(
        ///     logger,
        ///     dbContext,
        ///     eventQueue,
        ///     new OutboxDomainEventOptions { ProcessingMode = OutboxDomainEventProcessMode.Immediate });
        ///
        /// await publisher.Send(new CustomerRegisteredDomainEvent(customerId), cancellationToken);
        /// </code>
        /// </example>
        public async Task<IResult> Send(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            var outboxOptions = options ?? new OutboxDomainEventOptions();
            outboxOptions.Serializer ??= new SystemTextJsonSerializer();

            logger.LogInformation("{LogKey} store domain event to outbox: {EventType} ({EventId})", Constants.LogKey, @event.GetType().Name, @event.EventId);

            var outboxEvent = new OutboxDomainEvent
            {
                EventId = @event.EventId.ToString(),
                Type = @event.GetType().AssemblyQualifiedNameShort(),
                Content = outboxOptions.Serializer.SerializeToString(@event),
                ContentHash = HashHelper.Compute(@event),
                CreatedDate = @event.Timestamp
            };

            if (@event is DomainEventBase de && de.Properties != null)
            {
                outboxEvent.Properties = new Dictionary<string, object>(de.Properties);
            }

            context.OutboxDomainEvents.Add(outboxEvent);

            if (outboxOptions.AutoSave) // save outbox immediately
            {
                await context.SaveChangesAsync<OutboxDomainEvent>(logger, cancellationToken).ConfigureAwait(false);
            }

            if (outboxOptions.ProcessingMode == OutboxDomainEventProcessMode.Immediate)
            {
                eventQueue?.Enqueue(outboxEvent.EventId);
            }

            return Result.Success();
        }
    }
}
