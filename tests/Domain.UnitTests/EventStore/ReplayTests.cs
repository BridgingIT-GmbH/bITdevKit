// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.EventStore;

using EventSourcing.Model;
using Model;
using Model.Events;

[UnitTest("Domain")]
public class ReplayTests
{
    [Fact]
    public void Replay()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ev = new PersonCreatedEvent(id, "A", "B");
        var events = new List<IAggregateEvent> { ev };

        // Act
        var person = new Person(id, events);

        // Assert
        person.ShouldNotBeNull();
        person.Firstname.ShouldBe("B");
        person.Surname.ShouldBe("A");
    }

    [Fact]
    public void ReplayTwoEvents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ev = new PersonCreatedEvent(id, "A", "B");
        var ev2 = new ChangeSurnameEvent(id, 2, "C");
        var events = new List<IAggregateEvent> { ev, ev2 };

        // Act
        var person = new Person(id, events);

        // Assert
        person.ShouldNotBeNull();
        person.Firstname.ShouldBe("B");
        person.Surname.ShouldBe("C");
    }
}