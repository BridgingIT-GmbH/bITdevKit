// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using BridgingIT.DevKit.Domain.EventSourcing.Model;
using MediatR;

public class PublishAggregateEvent<TAggregate> : INotification
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAggregateEvent{TAggregate}"/> class.
    ///
    /// </summary>
    /// <param name="aggregate">Aggregate.</param>
    /// <param name="aggregateEvent">Event, welches den Publish initiiert hat. Kann bei einer kompletten Neuprojektion auch null sein.</param>
    public PublishAggregateEvent(TAggregate aggregate, IAggregateEvent aggregateEvent)
    {
        this.Aggregate = aggregate;
        this.AggregateEvent = aggregateEvent;
    }

    public TAggregate Aggregate { get; private set; }

    /// <summary>
    /// Event, welches den Publish initiiert hat. Kann bei einer kompletten Neuprojektion auch null sein.
    /// </summary>
    public IAggregateEvent AggregateEvent { get; private set; }
}