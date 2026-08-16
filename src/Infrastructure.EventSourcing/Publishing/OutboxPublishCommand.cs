// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using Domain.EventSourcing.Model;

/// <summary>
/// Represents outbox publish command.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <typeparam name="TAggregateEvent">The aggregate event type.</typeparam>
public class OutboxPublishCommand<TAggregate, TAggregateEvent>
    where TAggregate : EventSourcingAggregateRoot
    where TAggregateEvent : AggregateEvent
{
    /// <summary>
    /// Initializes a new instance of the <c>OutboxPublishCommand</c> class.
    /// </summary>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="aggregateEvent">The aggregate event used by the operation.</param>
    public OutboxPublishCommand(TAggregate aggregate, TAggregateEvent aggregateEvent)
    {
        this.Aggregate = aggregate;
        this.AggregateEvent = aggregateEvent;
    }

    /// <summary>
    /// Initializes a new instance of the <c>OutboxPublishCommand</c> class.
    /// </summary>
    public OutboxPublishCommand() { }

    /// <summary>
    /// Gets or sets the aggregate.
    /// </summary>
    public TAggregate Aggregate { get; set; }

    /// <summary>
    /// Gets or sets the aggregate event.
    /// </summary>
    public TAggregateEvent AggregateEvent { get; set; }

    /// <summary>
    /// Gets aggregate as object.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public object GetAggregateAsObject()
    {
        return this.Aggregate;
    }

    /// <summary>
    /// Gets aggregate event as object.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public object GetAggregateEventAsObject()
    {
        return this.AggregateEvent;
    }
}
