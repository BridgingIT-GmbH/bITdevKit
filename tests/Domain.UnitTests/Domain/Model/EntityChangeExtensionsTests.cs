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

    [Fact]
    public void Change_Collection_WithBackingField_ShouldWork()
    {
        // Arrange
        var person = new PersonStub();
        var address = AddressStub.Create("Home", "Street", "", "12345", "City", "Country");

        // Act - Add using backing field
        var resultAdd = person.AddAddress(address);

        // Assert - Add
        resultAdd.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldContain(address);
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonStub.AddressListChangedEvent);

        // Reset events
        person.DomainEvents.Clear();

        // Act - Remove using backing field
        var resultRemove = person.RemoveAddress(address);

        // Assert - Remove
        resultRemove.IsSuccess.ShouldBeTrue();
        person.Addresses.ShouldBeEmpty();
        person.DomainEvents.GetAll().ShouldContain(e => e is PersonStub.AddressListChangedEvent);
    }

    [Fact]
    public void Change_Set_WithResultReturningMethod_WhenSuccess_ShouldApplyChanges()
    {
        // Arrange
        var person = new PersonStub
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 25,
            EmploymentStatus = EmploymentStatus.FullTime
        };

        // Act - Using Set to chain multiple Result-returning methods
        var result = person.ChangeName("Jane", "Smith", 30, "jane@example.com");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        person.FirstName.ShouldBe("Jane");
        person.LastName.ShouldBe("Smith");
        person.Age.ShouldBe(30);
        person.Email.ShouldBe("jane@example.com");
    }

    [Fact]
    public void Change_Set_WithResultReturningMethod_WhenFirstFails_ShouldStopChain()
    {
        // Arrange
        var person = new PersonStub
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 25,
            EmploymentStatus = EmploymentStatus.FullTime
        };

        // Act - First Set should fail due to empty first name (Check validation)
        var result = person.ChangeName("", "Smith", 30, "jane@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        // Check uses messages, not errors
        result.Messages.ShouldNotBeEmpty();
        result.Messages[0].ShouldContain("First name");
        // Check modifies state then validates, so first Set modified state before failing
        person.FirstName.ShouldBe("");
        person.LastName.ShouldBe("Smith");
        // But second and third Set never run because first failed
        person.Age.ShouldBe(25);
        person.Email.ShouldBeNull();
    }

    [Fact]
    public void Change_Set_WithResultReturningMethod_WhenSecondFails_ShouldStopAtSecond()
    {
        // Arrange
        var person = new PersonStub
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 25,
            EmploymentStatus = EmploymentStatus.FullTime
        };

        // Act - Second Set (ChangeAge) should fail due to negative age
        var result = person.ChangeName("Jane", "Smith", -5, "jane@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("negative");
        // First Set succeeded, so name changed
        person.FirstName.ShouldBe("Jane");
        person.LastName.ShouldBe("Smith");
        // Second Set failed, so age and email remain unchanged
        person.Age.ShouldBe(25);
        person.Email.ShouldBeNull();
    }

    [Fact]
    public void Change_Set_WithResultReturningMethod_WhenThirdFails_ShouldStopAtThird()
    {
        // Arrange
        var person = new PersonStub
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 25,
            EmploymentStatus = EmploymentStatus.Unemployed // This will cause email change to fail
        };

        // Act - Third Set (ChangeEmail) should fail due to unemployment status
        var result = person.ChangeName("Jane", "Smith", 30, "jane@example.com");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("employed");
        // First and second Set succeeded
        person.FirstName.ShouldBe("Jane");
        person.LastName.ShouldBe("Smith");
        person.Age.ShouldBe(30);
        // Third Set failed, so email remains unchanged
        person.Email.ShouldBeNull();
    }

    [Fact]
    public void Change_Execute_WhenActionThrowsException_ShouldCatchAndReturnFailure()
    {
        // Arrange
        var person = new PersonStub
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var result = person.Change()
            .Execute((PersonStub p) => throw new InvalidOperationException("Test exception"))
            .Apply();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.Messages.ShouldContain(m => m.Contains("Test exception"));
    }

    [Fact]
    public void Change_Execute_WhenActionSucceeds_ShouldContinueChain()
    {
        // Arrange
        var person = new PersonStub { Age = 30 };
        var executionCount = 0;

        // Act
        var result = person.Change()
            .Execute(p => executionCount++)
            .Execute(p => executionCount++)
            .Set(p => p.FirstName, "Updated")
            .Apply();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        executionCount.ShouldBe(2);
            person.FirstName.ShouldBe("Updated");
        }

        [Fact]
        public void Change_Execute_WhenStandalone_ShouldExecuteTransformation()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };
            var tapExecuted = false;

            // Act - Do without Set, standalone usage
            var result = person.Change()
                .Execute(r => r.Tap(e => tapExecuted = true))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            tapExecuted.ShouldBeTrue();
            result.Value.ShouldBe(person);
        }

        [Fact]
        public void Change_Execute_WhenChainedWithSet_ShouldExecuteAfterSet()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };
            var observedName = string.Empty;

            // Act - Do after Set
            var result = person.Change()
                .Set(p => p.FirstName, "Jane")
                .Execute(r => r.Tap(e => observedName = e.FirstName))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            person.FirstName.ShouldBe("Jane");
            observedName.ShouldBe("Jane"); // Do observed the changed value
        }

        [Fact]
        public void Change_Execute_WithEnsure_WhenValidationFails_ShouldReturnFailure()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };

            // Act - Do with Ensure that fails
            var result = person.Change()
                .Set(p => p.FirstName, "")
                .Execute(r => r.Ensure(e => !string.IsNullOrEmpty(e.FirstName), new ValidationError("Name cannot be empty")))
                .Apply();

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.HasError<ValidationError>().ShouldBeTrue();
            result.GetError<ValidationError>().Message.ShouldBe("Name cannot be empty");
            person.FirstName.ShouldBe(""); // Change was applied, Do validation failed
        }

        [Fact]
        public void Change_Execute_MultipleCalls_ShouldExecuteSequentially()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };
            var executionOrder = new List<int>();

            // Act - Multiple Do calls
            var result = person.Change()
                .Execute(r => r.Tap(_ => executionOrder.Add(1)))
                .Execute(r => r.Tap(_ => executionOrder.Add(2)))
                .Execute(r => r.Tap(_ => executionOrder.Add(3)))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            executionOrder.ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void Change_Execute_WhenFirstFails_ShouldShortCircuit()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };
            var secondDoExecuted = false;

            // Act - First Do fails, second should not execute
            var result = person.Change()
                .Execute(r => Result<PersonStub>.Failure().WithError(new Error("First Do failed")))
                .Execute(r => r.Tap(_ => secondDoExecuted = true))
                .Apply();

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.HasError<Error>().ShouldBeTrue();
            secondDoExecuted.ShouldBeFalse(); // Should not have executed
        }

        [Fact]
        public void Change_Execute_WithMap_ShouldTransformResult()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John", Age = 25 };
            DateTime? capturedTimestamp = null;

            // Act - Use Map to add side effect
            var result = person.Change()
                .Set(p => p.Age, 26)
                .Execute(r => r.Map(e =>
                {
                    capturedTimestamp = DateTime.UtcNow;
                    return e;
                }))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            person.Age.ShouldBe(26);
            capturedTimestamp.ShouldNotBeNull();
        }

        [Fact]
        public void Change_Execute_WhenGuardSkipsOperations_ShouldNotExecuteDo()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John", Age = 15 };
            var doExecuted = false;

            // Act - When guard prevents Set, Do should also NOT execute
            var result = person.Change()
                .When(p => p.Age >= 18) // This is false, skipping entire transaction
                .Set(p => p.FirstName, "Adult")
                .Execute(r => r.Tap(_ => doExecuted = true))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            person.FirstName.ShouldBe("John"); // Not changed due to guard
            doExecuted.ShouldBeFalse(); // Do should NOT execute when guard fails
        }

        [Fact]
        public void Change_Execute_WithNullTransformation_ShouldBeIgnored()
        {
            // Arrange
            var person = new PersonStub { FirstName = "John" };

            // Act - Do with null transformation (should be ignored)
            var result = person.Change()
                .Execute(null)
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }

        [Fact]
        public void Change_Execute_RealWorldScenario_PromoteToAdultWithValidation()
        {
            // Arrange - Person eligible for adult promotion
            var person = new PersonStub { FirstName = "John", LastName = "Doe", Age = 18 };
            var promotionLogged = false;

            // Act - Promote to adult with validation and logging
            var result = person.Change()
                .When(p => p.Age >= 18) // Only promote if age qualifies
                .Set(p => p.FirstName, "Adult")
                .Execute(r => r.Map(e => { e.LastName = "Adult"; return e; })) // Additional field update via Do
                .Execute(r => r.Ensure(
                    e => !string.IsNullOrEmpty(e.FirstName) && !string.IsNullOrEmpty(e.LastName),
                    new ValidationError("Name fields cannot be empty after promotion")))
                .Execute(r => r.Tap(e => promotionLogged = true)) // Log the promotion
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            person.FirstName.ShouldBe("Adult");
            person.LastName.ShouldBe("Adult");
            promotionLogged.ShouldBeTrue();
        }

        [Fact]
        public void Change_Execute_RealWorldScenario_MinorCannotBePromoted()
        {
            // Arrange - Person NOT eligible for adult promotion (under 18)
            var person = new PersonStub { FirstName = "John", LastName = "Doe", Age = 16 };
            var promotionLogged = false;

            // Act - Attempt to promote minor (should be silently skipped)
            var result = person.Change()
                .When(p => p.Age >= 18) // Guard will fail
                .Set(p => p.FirstName, "Adult")
                .Execute(r => r.Map(e => { e.LastName = "Adult"; return e; }))
                .Execute(r => r.Tap(e => promotionLogged = true))
                .Apply();

            // Assert
            result.IsSuccess.ShouldBeTrue(); // Success but no changes
            person.FirstName.ShouldBe("John"); // Original name preserved
            person.LastName.ShouldBe("Doe"); // Original last name preserved
            promotionLogged.ShouldBeFalse(); // Do operations never executed
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