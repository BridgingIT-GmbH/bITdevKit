// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model.Events;
using BridgingIT.DevKit.Infrastructure.EventSourcing;

/// <summary>
/// Einfacher EventStore der die Events im Hauptspeicher hält.
/// Achtung: Die aktuelle Implementierung geht davon aus dass sich der
/// Fullname eines Aggregates bzw. AggregateEvents nicht ändert. Da der EventStore
/// nicht persistiert wird, führt dies aber auch zu keinen Problemen.
/// </summary>
[UnitTest("Infrastructure")]
public class MemoryEventStoreTests
{
    private const string PersonInfinityImmutableTypeIdentifierName = "PersonImmutable";
    private const string OrderInfinityImmutableTypeIdentifierName = "OrderImmutable";

    private readonly JsonNetSerializer eventSerializer;

    public MemoryEventStoreTests()
    {
        this.eventSerializer = new JsonNetSerializer();
    }

    [Fact]
    public async Task AddEventsToMemoryEventstore()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        var personCreatedEvent = new PersonCreatedEvent("Microsoft", "GovernanceBoard");

        await store.AddAsync<Person>(personCreatedEvent, CancellationToken.None).AnyContext();
        await store.AddAsync<Person>(new ChangeSurnameEvent(personCreatedEvent.AggregateId, 2, "GB"),
            CancellationToken.None).AnyContext();
        var list = await store.GetEventsAsync<Person>(personCreatedEvent.AggregateId, CancellationToken.None).AnyContext();
        personCreatedEvent.ToString().ShouldNotBe(Guid.Empty.ToString());
        list.Length.ShouldBe(2);
    }

    [Fact]
    public async Task AddEventsToMemoryEventstoreAndCheckForCorrectData()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        var personCreatedEvent = new PersonCreatedEvent("Microsoft", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent, CancellationToken.None).AnyContext();

        var events = await store.GetEventsAsync<Person>(personCreatedEvent.AggregateId, CancellationToken.None)
            .AnyContext();
        events.Length.ShouldBe(1);

        var eventFromStore = events.FirstOrDefault();
        eventFromStore.ShouldNotBeNull();
        eventFromStore.ShouldBeOfType(personCreatedEvent.GetType());

        ((PersonCreatedEvent)eventFromStore).Surname.ShouldBe(personCreatedEvent.Surname);
        ((PersonCreatedEvent)eventFromStore).Firstname.ShouldBe(personCreatedEvent.Firstname);
    }

    [Fact]
    public async Task AddEventsToMemoryEventstoreGetIds()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        var personCreatedEvent = new PersonCreatedEvent("Microsoft", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent, CancellationToken.None).AnyContext();
        await store.AddAsync<Person>(new ChangeSurnameEvent(personCreatedEvent.AggregateId, 2, "GB"),
            CancellationToken.None).AnyContext();

        var personCreatedEvent2 = new PersonCreatedEvent("Webfrontends", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent2, CancellationToken.None).AnyContext();

        var orderEvent = new OrderCreatedEvent();
        await store.AddAsync<Order>(orderEvent, CancellationToken.None).AnyContext();

        var ids = await store.GetAggregateIdsAsync(CancellationToken.None).AnyContext();
        ids.Length.ShouldBe(3);
        ids.Contains(personCreatedEvent.AggregateId).ShouldBeTrue();
        ids.Contains(personCreatedEvent2.AggregateId).ShouldBeTrue();
        ids.Contains(orderEvent.AggregateId).ShouldBeTrue();
        ids.Contains(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public async Task AddEventsToMemoryEventstoreGetPersonIds()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        aggregateRegistration.GetImmutableName<Person>().Returns(PersonInfinityImmutableTypeIdentifierName);
        aggregateRegistration.GetImmutableName<Order>().Returns(OrderInfinityImmutableTypeIdentifierName);
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        var personCreatedEvent = new PersonCreatedEvent("Microsoft", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent, CancellationToken.None).AnyContext();
        await store.AddAsync<Person>(new ChangeSurnameEvent(personCreatedEvent.AggregateId, 2, "GB"),
            CancellationToken.None).AnyContext();

        var personCreatedEvent2 = new PersonCreatedEvent("Webfrontends", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent2, CancellationToken.None).AnyContext();

        var orderEvent = new OrderCreatedEvent();
        await store.AddAsync<Order>(orderEvent, CancellationToken.None).AnyContext();

        var ids = await store.GetAggregateIdsAsync<Person>(CancellationToken.None).AnyContext();
        ids.Length.ShouldBe(2);
        ids.Contains(personCreatedEvent.AggregateId).ShouldBeTrue();
        ids.Contains(personCreatedEvent2.AggregateId).ShouldBeTrue();
        ids.Contains(orderEvent.AggregateId).ShouldBeFalse();

        ids = await store.GetAggregateIdsAsync<Order>(CancellationToken.None).AnyContext();
        ids.Length.ShouldBe(1);
        ids.Contains(orderEvent.AggregateId).ShouldBe(true);
    }

    [Fact]
    public async Task AddEventToMemoryEventstore()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        var personCreatedEvent = new PersonCreatedEvent("Microsoft", "GovernanceBoard");
        await store.AddAsync<Person>(personCreatedEvent, CancellationToken.None).AnyContext();
        var list = await store.GetEventsAsync<Person>(personCreatedEvent.AggregateId, CancellationToken.None).AnyContext();
        personCreatedEvent.ToString().ShouldNotBe(Guid.Empty.ToString());
        list.Length.ShouldBe(1);
    }

    [Fact]
    public void CreateMemoryEventstore()
    {
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = new InMemoryEventStoreRepository(this.eventSerializer, aggregateRegistration);
        store.ShouldNotBeNull();
    }

    private static Task SpannedEventStoreOperation()
    {
        return Task.FromResult(true);
    }
}