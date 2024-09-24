// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Registration;
using Domain.EventSourcing.Store;
using Domain.UnitTests.EventStore.Model;
using Domain.UnitTests.EventStore.Model.Events;
using EventSourcing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Infrastructure")]
public class RepositoryIntegrationTests(ITestOutputHelper output) : TestsBase(output, s => s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(TestsBase).Assembly)))
{
    [Fact]
    public async Task Get()
    {
        var mediator = Substitute.For<IMediator>();
        var aggregateId = Guid.NewGuid();
        var store = Substitute.For<IEventStoreRepository>();
        var aggregateEventSender = Substitute.For<IPublishAggregateEventSender>();
        var list = new List<IAggregateEvent> { new PersonCreatedEvent(aggregateId, "Mustermann", "Max"), new ChangeSurnameEvent(aggregateId, 2, "Musterfrau") };
        var listTask = Task.FromResult(list.ToArray());
        store.GetEventsAsync<Person>(aggregateId, CancellationToken.None)
            .Returns(listTask);
        var rep = new EventStore<Person>(mediator, store, aggregateEventSender, new EventStoreOptions<Person>());
        var aggregate = await rep.GetAsync(aggregateId, CancellationToken.None)
            .AnyContext();
        aggregate.ShouldNotBeNull();
        aggregate.ShouldBeOfType<Person>();
        aggregate.Firstname.ShouldBe("Max");
        aggregate.Surname.ShouldBe("Musterfrau");
    }

    [Fact]
    public async Task Save()
    {
        var mediator = Substitute.For<IMediator>();
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = Substitute.ForPartsOf<InMemoryEventStoreRepository>(new JsonNetSerializer(), aggregateRegistration);
        var aggregateEventSender = Substitute.For<IPublishAggregateEventSender>();
        var rep = new EventStore<Person>(mediator, store, aggregateEventSender, new EventStoreOptions<Person>());
        var aggregate = new Person("A", "B");
        aggregate.ChangeSurname("C");
        await rep.SaveEventsAsync(aggregate, CancellationToken.None)
            .AnyContext();
        await mediator.Received()
            .Publish(Arg.Any<PersonCreatedEvent>(), CancellationToken.None)
            .AnyContext();
        await mediator.Received()
            .Publish(Arg.Any<ChangeSurnameEvent>(), CancellationToken.None)
            .AnyContext();
        await store.Received()
            .AddAsync<Person>(Arg.Any<PersonCreatedEvent>(), CancellationToken.None)
            .AnyContext();
        await aggregateEventSender.Received()
            .PublishProjectionEventAsync(Arg.Any<PersonCreatedEvent>(), aggregate)
            .AnyContext();
        await aggregateEventSender.Received()
            .PublishProjectionEventAsync(Arg.Any<ChangeSurnameEvent>(), aggregate)
            .AnyContext();
    }

    [Fact]
    public async Task SaveMediatorIntegration()
    {
        var mediator = this.ServiceProvider.GetService(typeof(IMediator)) as IMediator;
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = Substitute.ForPartsOf<InMemoryEventStoreRepository>(new JsonNetSerializer(), aggregateRegistration);
        var aggregateEventSender = Substitute.For<IPublishAggregateEventSender>();
        var rep = new EventStore<Person>(mediator, store, aggregateEventSender, new EventStoreOptions<Person>());
        var aggregate = new Person("A", "B");
        aggregate.ChangeSurname("C");
        await rep.SaveEventsAsync(aggregate, CancellationToken.None)
            .AnyContext();
        await store.Received()
            .AddAsync<Person>(Arg.Any<PersonCreatedEvent>(), CancellationToken.None)
            .AnyContext();
        await aggregateEventSender.Received()
            .PublishProjectionEventAsync(Arg.Any<PersonCreatedEvent>(), aggregate)
            .AnyContext();
        await aggregateEventSender.Received()
            .PublishProjectionEventAsync(Arg.Any<ChangeSurnameEvent>(), aggregate)
            .AnyContext();
    }

    [Fact]
    public async Task SaveOneEvent()
    {
        var mediator = Substitute.For<IMediator>();
        var aggregateRegistration = Substitute.For<IEventStoreAggregateRegistration>();
        var store = Substitute.ForPartsOf<InMemoryEventStoreRepository>(new JsonNetSerializer(), aggregateRegistration);
        var aggregateEventSender = Substitute.For<IPublishAggregateEventSender>();
        var rep = new EventStore<Person>(mediator, store, aggregateEventSender, new EventStoreOptions<Person>());
        var aggregate = new Person("A", "B");
        await rep.SaveEventsAsync(aggregate, CancellationToken.None)
            .AnyContext();
        await mediator.Received()
            .Publish(Arg.Any<PersonCreatedEvent>(), CancellationToken.None)
            .AnyContext();
        await mediator.DidNotReceive()
            .Publish(Arg.Any<ChangeSurnameEvent>(), CancellationToken.None)
            .AnyContext();
        await store.Received()
            .AddAsync<Person>(Arg.Any<PersonCreatedEvent>(), CancellationToken.None)
            .AnyContext();
        await store.DidNotReceive()
            .AddAsync<Person>(Arg.Any<ChangeSurnameEvent>(), CancellationToken.None)
            .AnyContext();
        await aggregateEventSender.Received()
            .PublishProjectionEventAsync(Arg.Any<PersonCreatedEvent>(), aggregate)
            .AnyContext();
        await aggregateEventSender.DidNotReceive()
            .PublishProjectionEventAsync(Arg.Any<ChangeSurnameEvent>(), aggregate)
            .AnyContext();
    }

    private static Task SpannedEventStoreOperation()
    {
        return Task.FromResult(true);
    }
}