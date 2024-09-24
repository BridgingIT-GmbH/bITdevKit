// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using System.Reflection;
using Application.Commands;
using Application.Commands.EventSourcing;
using Common;
using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Model;
using MediatR;

public class AggregateEventMediatorNotificationSender(IMediator mediator) : IAggregateEventMediatorNotificationSender
{
    private readonly IMediator mediator = mediator;

    /// <summary>
    ///     Sendet das Event <see cref="savedEvent" /> über den Mediator als Notification
    /// </summary>
    /// <exception cref="PublishAggregateEventCouldNotBeConstructedException">
    ///     Das AggregateEvent konnte nicht erzeugt werden,
    ///     da der zugehörige Konstruktor nicht gefunden wurde.
    /// </exception>
    public async Task PublishProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        var genericPublishAggregateEvent =
            CreateGenericPublishAggregateEvent(aggregate, out var genericPublishAggregateEventConstructor);
        if (genericPublishAggregateEventConstructor is not null)
        {
            var @event = genericPublishAggregateEventConstructor.Invoke([aggregate, savedEvent]);
            await this.mediator.Publish(@event).AnyContext();
        }
        else
        {
            throw new PublishAggregateEventCouldNotBeConstructedException("Constructor for " +
                genericPublishAggregateEvent.FullName +
                " not found.");
        }
    }

    public async Task PublishEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        var genericPublishAggregateEvent =
            CreateGenericEventOccured(aggregate, out var genericPublishAggregateEventConstructor);
        if (genericPublishAggregateEventConstructor is not null)
        {
            var @event = genericPublishAggregateEventConstructor.Invoke([aggregate, savedEvent]);
            await this.mediator.Publish(@event).AnyContext();
        }
        else
        {
            throw new PublishAggregateEventCouldNotBeConstructedException("Constructor for " +
                genericPublishAggregateEvent.FullName +
                " not found.");
        }
    }

    public async Task<bool> PublishEventOccuredAsync(object savedEvent, object aggregate)
    {
        var genericPublishAggregateCommand =
            CreateAggregateEventOccuredCommand(aggregate, out var genericPublishAggregateCommandConstructor);
        if (genericPublishAggregateCommandConstructor is not null)
        {
            var @event = genericPublishAggregateCommandConstructor.Invoke([aggregate, savedEvent]);
            return await this.mediator.Send(@event).AnyContext() is CommandResponse<bool> commandResult &&
                commandResult.Cancelled == false &&
                commandResult.Result;
        }

        throw new PublishAggregateEventCommandCouldNotBeConstructedException("Constructor for " +
            genericPublishAggregateCommand.FullName +
            " not found.");
    }

    private static Type CreateGenericPublishAggregateEvent<TAggregate>(TAggregate aggregate, out ConstructorInfo geni)
        where TAggregate : EventSourcingAggregateRoot
    {
        var eventType = typeof(PublishAggregateEvent<>);
        var aggregateType = aggregate?.GetType();
        var gent = eventType.MakeGenericType(aggregateType);
        geni = gent.GetConstructor([aggregateType, typeof(IAggregateEvent)]);
        return gent;
    }

    private static Type CreateGenericEventOccured<TAggregate>(TAggregate aggregate, out ConstructorInfo geni)
        where TAggregate : EventSourcingAggregateRoot
    {
        var eventType = typeof(AggregateEventOccuredCommand<>);
        var aggregateType = aggregate?.GetType();
        var gent = eventType.MakeGenericType(aggregateType);
        geni = gent.GetConstructor([aggregateType, typeof(AggregateEvent)]);
        return gent;
    }

    private static Type CreateAggregateEventOccuredCommand(object aggregate, out ConstructorInfo geni)
    {
        var eventType = typeof(AggregateEventOccuredCommand<>);
        var aggregateType = aggregate?.GetType();
        var gent = eventType.MakeGenericType(aggregateType);
        geni = gent.GetConstructor([aggregateType, typeof(AggregateEvent)]);
        return gent;
    }
}