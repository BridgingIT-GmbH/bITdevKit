// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using System;
using System.Collections.Generic;
using System.Threading;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;
using BridgingIT.DevKit.Infrastructure.EventSourcing;

using NSubstitute;

[UnitTest("Infrastructure")]
public class ProjectionTests
{
    public static readonly string PersonInfinityImmutableTypeIdentifierName = "PersonImmutable";

    [Fact]
    public async void RequestProjectionTest()
    {
        var person = new Person("GB", "Microsoft");
        var id1 = person.Id;
        var eventStore = Substitute.For<IEventStore<Person>>();
        eventStore.GetAggregateIdsAsync(CancellationToken.None).Returns(new List<Guid>() { id1 });
        eventStore.GetAsync(id1, CancellationToken.None).Returns(person);
        var publishSender = Substitute.For<IPublishAggregateEventSender>();
        var projectionRequest = new ProjectionRequester<Person>(eventStore, publishSender, Substitute.For<Microsoft.Extensions.Logging.ILoggerFactory>());
        await projectionRequest.RequestProjectionAsync(CancellationToken.None).AnyContext();
        await eventStore.Received()
            .GetAggregateIdsAsync(CancellationToken.None).AnyContext();
        await eventStore.Received().GetAsync(id1, CancellationToken.None).AnyContext();
        await publishSender.Received().PublishProjectionEventAsync(null, person).AnyContext();
    }

    [Fact]
    public async void RequestProjectionTest2()
    {
        var person1 = new Person("GB", "Microsoft");
        var person2 = new Person("GB", "Webfrontends");
        var person3 = new Person("a", "b");
        var eventStore = Substitute.For<IEventStore<Person>>();
        eventStore.GetAggregateIdsAsync(CancellationToken.None)
            .Returns(new List<Guid>() { person1.Id, person2.Id });
        eventStore.GetAsync(person1.Id, CancellationToken.None).Returns(person1);
        eventStore.GetAsync(person2.Id, CancellationToken.None).Returns(person2);
        var publishSender = Substitute.For<IPublishAggregateEventSender>();
        var projectionRequest = new ProjectionRequester<Person>(eventStore, publishSender, Substitute.For<Microsoft.Extensions.Logging.ILoggerFactory>());
        await projectionRequest.RequestProjectionAsync(CancellationToken.None).AnyContext();
        await eventStore.Received()
            .GetAggregateIdsAsync(CancellationToken.None).AnyContext();
        await eventStore.Received().GetAsync(person1.Id, CancellationToken.None).AnyContext();
        await eventStore.Received().GetAsync(person2.Id, CancellationToken.None).AnyContext();
        await publishSender.Received().PublishProjectionEventAsync(null, person1).AnyContext();
        await publishSender.Received().PublishProjectionEventAsync(null, person2).AnyContext();
        await publishSender.DidNotReceive().PublishProjectionEventAsync(null, person3).AnyContext();
    }
}