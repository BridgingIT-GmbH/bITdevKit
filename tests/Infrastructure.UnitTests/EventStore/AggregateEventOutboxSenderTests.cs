// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EventStore;

using Domain.EventSourcing.Registration;
using Domain.Outbox;
using Domain.UnitTests.EventStore.Model;
using Domain.UnitTests.EventStore.Model.Events;
using EventSourcing.Publishing;

[UnitTest("Infrastructure")]
public class AggregateEventOutboxSenderTests
{
    [Fact]
    public void AggregateEventOutboxSenderTest_Test()
    {
        // Arrange
        var rep = Substitute.For<IOutboxMessageWriterRepository>();
        var regAggregateEvent = Substitute.For<IEventStoreAggregateEventRegistration>();
        var regAggregate = Substitute.For<IEventStoreAggregateRegistration>();

        // Act
        var sut = new AggregateEventOutboxSender(rep, regAggregateEvent, regAggregate);

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public async Task AggregateEventOutboxSenderTest_Send()
    {
        // Arrange
        var personEvent = new PersonCreatedEvent("Doe", "John");
        var rep = Substitute.For<IOutboxMessageWriterRepository>();
        var regAggregateEvent = Substitute.For<IEventStoreAggregateEventRegistration>();
        var regAggregate = Substitute.For<IEventStoreAggregateRegistration>();

        // Act
        var sut = new AggregateEventOutboxSender(rep, regAggregateEvent, regAggregate);
        await sut.WriteToOutboxAsync(personEvent, new Person("Doe", "John"))
            .AnyContext();

        // Assert
        await rep.Received(1)
            .InsertAsync(Arg.Any<OutboxMessage>())
            .AnyContext();
    }
}