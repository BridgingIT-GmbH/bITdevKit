// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Infrastructure.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;
using MediatR;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventStore(this IServiceCollection services, EventStorePublishingModes eventStorePublishingModes)
    {
        return services.AddTransient<ISerializer, JsonNetSerializer>()
            .AddSingleton<IEventTypeSelector, EventTypeSelector>()
            .AddTransient<IAggregateTypeSelector, AggregateTypeSelector>()
            .AddTransient<IPublishAggregateEventSender>(sp =>
                {
                    // ReSharper disable once ConvertToLambdaExpression
                    return new PublishAggregateEventSender(sp.GetRequiredService<IMediator>(),
                        eventStorePublishingModes, sp.GetRequiredService<IAggregateEventMediatorRequestSender>(),
                        sp.GetRequiredService<IAggregateEventMediatorNotificationSender>(),
                        sp.GetRequiredService<IAggregateEventOutboxSender>());
                })
            .AddTransient<IAggregateEventMediatorRequestSender, AggregateEventMediatorRequestSender>()
            .AddTransient<IAggregateEventMediatorNotificationSender, AggregateEventMediatorNotificationSender>()
            .AddTransient<IAggregateEventOutboxSender, AggregateEventOutboxSender>()
            .AddTransient<IAggregateEventOutboxReceiver, AggregateEventOutboxReceiver>()
            .AddTransient<IRegistrationForEventStoreAggregatesAndEvents, RegistrationForEventStoreAggregatesAndEvents>()
            .AddSingleton<IEventStoreAggregateRegistration, EventStoreAggregateRegistration>()
            .AddSingleton<IEventStoreAggregateEventRegistration, EventStoreAggregateEventRegistration>();
    }

    [Obsolete("Please use overload")]
    public static IServiceCollection RegisterAggregateAndProjectionRequestForEventStore<TAggregate>(
        this IServiceCollection services, EventStorePublishingModes projectionRequestPublishingModes)
        where TAggregate : EventSourcingAggregateRoot
    {
        return RegisterAggregateAndProjectionRequestForEventStore<TAggregate>(services, projectionRequestPublishingModes,
            false);
    }

    public static IServiceCollection RegisterAggregateAndProjectionRequestForEventStore<TAggregate>(this IServiceCollection services,
        EventStorePublishingModes projectionRequestPublishingModes, bool isSnapshotEnabled)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((projectionRequestPublishingModes & EventStorePublishingModes.AddToOutbox) !=
            EventStorePublishingModes.None)
        {
            throw new InvalidOperationException($"The Flag {EventStorePublishingModes.AddToOutbox} is not allowed for {nameof(projectionRequestPublishingModes)}");
        }

        return services
            .AddTransient<IEventStore<TAggregate>, EventStore<TAggregate>>()
            .AddTransient<IEventStoreOptions<TAggregate>>((_) => new EventStoreOptions<TAggregate>() { IsSnapshotEnabled = isSnapshotEnabled })
            .AddTransient<IProjectionRequester<TAggregate>, ProjectionRequester<TAggregate>>(
                sp => new ProjectionRequester<TAggregate>(
                    sp.GetRequiredService<IEventStore<TAggregate>>(),
                    new PublishAggregateEventSender(
                        sp.GetRequiredService<IMediator>(),
                        projectionRequestPublishingModes, sp.GetRequiredService<IAggregateEventMediatorRequestSender>(),
                        sp.GetRequiredService<IAggregateEventMediatorNotificationSender>(),
                        sp.GetRequiredService<IAggregateEventOutboxSender>()), sp.GetRequiredService<Logging.ILoggerFactory>()));
    }
}