// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore;

using EventSourcing.Registration;
using Model.Events;

[UnitTest("Domain")]
public class EventStoreEventRegistrationTests
{
    private const string PersonImmutableConst = "CreatePersonImmutable";
    private const string OrderImmutableConst = "CreateOrderImmutable";

    [Fact]
    public void AggregateNotRegisteredTest()
    {
        var registration = new EventStoreAggregateEventRegistration();
        var ev = new PersonCreatedEvent("a", "b");
        Exception ex = Assert.Throws<AggregateIsNotRegisteredException>(() => registration.GetImmutableName(ev));
    }

    [Fact]
    public void RegisterPersonAggregateEvent_GetTypeOnImmutableNameTest()
    {
        var registration = new EventStoreAggregateEventRegistration();
        registration.Register<PersonCreatedEvent>(PersonImmutableConst);
        var immutableTypeName = registration.GetImmutableName(new PersonCreatedEvent("a", "b"));
        var type = registration.GetTypeOnImmutableName(immutableTypeName);
        immutableTypeName.ShouldBe(PersonImmutableConst);
        type.ShouldBe(typeof(PersonCreatedEvent).FullName);
    }

    [Fact]
    public void RegisterPersonAggregateTest()
    {
        var registration = new EventStoreAggregateEventRegistration();
        registration.Register<PersonCreatedEvent>(PersonImmutableConst);
        var ev = new PersonCreatedEvent("a", "b");
        registration.GetImmutableName(ev)
            .ShouldBe(PersonImmutableConst);
    }

    [Fact]
    public void RegisterPersonAndOrderAggregateTest()
    {
        var registration = new EventStoreAggregateEventRegistration();
        registration.Register<PersonCreatedEvent>(PersonImmutableConst);
        registration.Register<OrderCreatedEvent>(OrderImmutableConst);
        registration.GetImmutableName(new PersonCreatedEvent("a", "b"))
            .ShouldBe(PersonImmutableConst);
        registration.GetImmutableName(new OrderCreatedEvent())
            .ShouldBe(OrderImmutableConst);
    }

    [Fact]
    public void RegisterPersonAndOrderWithIdenticialImmutableNameAggregateTest()
    {
        var registration = new EventStoreAggregateEventRegistration();
        registration.Register<PersonCreatedEvent>(PersonImmutableConst);
        Exception ex = Assert.Throws<ImmutableNameShouldBeUniqueException>(() =>
            registration.Register<OrderCreatedEvent>(PersonImmutableConst));
    }
}