// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

using System.Diagnostics;
using Common;
using DevKit.Domain.EventSourcing.Store;
using DevKit.Domain.Repositories;
using DevKit.Domain.Specifications;
using Domain.Model;
using Domain.Repositories;
using MediatR;

public class PersonService : IPersonService
{
    private readonly IMediator mediator;
    private readonly IEventStore<Person> eventStore;
    private readonly IPersonOverviewRepository personRepository;
    private readonly IEntityMapper mapper;

    public PersonService(
        IMediator mediator,
        IEventStore<Person> eventStore,
        IPersonOverviewRepository personRepository,
        IEntityMapper mapper)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));
        EnsureArg.IsNotNull(eventStore, nameof(eventStore));
        EnsureArg.IsNotNull(personRepository, nameof(personRepository));
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.mediator = mediator;
        this.eventStore = eventStore;
        this.personRepository = personRepository;
        this.mapper = mapper;
    }

    public async Task<PersonOverviewViewModel> CreatePersonAsync(CreatePersonViewModel model)
    {
        EnsureArg.IsNotNull(model, nameof(model));

        var command = new CreatePersonCommand { Model = model };
        var response = await this.mediator.Send(command, CancellationToken.None).AnyContext();
        if (!response.Cancelled)
        {
            return response.Result;
        }

        return null;
    }

    public async Task<Person> ReplayPersonAsync(Guid id)
    {
        return await this.eventStore.GetAsync(id, CancellationToken.None).AnyContext();
    }

    public async Task<PersonOverviewViewModel> ChangeSurnameAsync(
        ChangeSurnameViewModel model,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(model, nameof(model));

        // tag::ChangeSurnameExample[]
        var person = await this.eventStore.GetAsync(model.Id, cancellationToken).AnyContext(); // <1>
        person.ChangeSurname(model.Lastname); // <2>
        await this.eventStore.SaveEventsAsync(person, false, cancellationToken).AnyContext(); // <3>
        // end::ChangeSurnameExample[]
        return this.mapper.Map<PersonOverviewViewModel>(person);
    }

    public async Task<IEnumerable<PersonOverviewViewModel>> GetAllPersonsAsync()
    {
        var query = await this.personRepository.FindAllAsync(new FindOptions<PersonOverview> { NoTracking = true })
            .AnyContext();
        return query.Select(p => new PersonOverviewViewModel
        {
            Firstname = p.Firstname, Lastname = p.Lastname, Id = p.Id
        });
    }

    public async Task<IEnumerable<PersonOverviewViewModel>> GetAllPersonsAsync(
        string firstname,
        string lastname,
        int skip,
        int take)
    {
        firstname ??= string.Empty;
        var specFirstname
            = new Specification<PersonOverview>(p =>
                p.Firstname.StartsWith(firstname));
        var specLastname
            = new Specification<PersonOverview>(p =>
                p.Lastname.StartsWith(lastname));
        var specs = new List<ISpecification<PersonOverview>> { specFirstname, specLastname };
        var count = await this.personRepository.CountAsync(specs).AnyContext();
        Debug.WriteLine(count);
        var orderOption = new OrderOption<PersonOverview>(p => p.Firstname);
        var persons = await this.personRepository
            .FindAllAsync(specs, new FindOptions<PersonOverview>(skip, take, orderOption))
            .AnyContext();
        return persons.ToList()
            .Select(p => new PersonOverviewViewModel { Firstname = p.Firstname, Lastname = p.Lastname, Id = p.Id });
    }

    public async Task DeactivateAsync(Guid id)
    {
        var person = await this.eventStore.GetAsync(id, CancellationToken.None).AnyContext();
        person.DeactivateUser();
        await this.eventStore.SaveEventsAsync(person, CancellationToken.None).AnyContext();
    }
}