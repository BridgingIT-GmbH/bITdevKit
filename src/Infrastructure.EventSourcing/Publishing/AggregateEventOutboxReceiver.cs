// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Domain.Outbox;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

// ReSharper disable once ClassNeverInstantiated.Global
public class AggregateEventOutboxReceiver : IAggregateEventOutboxReceiver
{
    private readonly IEventStoreAggregateEventRegistration eventStoreAggregateEventRegistration;
    private readonly IEventStoreAggregateRegistration eventStoreAggregateRegistration;
    private readonly IAggregateEventMediatorRequestSender aggregateEventMediatorRequestSender;
    private readonly IAggregateEventMediatorNotificationSender aggregateEventMediatorNotificationSender;
    private readonly IEventTypeSelector eventTypeSelector;
    private readonly IAggregateTypeSelector aggregateTypeSelector;

    public AggregateEventOutboxReceiver(
        IEventStoreAggregateEventRegistration eventStoreAggregateEventRegistration,
        IEventStoreAggregateRegistration eventStoreAggregateRegistration,
        IAggregateEventMediatorRequestSender aggregateEventMediatorRequestSender,
        IAggregateEventMediatorNotificationSender aggregateEventMediatorNotificationSender,
        IEventTypeSelector eventTypeSelector,
        IAggregateTypeSelector aggregateTypeSelector)
    {
        this.eventStoreAggregateEventRegistration = eventStoreAggregateEventRegistration;
        this.eventStoreAggregateRegistration = eventStoreAggregateRegistration;
        this.aggregateEventMediatorRequestSender = aggregateEventMediatorRequestSender;
        this.aggregateEventMediatorNotificationSender = aggregateEventMediatorNotificationSender;
        this.eventTypeSelector = eventTypeSelector;
        this.aggregateTypeSelector = aggregateTypeSelector;
    }

    public async Task<(bool projectionSended, bool eventOccuredSended, bool eventOccuredNotified)> ReceiveAndPublishAsync(OutboxMessage message)
    {
        var settings = new JsonSerializerSettings // TODO: use ISerializer
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            ContractResolver = new PrivateSetterContractResolver()
        };
        var (aggregate, aggregateEvent) = this.Deserialize(message, settings);

        var resultProjectionSend = await this.aggregateEventMediatorRequestSender
            .SendProjectionEventAsync(aggregateEvent, aggregate).AnyContext();

        var resultEventOccured = await this.aggregateEventMediatorRequestSender.SendEventOccuredAsync(aggregateEvent, aggregate)
                .AnyContext();

        var resultEventOccuredNotified = await this.aggregateEventMediatorNotificationSender
                .PublishEventOccuredAsync(aggregateEvent, aggregate).AnyContext();
        return (projectionSended: resultProjectionSend, eventOccuredSended: resultEventOccured, eventOccuredNotified: resultEventOccuredNotified);
    }

    private static bool CheckParameters(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 2)
        {
            var last = parameters.Last();
            if (last.ParameterType == typeof(JsonSerializerSettings))
            {
                return true;
            }
        }

        return false;
    }

    private (object aggregate, object aggregateEvent) Deserialize(OutboxMessage message,
        JsonSerializerSettings settings)
    {
        // TODO: use ISerializer?
        var methods = typeof(JsonConvert).GetMethods()
            .Where(p => p.IsGenericMethod && p.Name == "DeserializeObject" && CheckParameters(p)).ToArray();
        var aggregateTypeName = this.eventStoreAggregateRegistration.GetTypeOnImmutableName(message.AggregateType);
        var aggregateEventTypeName =
            this.eventStoreAggregateEventRegistration.GetTypeOnImmutableName(message.EventType);
        var aggregateType = this.aggregateTypeSelector.Find(aggregateTypeName);
        var aggregateEventType = this.eventTypeSelector.FindType(aggregateEventTypeName);
        var method = methods.First();
        var genericAggregate = method.MakeGenericMethod(aggregateType);
        var aggregate = genericAggregate.Invoke(null, new object[] { message.Aggregate, settings });

        var genericAggregateEvent = method.MakeGenericMethod(aggregateEventType);
        var aggregateEvent = genericAggregateEvent.Invoke(null, new object[] { message.AggregateEvent, settings });
        return (aggregate, aggregateEvent);
    }
}