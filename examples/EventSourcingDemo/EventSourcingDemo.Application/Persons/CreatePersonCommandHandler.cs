// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Domain.Repositories;
using Domain.Model;
using Domain.Model.Events;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class CreatePersonCommandHandler : CommandHandlerBase<CreatePersonCommand, PersonOverviewViewModel>
{
    private readonly IEventStore<Person> eventStore;
    private readonly IEntityMapper mapper;

    public CreatePersonCommandHandler(ILoggerFactory loggerFactory, IEventStore<Person> eventStore, IEntityMapper mapper)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(eventStore, nameof(eventStore));
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.eventStore = eventStore;
        this.mapper = mapper;
    }

    public override async Task<CommandResponse<PersonOverviewViewModel>> Process(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(request, nameof(request));
        EnsureArg.IsNotNull(request.Model, nameof(request.Model));

        // tag::DemoPersonCreatedEvent[]
        var personCreated = new PersonCreatedEvent(request.Model.Lastname, request.Model.Firstname);  // <1>
        var person = new Person(personCreated);  // <2>
        await this.eventStore.SaveEventsAsync(person, cancellationToken).AnyContext();  // <3>
        // end::DemoPersonCreatedEvent[]

        return new CommandResponse<PersonOverviewViewModel>()
        {
            Result = this.mapper.Map<PersonOverviewViewModel>(person)
        };
    }
}