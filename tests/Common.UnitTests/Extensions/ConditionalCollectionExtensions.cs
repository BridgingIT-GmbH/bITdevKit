// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

[UnitTest("Common")]
public class ConditionalCollectionExtensionsTests
{
    [Fact]
    public void AddIf_WithTrueCondition_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        people.AddIf(person, person.Age > 25);

        // Assert
        people.ShouldContain(person);
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIf_WithFalseCondition_DoesNotAddPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("Jane", "Doe", "jane.doe@example.com", 20);

        // Act
        people.AddIf(person, person.Age > 25);

        // Assert
        people.ShouldBeEmpty();
    }

    [Fact]
    public void AddIf_WithNullCollection_ReturnsNullSilently()
    {
        // Arrange
        List<PersonStub> people = null;
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        var result = people.AddIf(person, true);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfPredicate_WithTruePredicate_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 42);

        // Act
        people.AddIf(person, x => x.Age > 40);

        // Assert
        people.ShouldContain(person);
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIfPredicate_WithNullPredicate_DoesNotAddPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("Jane", "Doe", "jane.doe@example.com", 25);

        // Act
        people.AddIf(person, (Func<PersonStub, bool>)null);

        // Assert
        people.ShouldBeEmpty(); // No exception, just no add
    }

    [Fact]
    public void AddIfNotNull_WithNonNullPerson_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        people.AddIfNotNull(person);

        // Assert
        people.ShouldContain(person);
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIfNotNull_WithNullPerson_DoesNotAdd()
    {
        // Arrange
        var people = new List<PersonStub>();
        PersonStub person = null;

        // Act
        people.AddIfNotNull(person);

        // Assert
        people.ShouldBeEmpty();
    }

    [Fact]
    public void AddRangeIf_WithTrueCondition_AddsAllPeople()
    {
        // Arrange
        var people = new List<PersonStub>();
        var range = new[]
        {
            new PersonStub("John", "Doe", "john.doe@example.com", 30),
            new PersonStub("Jane", "Doe", "jane.doe@example.com", 25)
        };

        // Act
        people.AddRangeIf(range, range.Any(p => p.Age > 25));

        // Assert
        people.Count.ShouldBe(2);
        people.ShouldContain(range[0]);
        people.ShouldContain(range[1]);
    }

    [Fact]
    public void AddRangeIf_WithNullItems_AddsNothing()
    {
        // Arrange
        var people = new List<PersonStub>();
        IEnumerable<PersonStub> range = null;

        // Act
        people.AddRangeIf(range, true);

        // Assert
        people.ShouldBeEmpty(); // SafeNull() handles null items
    }

    [Fact]
    public void AddIfUnique_WithNewPerson_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub> { PersonStub.Create(1) };
        var newPerson = PersonStub.Create(2);

        // Act
        people.AddIfUnique(newPerson);

        // Assert
        people.Count.ShouldBe(2);
        people.ShouldContain(newPerson);
    }

    [Fact]
    public void AddIfUnique_WithDuplicatePerson_DoesNotAdd()
    {
        // Arrange
        var people = new List<PersonStub>(); // { PersonStub.Create(1) };
        var person = new PersonStub { FirstName = "John1", LastName = "Doe1", Age = 42 }; // Same as Create(1)
        people.Add(person);

        // Act
        people.AddIfUnique(person);

        // Assert
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIfAll_WithAllTrue_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 35);

        // Act
        people.AddIfAll(person, x => x.Age > 30, x => x.FirstName.StartsWith("J"));

        // Assert
        people.ShouldContain(person);
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIfAll_WithOneFalse_DoesNotAdd()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("Jane", "Doe", "jane.doe@example.com", 25);

        // Act
        people.AddIfAll(person, x => x.Age > 30, x => x.FirstName.StartsWith("J"));

        // Assert
        people.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfAll_WithNullPredicates_DoesAdd()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        people.AddIfAll(person, null); 

        // Assert
        people.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddIfAny_WithOneTrue_AddsPerson()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 20);

        // Act
        people.AddIfAny(person, x => x.Age > 25, x => x.FirstName.StartsWith("J"));

        // Assert
        people.ShouldContain(person);
        people.Count.ShouldBe(1);
    }

    [Fact]
    public void AddIfAny_WithAllFalse_DoesNotAdd()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("Alice", "Smith", "alice.smith@example.com", 20);

        // Act
        people.AddIfAny(person, x => x.Age > 25, x => x.FirstName.StartsWith("J"));

        // Assert
        people.ShouldBeEmpty();
    }

    [Fact]
    public void AddIfAny_WithNullCollection_ReturnsNullSilently()
    {
        // Arrange
        List<PersonStub> people = null;
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        var result = people.AddIfAny(person, x => x.Age > 25);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AddIfAny_WithNullPredicates_DoesNotAdd()
    {
        // Arrange
        var people = new List<PersonStub>();
        var person = new PersonStub("John", "Doe", "john.doe@example.com", 30);

        // Act
        people.AddIfAny(person, null); // SafeAny() handles this

        // Assert
        people.ShouldBeEmpty();
    }
}