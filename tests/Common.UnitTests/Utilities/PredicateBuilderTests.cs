// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

public class PredicateBuilderTests
{
    private static List<PersonStub> GetPeople()
    {
        return
        [
            new("Alice", "Smith", "alice@example.com", 30),
            new("Bob", "Jones", "bob@example.com", 40),
            new("Charlie", "Brown", "charlie@example.com", 25),
            new("Diana", "Prince", "diana@example.com", 35),
            new("Eve", "Adams", "eve@example.com", 50)
        ];
    }

    [Fact]
    public void Add_Should_Filter_By_FirstName()
    {
        // Arrange
        var people = GetPeople();
        var builder = new PredicateBuilder<PersonStub>()
            .Add(p => p.FirstName == "Alice");

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].FirstName.ShouldBe("Alice");
    }

    [Fact]
    public void AddIf_Should_Conditionally_Add_Expression()
    {
        // Arrange
        var people = GetPeople();
        const bool filterByAge = true;
        var builder = new PredicateBuilder<PersonStub>()
            .AddIf(filterByAge, p => p.Age > 30);

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.All(p => p.Age > 30).ShouldBeTrue();
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Or_Should_Combine_With_Or()
    {
        // Arrange
        var people = GetPeople();
        var builder = new PredicateBuilder<PersonStub>()
            .Add(p => p.FirstName == "Alice")
            .Or(p => p.FirstName == "Bob");

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.Any(p => p.FirstName == "Alice").ShouldBeTrue();
        result.Any(p => p.FirstName == "Bob").ShouldBeTrue();
    }

    [Fact]
    public void AddRange_Should_Add_Multiple_Expressions()
    {
        // Arrange
        var people = GetPeople();
        int? minAge = 30;
        int? maxAge = 40;
        var builder = new PredicateBuilder<PersonStub>()
            .AddRange(
            [
                (minAge != null, p => p.Age >= minAge),
                (maxAge != null, p => p.Age <= maxAge)
            ]);

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.All(p => p.Age >= 30 && p.Age <= 40).ShouldBeTrue();
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void NotIf_Should_Negate_Expression()
    {
        // Arrange
        var people = GetPeople();
        const bool excludeEve = true;
        var builder = new PredicateBuilder<PersonStub>()
            .NotIf(excludeEve, p => p.FirstName == "Eve");

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.Any(p => p.FirstName == "Eve").ShouldBeFalse();
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void Grouping_Should_Work_With_Or()
    {
        // Arrange
        var people = GetPeople();
        var builder = new PredicateBuilder<PersonStub>()
            .BeginGroup()
                .Add(p => p.FirstName == "Alice")
                .Or(p => p.FirstName == "Bob")
            .EndGroup()
            .Add(p => p.Age > 20);

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.All(p => p.FirstName == "Alice" || p.FirstName == "Bob").ShouldBeTrue();
    }

    [Fact]
    public void AddIfElse_Should_Add_Correct_Expression()
    {
        // Arrange
        var people = GetPeople();
        const bool useYoung = false;
        var builder = new PredicateBuilder<PersonStub>()
            .AddIfElse(useYoung, p => p.Age < 30, p => p.Age >= 30);

        // Act
        var predicate = builder.Build();
        var result = people.Where(predicate).ToList();

        // Assert
        result.All(p => p.Age >= 30).ShouldBeTrue();
        result.Count.ShouldBe(4);
    }
}