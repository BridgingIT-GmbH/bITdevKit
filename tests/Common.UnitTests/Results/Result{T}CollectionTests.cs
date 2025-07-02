// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using System.Collections;

[UnitTest("Common")]
public class ResultValueCollectionTests
{
    [Fact]
    public void Filter_WithPredicate_FiltersCollection()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var filteredResult = result.FilterItems(x => x > 2);

        // Assert
        filteredResult.ShouldBeSuccess();
        filteredResult.Value.ShouldBe([3, 4, 5]);
    }

    [Fact]
    public void Map_WithMapper_TransformsCollection()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var mappedResult = result.MapItems(x => x * 2);

        // Assert
        mappedResult.ShouldBeSuccess();
        mappedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
    }

    [Fact]
    public void ForEach_WithAction_PerformsActionOnEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);
        var sum = 0;

        // Act
        var forEachResult = result.ForEach(x => sum += x);

        // Assert
        forEachResult.ShouldBeSuccess();
        sum.ShouldBe(15);
    }

    [Fact]
    public void Traverse_WithOperation_TransformsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var traversedResult = result.Traverse(x => Result<int>.Success(x * 2));

        // Assert
        traversedResult.ShouldBeSuccess();
        traversedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
    }

    [Fact]
    public void Bind_WithBinder_BindsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var boundResult = result.BindItems(x => Result<IEnumerable<int>>.Success([x, x * 2]));

        // Assert
        boundResult.ShouldBeSuccess();
        boundResult.Value.ShouldBe([1, 2, 2, 4, 3, 6, 4, 8, 5, 10]);
    }

    [Fact]
    public void Flatten_WithResults_FlattensCollection()
    {
        // Arrange
        var results = new List<Result<IEnumerable<int>>>
        {
            Result<IEnumerable<int>>.Success([1, 2]),
            Result<IEnumerable<int>>.Success([3, 4]),
            Result<IEnumerable<int>>.Success([5])
        };

        // Act
        var flattenedResult = results.Flatten();

        // Assert
        flattenedResult.ShouldBeSuccess();
        flattenedResult.Value.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void SelectMany_WithSelector_ProjectsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var selectedResult = result.SelectMany(x => new List<int> { x, x * 2 });

        // Assert
        selectedResult.ShouldBeSuccess();
        selectedResult.Value.ShouldBe([1, 2, 2, 4, 3, 6]);
    }

    [Fact]
    public void Tap_WithAction_PerformsActionOnEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);
        var sum = 0;

        // Act
        var tappedResult = result.TapItems(x => sum += x);

        // Assert
        tappedResult.ShouldBeSuccess();
        sum.ShouldBe(15);
    }

    [Fact]
    public void Do_WithAction_PerformsAction()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);
        var actionExecuted = false;

        // Act
        var doResult = result.Do(() => actionExecuted = true);

        // Assert
        doResult.ShouldBeSuccess();
        actionExecuted.ShouldBeTrue();
    }

    [Fact]
    public void TeeMap_WithMapperAndAction_TransformsAndPerformsAction()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);
        var sum = 0;

        // Act
        var teeMappedResult = result.TeeMapItems(x => x * 2, x => sum += x);

        // Assert
        teeMappedResult.ShouldBeSuccess();
        teeMappedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
        sum.ShouldBe(30);
    }

    [Fact]
    public void Validate_WithValidator_ValidatesEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var validatedResult = result.ValidateItems(x => x > 0 ? Result<int>.Success(x) : Result<int>.Failure("Invalid value"));

        // Assert
        validatedResult.ShouldBeSuccess();
        validatedResult.Value.ShouldBe(values);
    }

    [Fact]
    public void EnsureAll_WithPredicate_EnsuresAllItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var ensuredResult = result.EnsureAll(x => x > 0, new Error("All items must be positive"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
    }

    [Fact]
    public void EnsureAny_WithPredicate_EnsuresAnyItem()
    {
        // Arrange
        var values = new List<int> { -1, -2, 3, -4, -5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var ensuredResult = result.EnsureAny(x => x > 0, new Error("At least one item must be positive"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
    }

    [Fact]
    public void EnsureNotEmpty_EnsuresCollectionIsNotEmpty()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var ensuredResult = result.EnsureNotEmpty(new Error("Collection must not be empty"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
    }

    [Fact]
    public void EnsureCount_EnsuresCollectionCount()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var ensuredResult = result.EnsureCount(5, new Error("Collection must have 5 items"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
    }

    [Fact]
    public void Chunk_SplitsCollectionIntoChunks()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5, 6 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var chunkedResult = result.Chunk(2);

        // Assert
        chunkedResult.ShouldBeSuccess();
        chunkedResult.Value.ShouldBe(new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3, 4 }, new List<int> { 5, 6 } });
    }

    [Fact]
    public void DistinctBy_RemovesDuplicateItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 2, 3, 4, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var distinctResult = result.DistinctBy(x => x);

        // Assert
        distinctResult.ShouldBeSuccess();
        distinctResult.Value.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void First_WithOperation_TransformsFirstItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var firstResult = result.First(x => Result<string>.Success(x.ToString()));

        // Assert
        firstResult.ShouldBeSuccess();
        firstResult.Value.ShouldBe("1");
    }

    [Fact]
    public void GroupBy_GroupsItemsByKey()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var groupedResult = result.GroupBy(x => x % 2);

        // Assert
        groupedResult.ShouldBeSuccess();
        groupedResult.Value.ShouldBe(
        [
            new Grouping<int, int>(1, [1, 3, 5]),
            new Grouping<int, int>(0, [2, 4])
        ]);
    }

    [Fact]
    public void Aggregate_AggregatesItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var aggregatedResult = result.Aggregate(0, (acc, x) => acc + x);

        // Assert
        aggregatedResult.ShouldBeSuccess();
        aggregatedResult.Value.ShouldBe(15);
    }

    [Fact]
    public void OrderBy_OrdersItems()
    {
        // Arrange
        var values = new List<int> { 5, 3, 1, 4, 2 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var orderedResult = result.OrderBy(x => x);

        // Assert
        orderedResult.ShouldBeSuccess();
        orderedResult.Value.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void OrderByDescending_OrdersItemsInDescendingOrder()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = Result<IEnumerable<int>>.Success(values);

        // Act
        var orderedResult = result.OrderByDescending(x => x);

        // Assert
        orderedResult.ShouldBeSuccess();
        orderedResult.Value.ShouldBe([5, 4, 3, 2, 1]);
    }
}

// Add this class to represent the grouped elements
public class Grouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements) : IGrouping<TKey, TElement>
{
    public TKey Key { get; } = key;

    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}