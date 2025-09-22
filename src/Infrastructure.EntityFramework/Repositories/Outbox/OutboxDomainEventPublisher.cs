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
    public class OutboxDomainEventPublisher(ILogger logger, TContext context, IOutboxDomainEventQueue eventQueue = null, OutboxDomainEventOptions options = null) : IDomainEventPublisher
    {
        public async Task<IResult> Send(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("{LogKey} store domain event to outbox: {EventType} ({EventId})", Constants.LogKey, @event.GetType().Name, @event.EventId);

            var serializer = options?.Serializer ?? new SystemTextJsonSerializer();
            var outboxEvent = new OutboxDomainEvent
            {
                EventId = @event.EventId.ToString(),
                Type = @event.GetType().AssemblyQualifiedNameShort(),
                Content = serializer.SerializeToString(@event),
                ContentHash = HashHelper.Compute(@event),
                CreatedDate = @event.Timestamp
            };

            if (@event is DomainEventBase de && de.Properties != null)
            {
                outboxEvent.Properties = new Dictionary<string, object>(de.Properties);
            }

            context.OutboxDomainEvents.Add(outboxEvent);

            if (options.AutoSave) // save outbox immediately
            {
                await context.SaveChangesAsync<OutboxDomainEvent>(logger, cancellationToken).ConfigureAwait(false);
            }

            if (options.ProcessingMode == OutboxDomainEventProcessMode.Immediate)
            {
                eventQueue?.Enqueue(outboxEvent.EventId);
            }

            return Result.Success();
        }
    }
}