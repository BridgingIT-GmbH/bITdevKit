// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Bogus;
using Shouldly;
using Xunit;

public class PartitionTests
{
    private readonly Faker faker = new();

    [Fact]
    public void Partition_NullSource_ReturnsEmptyPartitions()
    {
        // Arrange
        IEnumerable<string> source = null;

        // Act
        var (matches, nonMatches) = source.Partition(x => true);

        // Assert
        matches.ShouldNotBeNull();
        matches.ShouldBeEmpty();
        nonMatches.ShouldNotBeNull();
        nonMatches.ShouldBeEmpty();
    }

    [Fact]
    public void Partition_EmptySource_ReturnsEmptyPartitions()
    {
        // Arrange
        var source = Array.Empty<string>();

        // Act
        var (matches, nonMatches) = source.Partition(x => true);

        // Assert
        matches.ShouldNotBeNull();
        matches.ShouldBeEmpty();
        nonMatches.ShouldNotBeNull();
        nonMatches.ShouldBeEmpty();
    }

    [Fact]
    public void Partition_PredicateMatchesAll_ReturnsAllInMatches()
    {
        // Arrange
        var source = new[] { this.faker.Random.Int(), this.faker.Random.Int(), this.faker.Random.Int() };

        // Act
        var (matches, nonMatches) = source.Partition(_ => true);

        // Assert
        matches.ShouldBe(source);
        nonMatches.ShouldBeEmpty();
    }

    [Fact]
    public void Partition_PredicateMatchesNone_ReturnsAllInNonMatches()
    {
        // Arrange
        var source = new[] { this.faker.Random.Int(), this.faker.Random.Int(), this.faker.Random.Int() };

        // Act
        var (matches, nonMatches) = source.Partition(_ => false);

        // Assert
        matches.ShouldBeEmpty();
        nonMatches.ShouldBe(source);
    }

    [Fact]
    public void Partition_PredicateMatchesSome_ReturnsSplitCollections()
    {
        // Arrange
        var evenNumbers = new[] { 2, 4, 6 };
        var oddNumbers = new[] { 1, 3, 5 };
        var source = evenNumbers.Concat(oddNumbers).ToArray();

        // Act
        var (matches, nonMatches) = source.Partition(x => x % 2 == 0);

        // Assert
        matches.ShouldBe(evenNumbers);
        nonMatches.ShouldBe(oddNumbers);
    }

    [Fact]
    public void Partition_WithStrings_HandlesEmptyStrings()
    {
        // Arrange
        var nonEmptyStrings = new[] { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var emptyStrings = new[] { string.Empty, string.Empty };
        var source = nonEmptyStrings.Concat(emptyStrings).ToArray();

        // Act
        var (matches, nonMatches) = source.Partition(s => !string.IsNullOrEmpty(s));

        // Assert
        matches.ShouldBe(nonEmptyStrings);
        nonMatches.ShouldBe(emptyStrings);
    }

    [Fact]
    public void Partition_WithCustomObjects_PreservesObjectReferences()
    {
        // Arrange
        var obj1 = new TestObject { Id = 1, IsValid = true };
        var obj2 = new TestObject { Id = 2, IsValid = false };
        var obj3 = new TestObject { Id = 3, IsValid = true };
        var source = new[] { obj1, obj2, obj3 };

        // Act
        var (matches, nonMatches) = source.Partition(x => x.IsValid);

        // Assert
        matches.ShouldContain(obj1);
        matches.ShouldContain(obj3);
        nonMatches.ShouldContain(obj2);
    }

    [Fact]
    public void Partition_WithLargeDataSet_HandlesEfficiently()
    {
        // Arrange
        var itemCount = 10000;
        var source = Enumerable.Range(1, itemCount).ToArray();

        // Act
        var (matches, nonMatches) = source.Partition(x => x % 2 == 0);

        // Assert
        matches.Count().ShouldBe(itemCount / 2);
        nonMatches.Count().ShouldBe(itemCount / 2);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public bool IsValid { get; set; }
    }
}