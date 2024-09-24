// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Model;

public interface IAggregateEventMediatorRequestSender
{
    Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    Task<bool> SendProjectionEventAsync(object savedEvent, object aggregate);

    Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    Task<bool> SendEventOccuredAsync(object savedEvent, object aggregate);
}