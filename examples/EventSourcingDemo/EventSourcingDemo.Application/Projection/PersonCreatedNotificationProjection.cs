// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Projection;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.Repositories;
using Domain.Model;
using Domain.Model.Events;
using Domain.Repositories;
using MediatR;

public sealed class PersonCreatedNotificationProjection : INotificationHandler<PublishAggregateEvent<Person>> // <1>
{
    private readonly IPersonOverviewRepository personOverviewRepository;
    private readonly IEntityMapper mapper;

    public PersonCreatedNotificationProjection(IPersonOverviewRepository personOverviewRepository, IEntityMapper mapper) // <2>
    {
        this.personOverviewRepository = personOverviewRepository;
        this.mapper = mapper;
    }

    async Task INotificationHandler<PublishAggregateEvent<Person>>.Handle(
        PublishAggregateEvent<Person> notification, CancellationToken cancellationToken) // <3>
    {
        System.Diagnostics.Debug.WriteLine("Do projection of " + notification.Aggregate.Id);
        if (!(notification.AggregateEvent is UserDeactivatedEvent)) // <4>
        {
            var pov = this.mapper.Map<PersonOverview>(notification.Aggregate);
            await this.personOverviewRepository.UpsertAsync(pov, cancellationToken).AnyContext();
        }
        else
        {
            await this.personOverviewRepository.DeleteAsync(notification.Aggregate.Id, cancellationToken).AnyContext();
        }
    }
}