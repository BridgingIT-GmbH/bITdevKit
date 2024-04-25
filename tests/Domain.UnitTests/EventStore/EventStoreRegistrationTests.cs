// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore;

using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.UnitTests.EventStore.Model;

[UnitTest("Domain")]
public class EventStoreRegistrationTests
{
    private const string PersonImmutableConst = "PersonImmutable";
    private const string OrderImmutableConst = "OrderImmutable";

    [Fact]
    public void AggregateNotRegisteredTest()
    {
        var registration = new EventStoreAggregateRegistration();
        Exception ex =
            Assert.Throws<AggregateIsNotRegisteredException>(() => registration.GetImmutableName<Order>());
    }

    [Fact]
    public void RegisterPersonAggregateTest()
    {
        var registration = new EventStoreAggregateRegistration();
        registration.Register<Person>(PersonImmutableConst);
        registration.GetImmutableName<Person>().ShouldBe(PersonImmutableConst);
    }

    [Fact]
    public void RegisterPersonAndOrderAggregateTest()
    {
        var registration = new EventStoreAggregateRegistration();
        registration.Register<Person>(PersonImmutableConst);
        registration.Register<Order>(OrderImmutableConst);
        registration.GetImmutableName<Person>().ShouldBe(PersonImmutableConst);
        registration.GetImmutableName<Order>().ShouldBe(OrderImmutableConst);
    }

    [Fact]
    public void RegisterPersonAndOrderWithIdenticialImmutableNameAggregateTest()
    {
        var registration = new EventStoreAggregateRegistration();
        registration.Register<Person>(PersonImmutableConst);
        Exception ex =
            Assert.Throws<ImmutableNameShouldBeUniqueException>(() =>
                registration.Register<Order>(PersonImmutableConst));
    }
}