// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;

using Events;
using EventSourcing.Model;

public class Order : EventSourcingAggregateRoot
{
    public Order()
        : base(new OrderCreatedEvent()) { }

    public Order(Guid id, IEnumerable<IAggregateEvent> events)
        : base(id, events) { }

    private void Apply(OrderCreatedEvent @event)
    {
        // TODO: do something with event
    }
}