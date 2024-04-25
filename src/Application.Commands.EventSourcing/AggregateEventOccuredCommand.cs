// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.EventSourcing;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public class AggregateEventOccuredCommand<TAggregate> : CommandRequestBase<bool>
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateEventOccuredCommand{TAggregate}"/> class.
    ///
    /// </summary>
    /// <param name="aggregate">Aggregate.</param>
    /// <param name="aggregateEvent">Event, welches den Publish initiiert hat. Kann bei einer kompletten Neuprojektion auch null sein.</param>
    public AggregateEventOccuredCommand(TAggregate aggregate, AggregateEvent aggregateEvent)
    {
        this.Aggregate = aggregate;
        this.AggregateEvent = aggregateEvent;
    }

    public TAggregate Aggregate { get; private set; }

    /// <summary>
    /// Event
    /// </summary>
    public AggregateEvent AggregateEvent { get; private set; }
}