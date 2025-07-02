// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

// ReSharper disable ExpressionIsAlwaysNull

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Bogus;
using Shouldly;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Xunit;

public class ExcludeNullTests
{
    private readonly Faker faker = new();

    [Fact]
    public void ExcludeNull_NullEnumerableOfClass_ReturnsEmptyEnumerable()
    {
        // Arrange
        IEnumerable<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_EnumerableWithNullValues_ReturnsCleanEnumerable()
    {
        // Arrange
        var nonNullValues = new[] { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var source = new[] { nonNullValues[0], null, nonNullValues[1], null };

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldBe(nonNullValues);
    }

    [Fact]
    public void ExcludeNull_NullEnumerableOfNullableType_ReturnsEmptyEnumerable()
    {
        // Arrange
        IEnumerable<int?> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_EnumerableOfNullableTypeWithNulls_ReturnsCleanEnumerable()
    {
        // Arrange
        var nonNullValues = new[] { this.faker.Random.Int(), this.faker.Random.Int() };
        var source = new int?[] { nonNullValues[0], null, nonNullValues[1], null };

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldBe(nonNullValues);
    }

    [Fact]
    public void ExcludeNullAsList_NullSource_ReturnsEmptyList()
    {
        // Arrange
        List<string> source = null;

        // Act
        var result = source.ExcludeNullAsList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_NullDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        IDictionary<string, string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_DictionaryWithNullValues_ReturnsCleanDictionary()
    {
        // Arrange
        var key1 = this.faker.Lorem.Word();
        var key2 = this.faker.Lorem.Word();
        var value = this.faker.Lorem.Word();
        var source = new Dictionary<string, string>
        {
            { key1, value },
            { key2, null }
        };

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.Count.ShouldBe(1);
        result[key1].ShouldBe(value);
    }

    [Fact]
    public void ExcludeNull_NullStack_ReturnsEmptyStack()
    {
        // Arrange
        Stack<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    //[Fact]
    //public void ExcludeNull_StackWithNullValues_ReturnsCleanStack()
    //{
    //    // Arrange
    //    var value = this.faker.Lorem.Word();
    //    var source = new Stack<string>();
    //    source.Push(null);
    //    source.Push(value);
    //    source.Push(null);

    //    // Act
    //    var result = source.ExcludeNull();

    //    // Assert
    //    result.Count.ShouldBe(1);
    //    result.Pop().ShouldBe(value);
    //}

    [Fact]
    public void ExcludeNull_NullQueue_ReturnsEmptyQueue()
    {
        // Arrange
        Queue<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    //[Fact]
    //public void ExcludeNull_QueueWithNullValues_ReturnsCleanQueue()
    //{
    //    // Arrange
    //    var value = this.faker.Lorem.Word();
    //    var source = new Queue<string>();
    //    source.Enqueue(null);
    //    source.Enqueue(value);
    //    source.Enqueue(null);

    //    // Act
    //    var result = source.ExcludeNull();

    //    // Assert
    //    result.Count.ShouldBe(1);
    //    result.Dequeue().ShouldBe(value);
    //}

    [Fact]
    public void ExcludeNull_NullLinkedList_ReturnsEmptyLinkedList()
    {
        // Arrange
        LinkedList<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_NullArray_ReturnsEmptyArray()
    {
        // Arrange
        string[] source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_ArrayWithNullValues_ReturnsCleanArray()
    {
        // Arrange
        var nonNullValues = new[] { this.faker.Lorem.Word(), this.faker.Lorem.Word() };
        var source = new[] { nonNullValues[0], null, nonNullValues[1], null };

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldBe(nonNullValues);
    }

    [Fact]
    public void ExcludeNull_NullHashSet_ReturnsEmptyHashSet()
    {
        // Arrange
        HashSet<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_HashSetWithNullValues_ReturnsCleanHashSet()
    {
        // Arrange
        var value = this.faker.Lorem.Word();
        var source = new HashSet<string> { null, value, null };

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.Count.ShouldBe(1);
        result.ShouldContain(value);
    }

    [Fact]
    public void ExcludeNull_NullSortedSet_ReturnsEmptySortedSet()
    {
        // Arrange
        SortedSet<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_NullObservableCollection_ReturnsEmptyObservableCollection()
    {
        // Arrange
        ObservableCollection<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExcludeNull_NullConcurrentBag_ReturnsEmptyConcurrentBag()
    {
        // Arrange
        ConcurrentBag<string> source = null;

        // Act
        var result = source.ExcludeNull();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}