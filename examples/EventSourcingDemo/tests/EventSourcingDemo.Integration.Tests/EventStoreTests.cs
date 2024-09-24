// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.IntegrationTests;

using DevKit.Domain.EventSourcing.Store;
using Domain.Model;
using Domain.Model.Events;
using Domain.Repositories;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class EventStoreTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : EventstoreTestBase(output, fixture)
{
    [Fact]
    public async Task CreateAndChangePersonAggregate()
    {
        var personStore = this.ServiceProvider.GetService<IEventStore<Person>>();
        var max = new Person(new PersonCreatedEvent("Max", "Musterman"));
        await personStore.SaveEventsAsync(max, CancellationToken.None).AnyContext();
        var rep = this.ServiceProvider.GetService<IPersonOverviewRepository>();
        var personList = (await rep.FindAllAsync().AnyContext()).ToList();
        personList.ShouldNotBeNull();
        personList.Count().ShouldBe(1);
        var p = personList.First();
        p.Firstname.ShouldBe(max.Firstname);
        p.Lastname.ShouldBe(max.Lastname);

        var loadedPerson = await personStore.GetAsync(max.Id, CancellationToken.None).AnyContext();
        loadedPerson.Firstname.ShouldBe(max.Firstname);
        loadedPerson.Lastname.ShouldBe(max.Lastname);

        loadedPerson.ChangeSurname("Mustermann");
        loadedPerson.Lastname.ShouldBe("Mustermann");
        await personStore.SaveEventsAsync(loadedPerson, CancellationToken.None);

        var personList2 = (await rep.FindAllAsync().AnyContext()).ToList();
        var p2 = personList2.First();
        p2.Lastname.ShouldBe(loadedPerson.Lastname);
        p2.Firstname.ShouldBe(loadedPerson.Firstname);

        var events = await personStore.GetEventsAsync(max.Id, CancellationToken.None);
        events.Length.ShouldBe(2);

        var loadedPersonWithReplay = await personStore.GetAsync(max.Id, true, CancellationToken.None);
        loadedPersonWithReplay.Lastname.ShouldBe(loadedPerson.Lastname);
        loadedPersonWithReplay.Firstname.ShouldBe(loadedPerson.Firstname);
    }
}