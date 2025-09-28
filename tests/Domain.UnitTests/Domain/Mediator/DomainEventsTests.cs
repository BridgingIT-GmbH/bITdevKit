// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Mediator;

using DevKit.Domain.Model;
using MediatR;

[UnitTest("Domain")]
public class DomainEventsTests
{
    [Fact]
    public void GetAll_WhenCalled_ReturnsAllRegisteredEvents()
    {
        // Arrange
        var sut = new DomainEvents();
        var domainEvent1 = Substitute.For<IDomainEvent>();
        var domainEvent2 = Substitute.For<IDomainEvent>();
        sut.Register(domainEvent1);
        sut.Register(domainEvent2);

        // Act
        var events = sut.GetAll();

        // Assert
        events.ShouldContain(domainEvent1);
        events.ShouldContain(domainEvent2);
    }

    // Test for Register(IEnumerable<IDomainEvent>) method
    [Fact]
    public void Register_MultipleEvents_EventsAreRegistered()
    {
        // Arrange
        var sut = new DomainEvents();
        var domainEvent1 = Substitute.For<IDomainEvent>();
        var domainEvent2 = Substitute.For<IDomainEvent>();

        // Act
        sut.Register([domainEvent1, domainEvent2]);

        // Assert
        var registeredEvents = sut.GetAll();
        registeredEvents.ShouldContain(domainEvent1);
        registeredEvents.ShouldContain(domainEvent2);
    }

    // Test for Register(IDomainEvent, bool) method with ensureSingleByType = true
    [Fact]
    public void Register_EventWithEnsureSingleByType_OnlyOneEventOfTypeRegistered()
    {
        // Arrange
        var sut = new DomainEvents();
        var domainEvent1 = Substitute.For<IDomainEvent>();
        var domainEvent2 = Substitute.For<IDomainEvent>();

        // Act
        sut.Register(domainEvent1);
        sut.Register(domainEvent2, true);

        // Assert
        var registeredEvents = sut.GetAll()
            .ToList();
        registeredEvents.ShouldNotContain(domainEvent1);
        registeredEvents.ShouldContain(domainEvent2);
    }

    // Test for DispatchAsync method
    [Fact]
    public async Task DispatchAsync_WhenCalled_DispatchesAllEvents()
    {
        // Arrange
        var sut = new DomainEvents();
        var domainEvent1 = Substitute.For<IDomainEvent>();
        var domainEvent2 = Substitute.For<IDomainEvent>();
        var notifier = Substitute.For<INotifier>();
        sut.Register(domainEvent1);
        sut.Register(domainEvent2);

        // Act
        await sut.PublishAsync(notifier);

        // Assert
        await notifier.Received(1)
            .PublishAsync(domainEvent1);
        await notifier.Received(1)
            .PublishAsync(domainEvent2);
    }

    // Test for Clear method
    [Fact]
    public void Clear_WhenCalled_RegisteredEventsAreCleared()
    {
        // Arrange
        var sut = new DomainEvents();
        var domainEvent = Substitute.For<IDomainEvent>();
        sut.Register(domainEvent);

        // Act
        sut.Clear();

        // Assert
        var events = sut.GetAll();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void Register_NullEvent_EventIsNotRegistered()
    {
        // Arrange
        var sut = new DomainEvents();

        // Act
        sut.Register((IDomainEvent)null);

        // Assert
        var events = sut.GetAll();
        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_NullMediator_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new DomainEvents();
        var act = () => sut.PublishAsync((INotifier)null);

        // Act & Assert
        await act.ShouldThrowAsync<ArgumentNullException>();
    }
}