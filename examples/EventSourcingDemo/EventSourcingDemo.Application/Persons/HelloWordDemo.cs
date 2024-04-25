// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Persons;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using Domain.Model;
using Domain.Model.Events;

public class HelloWordDemo
{
    private readonly IEventStore<Person> eventStore;

    public HelloWordDemo(IEventStore<Person> eventStore)
    {
        EnsureArg.IsNotNull(eventStore, nameof(eventStore));

        this.eventStore = eventStore;
    }

    public async Task Demo1(CancellationToken token)
    {
        // tag::EventStoreDemo1[]
        var createEvent = new PersonCreatedEvent("MusterMann", "Max"); // <1>
        var person = new Person(createEvent); // <2>
        await this.eventStore.SaveEventsAsync(person, token).AnyContext(); // <3>
        // ...
        var person2 = await this.eventStore.GetAsync(person.Id, token).AnyContext();  // <4>
        person2.ChangeSurname("Mustermann"); // <5>
        await this.eventStore.SaveEventsAsync(person2, token).AnyContext(); // <6>
        // end::EventStoreDemo1[]
    }
}