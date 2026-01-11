// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using Shouldly;
using System.Linq;
using Xunit;

public class EntityChangeExtensionsTests
{
    [Fact]
    public void Change_SetProperty_WhenValueDifferent_ShouldUpdateAndRaiseEvents()
    {
        // Arrange
        var person = new PersonStub { FirstName = "John", LastName = "Doe" };

        // Act
        var result = person.ChangeName("Jane", "Doe"); // LastName unchanged

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.FirstName.ShouldBe("Jane");
        person.LastName.ShouldBe("Doe");

        // Check Events
        person.DomainEvents.GetAll().Count().ShouldBe(1); // 1 Custom event only
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonStub.PersonNameChangedEvent);
        // default EntityUpdatedDomainEvent<PersonStub> is not raised as we have a custom event (see ChangeName)
    }

    [Fact]
    public void Change_SetProperty_WhenValuesSame_ShouldNotRaiseEvents()
    {
        // Arrange
        var person = new PersonStub { FirstName = "John", LastName = "Doe" };

        // Act
        var result = person.ChangeName("John", "Doe");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.DomainEvents.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Change_Ensure_WhenFails_ShouldFailAndNotUpdate()
    {
        // Arrange
        var person = new PersonStub { Age = 25 };

        // Act
        var result = person.ChangeAge(-5); // Invalid age

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<Error>().ShouldBeTrue();
        result.GetError<Error>().Message.ShouldContain("negative");
        person.Age.ShouldBe(25); // Should stay original
        person.DomainEvents.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Change_When_WhenFails_ShouldSkipAndNotUpdate()
    {
        // Arrange
        var person = new PersonStub { Age = 25 };

        // Act
        var result = person.ChangeAge(0); // Invalid age

        // Assert
        result.IsSuccess.ShouldBeTrue(); // When in ChangeAge clause skips the change, but does not fail
        person.Age.ShouldBe(25); // Should stay original
        person.DomainEvents.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Change_Ensure_WhenPreConditionFails_ShouldFailFast()
    {
        // Arrange
        var person = new PersonStub { EmploymentStatus = EmploymentStatus.Unemployed };

        // Act
        // Try to give a work email to an unemployed person
        var result = person.ChangeEmail("job@company.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("employed");
        person.Email.ShouldBeNull(); // Should not have touched the property
    }

    [Fact]
    public void Change_SetWithResult_WhenResultFails_ShouldFailChain()
    {
        // Arrange
        var person = new PersonStub();

        // Act
        // IsValidEmail returns Failure for this input
        var result = person.ChangeEmailWithValidation("invalid-email");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("format");
        person.Email.ShouldBeNull();
    }

    [Fact]
    public void Change_WhenPredicate_ShouldOnlyUpdateIfTrue()
    {
        // Arrange
        var person = new PersonStub { Age = 17, FirstName = "Kid" };

        // Act
        // Should only update name to "Adult" if Age >= 18
        var result = person.PromoteToAdult();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.FirstName.ShouldBe("Kid"); // Should NOT change
        person.DomainEvents.GetAll().ShouldBeEmpty();

        // Arrange 2
        person.Age = 18;

        // Act 2
        person.PromoteToAdult();

        // Assert 2
        person.FirstName.ShouldBe("Adult"); // Should change now
    }

    [Fact]
    public void Change_Collection_AddRemove_ShouldUpdateCollection()
    {
        // Arrange
        var person = new PersonStub();
        var address = AddressStub.Create("Home", "Street", "", "12345", "City", "Country");

        // Act - Add
        var resultAdd = person.AddAddress(address);

        // Assert - Add
        resultAdd.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldContain(address);
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonStub.AddressListChangedEvent);

        // Reset events
        person.DomainEvents.Clear();

        // Act - Remove
        var resultRemove = person.RemoveAddress(address);

        // Assert - Remove
        resultRemove.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldBeEmpty();
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonStub.AddressListChangedEvent);
    }

    [Fact]
    public void Change_Execute_ShouldRunArbitraryAction()
    {
        // Arrange
        var person = new PersonStub();
        person.Addresses.Add(AddressStub.Create("Old", "St", "", "1", "C", "C"));

        // Act
        var result = person.ClearAddresses();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldBeEmpty();
        person.DomainEvents.GetAll().ShouldContain(e => e is EntityUpdatedDomainEvent<PersonStub>);
    }

    [Fact]
    public void Change_WithEventContext_ShouldAccessOldValues()
    {
        // Arrange
        var person = new PersonStub { Email = "old@mail.com" };

        // Act
        var result = person.UpdateEmailWithHistory("new@mail.com");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var evt = person.DomainEvents.GetAll().OfType<PersonStub.EmailChangedEvent>().Single();
        evt.OldEmail.ShouldBe("old@mail.com");
        evt.NewEmail.ShouldBe("new@mail.com");
    }

    [Fact]
    public void Change_OnPlainEntity_WhenNoEventsRegistered_ShouldSucceed()
    {
        // Arrange
        var entity = new PlainEntity { Id = Guid.NewGuid(), Name = "Old Name" };

        // Act
        var result = entity.ChangeName("New Name");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        entity.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Change_OnPlainEntity_WhenEventRegistered_ShouldThrowException()
    {
        // Arrange
        var entity = new PlainEntity { Id = Guid.NewGuid(), Name = "Old Name" };

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            entity.ChangeNameWithEvent("New Name"));

        exception.Message.ShouldContain("does not implement IAggregateRoot");
        exception.Message.ShouldContain("PlainEntity");
    }

    // -------------------------------------------------------------------------
    // Test Helpers (PersonStub & Events)
    // -------------------------------------------------------------------------

    /// <summary>
    /// A plain entity that does not inherit from AggregateRoot
    /// </summary>
    private class PlainEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public Result<PlainEntity> ChangeName(string newName)
        {
            return this.Change()
                .Set(e => e.Name, newName)
                .Apply();
        }

        public Result<PlainEntity> ChangeNameWithEvent(string newName)
        {
            return this.Change()
                .Set(e => e.Name, newName)
                .Register(e => new TestDomainEvent()) // not allowed on entities (non aggregate roots)
                .Apply();
        }
    }

    private class TestDomainEvent : DomainEventBase;
}