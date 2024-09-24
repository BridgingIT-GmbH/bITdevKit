// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using Common.PrivateReflection;
using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Store;
using Domain.UnitTests.EventStore.Model;
using Domain.UnitTests.EventStore.Model.Events;
using EventSourcing;
using MediatR;
using Person = Domain.UnitTests.EventStore.Model.Person;

[UnitTest("Infrastructure")]
public class EventStoreTests
{
    [Fact]
    public async Task EventStoreTest_Warmup()
    {
        var mediator = Substitute.For<IMediator>();
        var eventStoreRep = Substitute.For<IEventStoreRepository>();
        var publishAggregateSender = Substitute.For<IPublishAggregateEventSender>();
        var store = new EventStore<Person>(mediator, eventStoreRep, publishAggregateSender, new EventStoreOptions<Person>());
        var person = new Person(new PersonCreatedEvent("Mustermann", "Max"));
        await store.SaveEventsAsync(person, CancellationToken.None)
            .AnyContext();
    }

    [Fact]
    public async Task EventStoreTest_Events()
    {
        var mediator = Substitute.For<IMediator>();
        var eventStoreRep = Substitute.For<IEventStoreRepository>();
        eventStoreRep.When(x => x.ExecuteScopedAsync(Arg.Any<Func<Task>>()))
            .Do(async x =>
            {
                var operation = x.Args()
                    .First() as Func<Task>;
                if (operation is not null)
                {
                    await operation.Invoke()
                        .AnyContext();
                }
            });
        var publishAggregateSender = Substitute.For<IPublishAggregateEventSender>();

        var store = new EventStore<Person>(mediator, eventStoreRep, publishAggregateSender, new EventStoreOptions<Person>());
        var evt = new PersonCreatedEvent("Mustermann", "Max");
        var person = new Person(evt);
        await store.SaveEventsAsync(person, CancellationToken.None)
            .AnyContext();
        await publishAggregateSender.Received(1)
            .WriteToOutboxAsync(evt, person)
            .AnyContext();
        await publishAggregateSender.Received(1)
            .SendEventOccuredAsync(evt, person)
            .AnyContext();
        await publishAggregateSender.Received(1)
            .PublishEventOccuredAsync(evt, person)
            .AnyContext();
        await publishAggregateSender.Received(1)
            .PublishProjectionEventAsync(evt, person)
            .AnyContext();
        await publishAggregateSender.Received(1)
            .SendProjectionEventAsync(evt, person)
            .AnyContext();
        await eventStoreRep.Received(1)
            .AddAsync<Person>(evt, CancellationToken.None)
            .AnyContext();
    }

    [Fact]
    public void EventStoreTest_AggregateWithoutApply()
    {
        var person = new PersonWithoutApplyChangeSurname(new PersonCreatedEvent("Mustermann", "Max"));

        var ex = Record.Exception(() => person.ChangeSurname("Musterfrau"));
        Assert.IsType<PrivateReflectionMethodNotFoundException>(ex);
        Assert.Contains(typeof(ChangeSurnameEvent).FullName ?? string.Empty, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}