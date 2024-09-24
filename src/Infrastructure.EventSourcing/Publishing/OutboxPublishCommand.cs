// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using Domain.EventSourcing.Model;

public class OutboxPublishCommand<TAggregate, TAggregateEvent>
    where TAggregate : EventSourcingAggregateRoot
    where TAggregateEvent : AggregateEvent
{
    public OutboxPublishCommand(TAggregate aggregate, TAggregateEvent aggregateEvent)
    {
        this.Aggregate = aggregate;
        this.AggregateEvent = aggregateEvent;
    }

    public OutboxPublishCommand() { }

    public TAggregate Aggregate { get; set; }

    public TAggregateEvent AggregateEvent { get; set; }

    public object GetAggregateAsObject()
    {
        return this.Aggregate;
    }

    public object GetAggregateEventAsObject()
    {
        return this.AggregateEvent;
    }
}