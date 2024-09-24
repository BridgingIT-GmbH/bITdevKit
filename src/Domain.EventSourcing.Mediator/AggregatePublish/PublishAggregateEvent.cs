// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using MediatR;
using Model;

/// <summary>
///     Initializes a new instance of the <see cref="PublishAggregateEvent{TAggregate}" /> class.
/// </summary>
/// <param name="aggregate">Aggregate.</param>
/// <param name="aggregateEvent">
///     Event, welches den Publish initiiert hat. Kann bei einer kompletten Neuprojektion auch
///     null sein.
/// </param>
public class PublishAggregateEvent<TAggregate>(TAggregate aggregate, IAggregateEvent aggregateEvent) : INotification
    where TAggregate : EventSourcingAggregateRoot
{
    public TAggregate Aggregate { get; private set; } = aggregate;

    /// <summary>
    ///     Event, welches den Publish initiiert hat. Kann bei einer kompletten Neuprojektion auch null sein.
    /// </summary>
    public IAggregateEvent AggregateEvent { get; private set; } = aggregateEvent;
}