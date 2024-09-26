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

public class AggregateEventMediatorRequestSender(IMediator mediator) : IAggregateEventMediatorRequestSender
{
    private readonly IMediator mediator = mediator;

    /// <summary>
    ///     Sendet das Event <see cref="savedEvent" /> über den Mediator als Request.
    /// </summary>
    /// <exception cref="PublishAggregateEventCouldNotBeConstructedException">
    ///     Das AggregateEvent konnte nicht erzeugt werden,
    ///     da der zugehörige Konstruktor nicht gefunden wurde.
    /// </exception>
    public async Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        var genericPublishAggregateCommand =
            CreateProjectionCommand(aggregate, out var genericPublishAggregateCommandConstructor);
        if (genericPublishAggregateCommandConstructor is not null)
        {
            var @event = genericPublishAggregateCommandConstructor.Invoke([aggregate, savedEvent]);
            var commandResult = await this.mediator.Send(@event).AnyContext() as CommandResponse<bool>;
        }
        else
        {
            throw new PublishAggregateEventCommandCouldNotBeConstructedException("Constructor for " +
                genericPublishAggregateCommand.FullName +
                " not found.");
        }
    }

    public async Task<bool> SendProjectionEventAsync(object savedEvent, object aggregate)
    {
        var genericPublishAggregateCommand =
            CreateProjectionCommand(aggregate, out var genericPublishAggregateCommandConstructor);
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

    public async Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        var genericPublishAggregateCommand =
            CreateAggregateEventOccuredCommand(aggregate, out var genericPublishAggregateCommandConstructor);
        if (genericPublishAggregateCommandConstructor is not null)
        {
            var @event = genericPublishAggregateCommandConstructor.Invoke([aggregate, savedEvent]);
            var commandResult = await this.mediator.Send(@event).AnyContext() as CommandResponse<bool>;
        }
        else
        {
            throw new PublishAggregateEventCommandCouldNotBeConstructedException("Constructor for " +
                genericPublishAggregateCommand.FullName +
                " not found.");
        }
    }

    public async Task<bool> SendEventOccuredAsync(object savedEvent, object aggregate)
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

    private static Type CreateProjectionCommand<TAggregate>(TAggregate aggregate, out ConstructorInfo geni)
        where TAggregate : EventSourcingAggregateRoot
    {
        var eventType = typeof(AggregateEventProjectionCommand<>);
        var aggregateType = aggregate?.GetType();
        var gent = eventType.MakeGenericType(aggregateType);
        geni = gent.GetConstructor([aggregateType, typeof(AggregateEvent)]);

        return gent;
    }

    private static Type CreateProjectionCommand(object aggregate, out ConstructorInfo geni)
    {
        var eventType = typeof(AggregateEventProjectionCommand<>);
        var aggregateType = aggregate?.GetType();
        var gent = eventType.MakeGenericType(aggregateType);
        geni = gent.GetConstructor([aggregateType, typeof(AggregateEvent)]);

        return gent;
    }

    private static Type CreateAggregateEventOccuredCommand<TAggregate>(TAggregate aggregate, out ConstructorInfo geni)
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