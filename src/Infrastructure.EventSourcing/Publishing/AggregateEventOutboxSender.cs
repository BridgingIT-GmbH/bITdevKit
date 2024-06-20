// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using System;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.Outbox;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

// ReSharper disable once ClassNeverInstantiated.Global
public class AggregateEventOutboxSender(IOutboxMessageWriterRepository outboxMessageWriterRepository, IEventStoreAggregateEventRegistration eventStoreAggregateEventRegistration,
    IEventStoreAggregateRegistration eventStoreAggregateRegistration) : IAggregateEventOutboxSender
{
    private readonly IOutboxMessageWriterRepository outboxMessageWriterRepository = outboxMessageWriterRepository;
    private readonly IEventStoreAggregateEventRegistration eventStoreAggregateEventRegistration = eventStoreAggregateEventRegistration;
    private readonly IEventStoreAggregateRegistration eventStoreAggregateRegistration = eventStoreAggregateRegistration;

    public async Task WriteToOutboxAsync<TAggregate>(AggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableEventTypeName = this.eventStoreAggregateEventRegistration.GetImmutableName(savedEvent);
        var immutableAggregateTypeName = this.eventStoreAggregateRegistration.GetImmutableName<TAggregate>();
        var msg = new OutboxMessage()
        {
            MessageId = savedEvent.EventId,
            AggregateId = savedEvent.AggregateId,
            Aggregate = JsonConvert.SerializeObject(aggregate), // TODO: use ISerializer
            AggregateEvent = JsonConvert.SerializeObject(savedEvent), // TODO: use ISerializer
            AggregateType = immutableAggregateTypeName,
            EventType = immutableEventTypeName,
            IsProcessed = false,
            RetryAttempt = 0,
            TimeStamp = DateTime.Now
        };
        await this.outboxMessageWriterRepository.InsertAsync(msg).AnyContext();
    }
}