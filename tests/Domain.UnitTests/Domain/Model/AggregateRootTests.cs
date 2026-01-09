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

public class AggregateRootTests
{
    [Fact]
    public void Change_SetProperty_WhenValueDifferent_ShouldUpdateAndRaiseEvents()
    {
        // Arrange
        var person = new TestPerson { FirstName = "John", LastName = "Doe" };

        // Act
        var result = person.ChangeName("Jane", "Doe"); // LastName unchanged

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.FirstName.ShouldBe("Jane");
        person.LastName.ShouldBe("Doe");

        // Check Events
        person.DomainEvents.GetAll().Count().ShouldBe(1); // 1 Custom event only
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonNameChangedEvent);
        // default EntityUpdatedDomainEvent<TestPerson> is not raised as we have a custom event (see ChangeName)
    }

    [Fact]
    public void Change_SetProperty_WhenValuesSame_ShouldNotRaiseEvents()
    {
        // Arrange
        var person = new TestPerson { FirstName = "John", LastName = "Doe" };

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
        var person = new TestPerson { Age = 25 };

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
        var person = new TestPerson { Age = 25 };

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
        var person = new TestPerson { EmploymentStatus = EmploymentStatus.Unemployed };

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
        var person = new TestPerson();

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
        var person = new TestPerson { Age = 17, FirstName = "Kid" };

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
        var person = new TestPerson();
        var address = AddressStub.Create("Home", "Street", "", "12345", "City", "Country");

        // Act - Add
        var resultAdd = person.AddAddress(address);

        // Assert - Add
        resultAdd.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldContain(address);
        person.DomainEvents.GetAll().ShouldContain(e => e is AddressListChangedEvent);

        // Reset events
        person.DomainEvents.Clear();

        // Act - Remove
        var resultRemove = person.RemoveAddress(address);

        // Assert - Remove
        resultRemove.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldBeEmpty();
        person.DomainEvents.GetAll().ShouldContain(e => e is AddressListChangedEvent);
    }

    [Fact]
    public void Change_Execute_ShouldRunArbitraryAction()
    {
        // Arrange
        var person = new TestPerson();
        person.Addresses.Add(AddressStub.Create("Old", "St", "", "1", "C", "C"));

        // Act
        var result = person.ClearAddresses();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldBeEmpty();
        person.DomainEvents.GetAll().ShouldContain(e => e is EntityUpdatedDomainEvent<TestPerson>);
    }

    [Fact]
    public void Change_WithEventContext_ShouldAccessOldValues()
    {
        // Arrange
        var person = new TestPerson { Email = "old@mail.com" };

        // Act
        var result = person.UpdateEmailWithHistory("new@mail.com");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var evt = person.DomainEvents.GetAll().OfType<EmailChangedEvent>().Single();
        evt.OldEmail.ShouldBe("old@mail.com");
        evt.NewEmail.ShouldBe("new@mail.com");
    }

    // -------------------------------------------------------------------------
    // Test Helpers (TestPerson & Events)
    // -------------------------------------------------------------------------

    /// <summary>
    /// wrapper class to add behavior to the stub without modifying the original file.
    /// </summary>
    private class TestPerson : PersonStub
    {
        public TestPerson()
        {
            this.Id = Guid.NewGuid();
            this.Addresses = []; // Ensure initialization
        }

        public Result<TestPerson> ChangeName(string first, string last)
        {
            return this.Change()
                .Set(p => p.FirstName, first)
                .Set(p => p.LastName, last)
                .Register(p => new PersonNameChangedEvent(p.Id)) // custom event, replaces default EntityUpdatedDomainEvent<TestPerson>
                .Check(p => p.FirstName != string.Empty, "First name cannot be empty") // Post-condition
                .Apply();
        }

        public Result<TestPerson> ChangeAge(int age)
        {
            return this.Change()
                .When(_ => age != 0)
                .Ensure(p => age > 0, "Age must be non-negative") // Pre-condition
                .Set(p => p.Age, age)
                .Check(p => p.Age >= 0, "Age cannot be negative") // Post-condition
                .Apply();
        }

        public Result<TestPerson> ChangeEmail(string email)
        {
            return this.Change()
                // Ensure runs BEFORE any changes
                .Ensure(p => p.EmploymentStatus != EmploymentStatus.Unemployed, "Must be employed to have email")
                .Set(p => p.Email, email)
                .Apply();
        }

        public Result<TestPerson> ChangeEmailWithValidation(string email)
        {
            // Simulating a Result-returning factory
            static Result<string> CreateEmail(string input)
            {
                if (input.Contains("invalid"))
                    return Result<string>.Failure().WithError(new ValidationError("Invalid format"));
                return Result<string>.Success(input);
            }

            return this.Change()
                .Set(p => p.Email, CreateEmail(email))
                .Apply();
        }

        public Result<TestPerson> PromoteToAdult()
        {
            return this.Change()
                .When(p => p.Age >= 18)
                .Set(p => p.FirstName, "Adult")
                .Apply();
        }

        public Result<TestPerson> AddAddress(AddressStub address)
        {
            return this.Change()
                .Add(p => p.Addresses, address)
                .Register(_ => new AddressListChangedEvent())
                .Apply();
        }

        public Result<TestPerson> RemoveAddress(AddressStub address)
        {
            return this.Change()
                .Remove(p => p.Addresses, address)
                .Register(_ => new AddressListChangedEvent())
                .Apply();
        }

        public Result<TestPerson> ClearAddresses()
        {
            return this.Change()
                .Execute(p => p.Addresses.Clear()) // Using Execute for void methods
                .Apply();
        }

        public Result<TestPerson> UpdateEmailWithHistory(string newEmail)
        {
            return this.Change()
                .Set(p => p.Email, newEmail)
                .Register((p, ctx) => new EmailChangedEvent(ctx.GetOldValue<string>(nameof(this.Email)), p.Email))
                .Apply();
        }
    }

    // Domain Events for testing
    private class PersonNameChangedEvent(Guid id) : DomainEventBase
    {
        public Guid PersonId { get; } = id;
    }

    private class AddressListChangedEvent : DomainEventBase;

    private class EmailChangedEvent(string oldEmail, string newEmail) : DomainEventBase
    {
        public string OldEmail { get; } = oldEmail;

        public string NewEmail { get; } = newEmail;
    }
}