// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultPagedCollectionTests
{
    [Fact]
    public void Filter_WithPredicate_FiltersCollection()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var filteredResult = result.FilterItems(x => x > 2);

        // Assert
        filteredResult.ShouldBeSuccess();
        filteredResult.Value.ShouldBe([3, 4, 5]);
        filteredResult.TotalCount.ShouldBe(100);
        filteredResult.CurrentPage.ShouldBe(1);
        filteredResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Map_WithMapper_TransformsCollection()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var mappedResult = result.MapItems(x => x * 2);

        // Assert
        mappedResult.ShouldBeSuccess();
        mappedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
        mappedResult.TotalCount.ShouldBe(100);
        mappedResult.CurrentPage.ShouldBe(1);
        mappedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void ForEach_WithAction_PerformsActionOnEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);
        var sum = 0;

        // Act
        var forEachResult = result.ForEach(x => sum += x);

        // Assert
        forEachResult.ShouldBeSuccess();
        sum.ShouldBe(15);
        forEachResult.TotalCount.ShouldBe(100);
        forEachResult.CurrentPage.ShouldBe(1);
        forEachResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Traverse_WithOperation_TransformsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var traversedResult = result.Traverse(x => Result<int>.Success(x * 2));

        // Assert
        traversedResult.ShouldBeSuccess();
        traversedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
        traversedResult.TotalCount.ShouldBe(100);
        traversedResult.CurrentPage.ShouldBe(1);
        traversedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Bind_WithBinder_BindsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var boundResult = result.BindItems(x => Result<IEnumerable<int>>.Success([x, x * 2]));

        // Assert
        boundResult.ShouldBeSuccess();
        boundResult.Value.ShouldBe([1, 2, 2, 4, 3, 6]);
        boundResult.TotalCount.ShouldBe(100);
        boundResult.CurrentPage.ShouldBe(1);
        boundResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Flatten_WithResults_FlattensCollection()
    {
        // Arrange
        var results = new List<ResultPaged<int>>
        {
            ResultPaged<int>.Success([1, 2], 50, 1, 5),
            ResultPaged<int>.Success([3, 4], 50, 2, 5),
            ResultPaged<int>.Success([5], 50, 3, 5)
        };

        // Act
        var flattenedResult = results.Flatten();

        // Assert
        flattenedResult.ShouldBeSuccess();
        flattenedResult.Value.ShouldBe([1, 2, 3, 4, 5]);
        flattenedResult.TotalCount.ShouldBe(150);
        flattenedResult.CurrentPage.ShouldBe(1);
        flattenedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void SelectMany_WithSelector_ProjectsEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var selectedResult = result.SelectMany(x => new List<int> { x, x * 2 });

        // Assert
        selectedResult.ShouldBeSuccess();
        selectedResult.Value.ShouldBe([1, 2, 2, 4, 3, 6]);
        selectedResult.TotalCount.ShouldBe(100);
        selectedResult.CurrentPage.ShouldBe(1);
        selectedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Tap_WithAction_PerformsActionOnEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);
        var sum = 0;

        // Act
        var tappedResult = result.TapItems(x => sum += x);

        // Assert
        tappedResult.ShouldBeSuccess();
        sum.ShouldBe(15);
        tappedResult.TotalCount.ShouldBe(100);
        tappedResult.CurrentPage.ShouldBe(1);
        tappedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Do_WithAction_PerformsAction()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);
        var actionExecuted = false;

        // Act
        var doResult = result.DoItems(() => actionExecuted = true);

        // Assert
        doResult.ShouldBeSuccess();
        actionExecuted.ShouldBeTrue();
        doResult.TotalCount.ShouldBe(100);
        doResult.CurrentPage.ShouldBe(1);
        doResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void TeeMap_WithMapperAndAction_TransformsAndPerformsAction()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);
        var sum = 0;

        // Act
        var teeMappedResult = result.TeeMapItems(x => x * 2, x => sum += x);

        // Assert
        teeMappedResult.ShouldBeSuccess();
        teeMappedResult.Value.ShouldBe([2, 4, 6, 8, 10]);
        sum.ShouldBe(30);
        teeMappedResult.TotalCount.ShouldBe(100);
        teeMappedResult.CurrentPage.ShouldBe(1);
        teeMappedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Validate_WithValidator_ValidatesEachItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var validatedResult = result.ValidateItems(x => x > 0 ? Result<int>.Success(x) : Result<int>.Failure("Invalid value"));

        // Assert
        validatedResult.ShouldBeSuccess();
        validatedResult.Value.ShouldBe(values);
        validatedResult.TotalCount.ShouldBe(100);
        validatedResult.CurrentPage.ShouldBe(1);
        validatedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void EnsureAll_WithPredicate_EnsuresAllItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var ensuredResult = result.EnsureAll(x => x > 0, new Error("All items must be positive"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
        ensuredResult.TotalCount.ShouldBe(100);
        ensuredResult.CurrentPage.ShouldBe(1);
        ensuredResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void EnsureAny_WithPredicate_EnsuresAnyItem()
    {
        // Arrange
        var values = new List<int> { -1, -2, 3, -4, -5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var ensuredResult = result.EnsureAny(x => x > 0, new Error("At least one item must be positive"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
        ensuredResult.TotalCount.ShouldBe(100);
        ensuredResult.CurrentPage.ShouldBe(1);
        ensuredResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void EnsureNotEmpty_EnsuresCollectionIsNotEmpty()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var ensuredResult = result.EnsureNotEmpty(new Error("Collection must not be empty"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
        ensuredResult.TotalCount.ShouldBe(100);
        ensuredResult.CurrentPage.ShouldBe(1);
        ensuredResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void EnsureCount_EnsuresCollectionCount()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var ensuredResult = result.EnsureCount(5, new Error("Collection must have 5 items"));

        // Assert
        ensuredResult.ShouldBeSuccess();
        ensuredResult.Value.ShouldBe(values);
        ensuredResult.TotalCount.ShouldBe(100);
        ensuredResult.CurrentPage.ShouldBe(1);
        ensuredResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void Chunk_SplitsCollectionIntoChunks()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5, 6 };
        var result = ResultPaged<int>.Success(values, 100, 1, 6);

        // Act
        var chunkedResult = result.Chunk(2);

        // Assert
        chunkedResult.ShouldBeSuccess();
        chunkedResult.Value.ShouldBe(new List<List<int>> { new() { 1, 2 }, new() { 3, 4 }, new() { 5, 6 } });
        chunkedResult.TotalCount.ShouldBe(100);
        chunkedResult.CurrentPage.ShouldBe(1);
        chunkedResult.PageSize.ShouldBe(6);
    }

    [Fact]
    public void DistinctBy_RemovesDuplicateItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 2, 3, 4, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 7);

        // Act
        var distinctResult = result.DistinctBy(x => x);

        // Assert
        distinctResult.ShouldBeSuccess();
        distinctResult.Value.ShouldBe([1, 2, 3, 4, 5]);
        distinctResult.TotalCount.ShouldBe(100);
        distinctResult.CurrentPage.ShouldBe(1);
        distinctResult.PageSize.ShouldBe(7);
    }

    [Fact]
    public void First_WithOperation_TransformsFirstItem()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var firstResult = result.First(x => Result<string>.Success(x.ToString()));

        // Assert
        firstResult.ShouldBeSuccess();
        firstResult.Value.ShouldBe("1");
    }

    [Fact]
    public void Aggregate_AggregatesItems()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

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
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var orderedResult = result.OrderBy(x => x);

        // Assert
        orderedResult.ShouldBeSuccess();
        orderedResult.Value.ShouldBe([1, 2, 3, 4, 5]);
        orderedResult.TotalCount.ShouldBe(100);
        orderedResult.CurrentPage.ShouldBe(1);
        orderedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void OrderByDescending_OrdersItemsInDescendingOrder()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3, 4, 5 };
        var result = ResultPaged<int>.Success(values, 100, 1, 5);

        // Act
        var orderedResult = result.OrderByDescending(x => x);

        // Assert
        orderedResult.ShouldBeSuccess();
        orderedResult.Value.ShouldBe([5, 4, 3, 2, 1]);
        orderedResult.TotalCount.ShouldBe(100);
        orderedResult.CurrentPage.ShouldBe(1);
        orderedResult.PageSize.ShouldBe(5);
    }

    [Fact]
    public void TapItems_ExecutesActionWithPaginationDetails()
    {
        // Arrange
        var values = new List<int> { 1, 2, 3 };
        var result = ResultPaged<int>.Success(values, 100, 2, 10);
        var pageInfo = string.Empty;

        // Act
        var tappedResult = result.TapItems(r =>
            pageInfo = $"Page {r.CurrentPage}/{r.TotalPages} ({r.TotalCount} total)");

        // Assert
        tappedResult.ShouldBeSuccess();
        pageInfo.ShouldBe("Page 2/10 (100 total)");
        tappedResult.Value.ShouldBe(values);
        tappedResult.TotalCount.ShouldBe(100);
        tappedResult.CurrentPage.ShouldBe(2);
        tappedResult.PageSize.ShouldBe(10);
    }
}