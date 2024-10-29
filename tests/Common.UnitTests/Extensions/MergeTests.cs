// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Collections.Concurrent;
using Bogus;
using Shouldly;
using Xunit;

public class ExtensionsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Merge_EmptyEnumerables_ReturnsEmptyEnumerable()
    {
        // Arrange
        var primary = Enumerable.Empty<string>();
        var secondary = Enumerable.Empty<string>();

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_PrimaryEnumerableWithValues_SecondaryEmpty_ReturnsPrimaryValues()
    {
        // Arrange
        var primaryValues = new[] { this.faker.Random.Word(), this.faker.Random.Word() };
        var primary = primaryValues.AsEnumerable();
        var secondary = Enumerable.Empty<string>();

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBe(primaryValues);
    }

    [Fact]
    public void Merge_DictionariesWithOverlappingKeys_PrimaryValuesTakePrecedence()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var primaryValue = this.faker.Random.Word();
        var secondaryValue = this.faker.Random.Word();

        var primary = new Dictionary<string, string> { [key] = primaryValue };
        var secondary = new Dictionary<string, string> { [key] = secondaryValue };

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result[key].ShouldBe(primaryValue);
    }

    [Fact]
    public void Merge_HashSets_RemovesDuplicates()
    {
        // Arrange
        var commonValue = this.faker.Random.Word();
        var primary = new HashSet<string> { commonValue, this.faker.Random.Word() };
        var secondary = new HashSet<string> { commonValue, this.faker.Random.Word() };

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.Count(x => x == commonValue).ShouldBe(1);
    }

    [Fact]
    public void Merge_SortedSets_MaintainsSortOrder()
    {
        // Arrange
        var primary = new SortedSet<int> { 3, 1 };
        var secondary = new SortedSet<int> { 4, 2 };

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBe(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Merge_ConcurrentDictionaries_HandlesConcurrentUpdates()
    {
        // Arrange
        var key = this.faker.Random.Word();
        var primaryValue = this.faker.Random.Word();
        var secondaryValue = this.faker.Random.Word();

        var primary = new ConcurrentDictionary<string, string>();
        var secondary = new ConcurrentDictionary<string, string>();

        primary.TryAdd(key, primaryValue);
        secondary.TryAdd(key, secondaryValue);

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result[key].ShouldBe(primaryValue);
    }

    [Fact]
    public void Merge_Queues_MaintainsOrder()
    {
        // Arrange
        var primaryValues = new[] { this.faker.Random.Word(), this.faker.Random.Word() };
        var secondaryValues = new[] { this.faker.Random.Word(), this.faker.Random.Word() };

        var primary = new Queue<string>(primaryValues);
        var secondary = new Queue<string>(secondaryValues);

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBe(secondaryValues.Concat(primaryValues));
    }

    [Fact]
    public void Merge_Stacks_PreservesStackOrder()
    {
        // Arrange
        var primaryValues = new[] { this.faker.Random.Word() + "_1a", this.faker.Random.Word() + "_1b" };
        var secondaryValues = new[] { this.faker.Random.Word() + "_2a", this.faker.Random.Word()  + "_2b"};

        var primary = new Stack<string>(primaryValues);
        var secondary = new Stack<string>(secondaryValues);

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBe(secondaryValues.Reverse().Concat(primaryValues.Reverse()));
    }

    [Fact]
    public void Merge_LinkedLists_PreservesOrder()
    {
        // Arrange
        var primaryValues = new[] { this.faker.Random.Word(), this.faker.Random.Word() };
        var secondaryValues = new[] { this.faker.Random.Word(), this.faker.Random.Word() };

        var primary = new LinkedList<string>(primaryValues);
        var secondary = new LinkedList<string>(secondaryValues);

        // Act
        var result = primary.Merge(secondary);

        // Assert
        result.ShouldBe(secondaryValues.Concat(primaryValues));
    }
}