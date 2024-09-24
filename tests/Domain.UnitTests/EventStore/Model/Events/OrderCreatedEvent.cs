// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;

using EventSourcing.Model;

public class OrderCreatedEvent : AggregateCreatedEvent<Order>
{
    public OrderCreatedEvent()
        : base(Guid.NewGuid()) { }

    public OrderCreatedEvent(Guid id)
        : base(id) { }
}