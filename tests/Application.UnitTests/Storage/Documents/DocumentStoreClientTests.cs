// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;

public class DocumentStoreClientTests
{
    private readonly IDocumentStoreProvider provider;
    private readonly DocumentStoreClient<PersonStub> sut;

    public DocumentStoreClientTests()
    {
        this.provider = Substitute.For<IDocumentStoreProvider>();
        this.sut = new DocumentStoreClient<PersonStub>(this.provider);
    }

    [Fact]
    public async Task CountAsync_WithNoCancellation_ShouldCallProviderCountAsync()
    {
        // Arrange
        const int expectedCount = 5;
        this.provider.CountAsync<PersonStub>(default).ReturnsForAnyArgs(expectedCount);

        // Act
        var result = await this.sut.CountAsync();

        // Assert
        await this.provider.Received(1).CountAsync<PersonStub>(default);
        result.ShouldBe(expectedCount);
    }

    [Fact]
    public async Task DeleteAsync_WithDocumentKeyAndNoCancellation_ShouldCallProviderDeleteAsync()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");

        // Act
        await this.sut.DeleteAsync(documentKey);

        // Assert
        await this.provider.Received(1).DeleteAsync<PersonStub>(documentKey, default);
    }

    [Fact]
    public async Task ExistsAsync_WithDocumentKeyAndNoCancellation_ShouldCallProviderExistsAsync()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        this.provider.ExistsAsync<PersonStub>(documentKey, default).ReturnsForAnyArgs(true);

        // Act
        var result = await this.sut.ExistsAsync(documentKey);

        // Assert
        await this.provider.Received(1).ExistsAsync<PersonStub>(documentKey, default);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task FindAsync_WithNoFiltersAndNoCancellation_ShouldCallProviderFindAsync()
    {
        // Arrange
        var expectedEntities = new List<PersonStub> { new(), new() };
        this.provider.FindAsync<PersonStub>(default).ReturnsForAnyArgs(expectedEntities);

        // Act
        var result = await this.sut.FindAsync();

        // Assert
        await this.provider.Received(1).FindAsync<PersonStub>(default);
        result.ShouldBe(expectedEntities);
    }

    [Fact]
    public async Task FindAsync_WithDocumentKeyAndNoFiltersAndNoCancellation_ShouldCallProviderFindAsync()
    {
        // Arrange
        var documentKey = new DocumentKey("partitionKey", "rowKey");
        var expectedEntities = new List<PersonStub> { new(), new() };
        this.provider.FindAsync<PersonStub>(documentKey, default).ReturnsForAnyArgs(expectedEntities);

        // Act
        var result = await this.sut.FindAsync(documentKey);

        // Assert
        await this.provider.Received(1).FindAsync<PersonStub>(documentKey, default);
        result.ShouldBe(expectedEntities);
    }

    [Fact]
    public async Task ListAsync_WithNoDocumentKey_ReturnsExpectedResult()
    {
        // Arrange
        var expectedKeys = new List<DocumentKey>()
        {
            new("partition1", "row1"),
            new("partition2", "row2")
        };
        this.provider.ListAsync<PersonStub>(Arg.Any<CancellationToken>())
            .Returns(expectedKeys);

        // Act
        var result = await this.sut.ListAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(expectedKeys);
    }

    [Fact]
    public async Task ListAsync_WithDocumentKeyAndNoFilter_ReturnsExpectedResult()
    {
        // Arrange
        var documentKey = new DocumentKey("partition1", "row1");
        var expectedKeys = new List<DocumentKey>()
        {
            new("partition1", "row1"),
            new("partition1", "row1")
        };
        this.provider.ListAsync<PersonStub>(documentKey, Arg.Any<CancellationToken>())
            .Returns(expectedKeys);

        // Act
        var result = await this.sut.ListAsync(documentKey, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedKeys);
    }
}

public class PersonStub
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}