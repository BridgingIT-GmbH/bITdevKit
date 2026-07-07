// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public class DocumentStoreClientTests
{
    private readonly IDocumentStoreProvider provider;
    private readonly DocumentStoreClient<DocumentClientPersonStub> sut;

    public DocumentStoreClientTests()
    {
        this.provider = Substitute.For<IDocumentStoreProvider>();
        this.sut = new DocumentStoreClient<DocumentClientPersonStub>(this.provider);
    }

    [Fact]
    public async Task GetResultAsync_WithDocumentKey_ShouldCallProvider()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        var expected = new DocumentClientPersonStub { FirstName = "Mary" };
        this.provider.GetResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>())
            .Returns(Result<DocumentClientPersonStub>.Success(expected));

        // Act
        var result = await this.sut.GetResultAsync(documentKey);

        // Assert
        await this.provider.Received(1)
            .GetResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task FindPageResultAsync_WithQuery_ShouldCallProvider()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("partitionKey", "row")
            .WithRowKeyPrefix()
            .Take(10)
            .Build();
        var expected = new DocumentPage<DocumentClientPersonStub>
        {
            Items = [new DocumentClientPersonStub { FirstName = "John" }]
        };
        this.provider.FindPageResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>())
            .Returns(Result<DocumentPage<DocumentClientPersonStub>>.Success(expected));

        // Act
        var result = await this.sut.FindPageResultAsync(query);

        // Assert
        await this.provider.Received(1)
            .FindPageResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ListPageResultAsync_WithQuery_ShouldCallProvider()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("partitionKey", "row")
            .WithRowKeyPrefix()
            .Take(10)
            .Build();
        var expected = new DocumentKeyPage
        {
            Items = [new DocumentKey("partitionKey", "row1")]
        };
        this.provider.ListPageResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>())
            .Returns(Result<DocumentKeyPage>.Success(expected));

        // Act
        var result = await this.sut.ListPageResultAsync(query);

        // Assert
        await this.provider.Received(1)
            .ListPageResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task CountResultAsync_WithQuery_ShouldCallProvider()
    {
        // Arrange
        var query = DocumentQueries.Count()
            .ForKey("partitionKey", "row")
            .WithRowKeyPrefix()
            .Build();
        this.provider.CountResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>())
            .Returns(Result<long>.Success(5));

        // Act
        var result = await this.sut.CountResultAsync(query);

        // Assert
        await this.provider.Received(1)
            .CountResultAsync<DocumentClientPersonStub>(query, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
    }

    [Fact]
    public async Task ExistsResultAsync_WithDocumentKey_ShouldCallProvider()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        this.provider.ExistsResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        // Act
        var result = await this.sut.ExistsResultAsync(documentKey);

        // Assert
        await this.provider.Received(1)
            .ExistsResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_WithDocumentKeyAndEntity_ShouldCallProvider()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        var entity = new DocumentClientPersonStub { FirstName = "John" };
        this.provider.UpsertResultAsync(documentKey, entity, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await this.sut.UpsertResultAsync(documentKey, entity);

        // Assert
        await this.provider.Received(1)
            .UpsertResultAsync(documentKey, entity, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertResultAsync_WithEntities_ShouldCallProvider()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var entities = new List<(DocumentKey DocumentKey, DocumentClientPersonStub Entity)>
        {
            (new DocumentKey("partitionKey", "rowKey1"), new DocumentClientPersonStub { FirstName = "John" }),
            (new DocumentKey("partitionKey", "rowKey2"), new DocumentClientPersonStub { FirstName = "Mary" })
        };
        this.provider.UpsertResultAsync(entities, cancellationTokenSource.Token)
            .Returns(Result.Success());

        // Act
        var result = await this.sut.UpsertResultAsync(entities, cancellationTokenSource.Token);

        // Assert
        await this.provider.Received(1)
            .UpsertResultAsync(entities, cancellationTokenSource.Token);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteResultAsync_WithDocumentKey_ShouldCallProvider()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        this.provider.DeleteResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await this.sut.DeleteResultAsync(documentKey);

        // Assert
        await this.provider.Received(1)
            .DeleteResultAsync<DocumentClientPersonStub>(documentKey, Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullProvider_ShouldThrow()
    {
        // Arrange & Act
        var action = () => new DocumentStoreClient<DocumentClientPersonStub>(null);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }
}

public class DocumentClientPersonStub
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}
