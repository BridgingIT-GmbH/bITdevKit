﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Projection;

using EventSourcing.AggregatePublish;
using MediatR;

public sealed class PersonCreatedProjection : INotificationHandler<PublishAggregateEvent<Person>>
{
    Task INotificationHandler<PublishAggregateEvent<Person>>.Handle(
        PublishAggregateEvent<Person> notification,
        CancellationToken cancellationToken)
    {
        notification.ShouldNotBeNull();
        notification.Aggregate.Id.ShouldNotBe(Guid.Empty);
        notification.AggregateEvent.ShouldNotBeNull();

        return Task.FromResult(notification.Aggregate);
    }
}