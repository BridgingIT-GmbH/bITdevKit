// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Application")]
public class InMemoryDocumentStoreProviderTests
{
    private readonly InMemoryDocumentStoreProvider sut = new(NullLoggerFactory.Instance);

    [Fact]
    public async Task GetResultAsync_WithExistingKey_ReturnsEntity()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "001");
        var entity = new UnitTests.PersonStub { Nationality = "USA", FirstName = "John", LastName = "Doe", Age = 18 };
        await this.sut.UpsertResultAsync(documentKey, entity);

        // Act
        var result = await this.sut.GetResultAsync<UnitTests.PersonStub>(documentKey);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FirstName.ShouldBe("John");
    }

    [Fact]
    public async Task FindPageResultAsync_WithPrefixQuery_ReturnsBoundedPageAndContinuation()
    {
        // Arrange
        await this.UpsertPeopleAsync("people", ["001", "002", "003"]);
        var query = DocumentQueries.Query()
            .ForKey("people", "00")
            .WithRowKeyPrefix()
            .Take(2)
            .Build();

        // Act
        var firstPage = await this.sut.FindPageResultAsync<UnitTests.PersonStub>(query);
        var secondPage = await this.sut.FindPageResultAsync<UnitTests.PersonStub>(
            DocumentQueries.Query()
                .ForKey("people", "00")
                .WithRowKeyPrefix()
                .Take(2)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Count.ShouldBe(2);
        firstPage.Value.HasMore.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Count.ShouldBe(1);
        secondPage.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ListPageResultAsync_WithPrefixQuery_ReturnsKeysOnly()
    {
        // Arrange
        await this.UpsertPeopleAsync("people", ["101", "102", "201"]);
        var query = DocumentQueries.Query()
            .ForKey("people", "10")
            .WithRowKeyPrefix()
            .Take(10)
            .Build();

        // Act
        var result = await this.sut.ListPageResultAsync<UnitTests.PersonStub>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(key => key.RowKey).ShouldBe(["101", "102"]);
    }

    [Fact]
    public async Task CountResultAsync_WithPrefixQuery_ReturnsMatchingCount()
    {
        // Arrange
        await this.UpsertPeopleAsync("people", ["301", "302", "401"]);
        var query = DocumentQueries.Count()
            .ForKey("people", "30")
            .WithRowKeyPrefix()
            .Build();

        // Act
        var result = await this.sut.CountResultAsync<UnitTests.PersonStub>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
    }

    [Fact]
    public async Task ExistsResultAsync_AfterDelete_ReturnsFalse()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "501");
        await this.sut.UpsertResultAsync(documentKey, new UnitTests.PersonStub { FirstName = "Mary" });

        // Act
        var deleteResult = await this.sut.DeleteResultAsync<UnitTests.PersonStub>(documentKey);
        var existsResult = await this.sut.ExistsResultAsync<UnitTests.PersonStub>(documentKey);

        // Assert
        deleteResult.IsSuccess.ShouldBeTrue();
        existsResult.IsSuccess.ShouldBeTrue();
        existsResult.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task GetResultAsync_WithMissingKey_ReturnsFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "missing");

        // Act
        var result = await this.sut.GetResultAsync<UnitTests.PersonStub>(documentKey);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_WithNullEntity_ReturnsFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "701");

        // Act
        var result = await this.sut.UpsertResultAsync<UnitTests.PersonStub>(documentKey, null);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_WithInvalidKey_ReturnsFailure()
    {
        // Arrange
        var documentKey = new DocumentKey("people", string.Empty);

        // Act
        var result = await this.sut.UpsertResultAsync(documentKey, new UnitTests.PersonStub { FirstName = "Invalid" });

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_WithEntities_StopsOnInvalidEntity()
    {
        // Arrange
        var entities = new List<(DocumentKey DocumentKey, UnitTests.PersonStub Entity)>
        {
            (new DocumentKey("people", "801"), new UnitTests.PersonStub { FirstName = "Valid" }),
            (new DocumentKey("people", "802"), null)
        };

        // Act
        var result = await this.sut.UpsertResultAsync(entities);
        var firstExists = await this.sut.ExistsResultAsync<UnitTests.PersonStub>(new DocumentKey("people", "801"));
        var secondExists = await this.sut.ExistsResultAsync<UnitTests.PersonStub>(new DocumentKey("people", "802"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        firstExists.Value.ShouldBeTrue();
        secondExists.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task GetResultAsync_AfterMutatingOriginalAndLoadedEntity_ReturnsClonedEntity()
    {
        // Arrange
        var documentKey = new DocumentKey("people", "901");
        var entity = new UnitTests.PersonStub { FirstName = "Original", LastName = "Value" };
        await this.sut.UpsertResultAsync(documentKey, entity);
        entity.FirstName = "Changed after save";

        // Act
        var firstLoad = await this.sut.GetResultAsync<UnitTests.PersonStub>(documentKey);
        firstLoad.IsSuccess.ShouldBeTrue();
        firstLoad.Value.FirstName.ShouldBe("Original");
        firstLoad.Value.FirstName = "Changed after load";
        var secondLoad = await this.sut.GetResultAsync<UnitTests.PersonStub>(documentKey);

        // Assert
        secondLoad.IsSuccess.ShouldBeTrue();
        secondLoad.Value.FirstName.ShouldBe("Original");
    }

    [Fact]
    public async Task ListPageResultAsync_WithContinuationTokenFromDifferentQuery_ReturnsFailure()
    {
        // Arrange
        await this.UpsertPeopleAsync("people", ["a001", "a002", "b001"]);
        var firstPage = await this.sut.ListPageResultAsync<UnitTests.PersonStub>(
            DocumentQueries.Query()
                .ForKey("people", "a")
                .WithRowKeyPrefix()
                .Take(1)
                .Build());

        // Act
        var result = await this.sut.ListPageResultAsync<UnitTests.PersonStub>(
            DocumentQueries.Query()
                .ForKey("people", "b")
                .WithRowKeyPrefix()
                .Take(1)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.HasMore.ShouldBeTrue();
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task FindPageResultAsync_WithExplicitFullScan_WhenProviderAllowsFullScans_ReturnsPage()
    {
        // Arrange
        await this.UpsertPeopleAsync("people", ["601", "602"]);
        var query = DocumentQueries.Query()
            .Take(1)
            .AllowFullScan()
            .Build();

        // Act
        var result = await this.sut.FindPageResultAsync<UnitTests.PersonStub>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.HasMore.ShouldBeTrue();
    }

    private async Task UpsertPeopleAsync(string partitionKey, IEnumerable<string> rowKeys)
    {
        foreach (var rowKey in rowKeys)
        {
            await this.sut.UpsertResultAsync(
                new DocumentKey(partitionKey, rowKey),
                new UnitTests.PersonStub
                {
                    Nationality = "USA",
                    FirstName = "First" + rowKey,
                    LastName = "Last" + rowKey,
                    Age = 20
                });
        }
    }
}
