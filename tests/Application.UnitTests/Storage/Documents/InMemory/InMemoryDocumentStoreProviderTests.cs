// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using BridgingIT.DevKit.Application.Storage;

public class InMemoryDocumentStoreProviderTests
{
    [Fact]
    public async Task FindAsync_WithoutFilter_ReturnsEntities()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var expectedEntities = new List<UnitTests.PersonStub>
        {
            new() { Nationality = "USA", FirstName = "John", LastName = "Doe", Age = 18 },
            new() { Nationality = "USA", FirstName = "Mary", LastName = "Jane", Age = 23 },
        };

        context.Find<UnitTests.PersonStub>().Returns(expectedEntities);

        // Act
        var result = await sut.FindAsync<UnitTests.PersonStub>();

        // Assert
        result.ShouldBe(expectedEntities);
    }

    [Fact]
    public async Task FindAsync_WithDocumentKeyAndFilter_ReturnsFilteredEntities()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var documentKey = new DocumentKey("partition", "row");
        var expectedEntities = new List<UnitTests.PersonStub>
        {
            new() { Nationality = "USA", FirstName = "John", LastName = "Doe", Age = 18 },
            new() { Nationality = "USA", FirstName = "Mary", LastName = "Jane", Age = 23 },
        };

        context.Find<UnitTests.PersonStub>(documentKey, DocumentKeyFilter.FullMatch).Returns(expectedEntities);

        // Act
        var result = await sut.FindAsync<UnitTests.PersonStub>(documentKey, DocumentKeyFilter.FullMatch);

        // Assert
        result.ShouldBe(expectedEntities);
    }

    [Fact]
    public async Task ListAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var expectedDocumentKeys = new List<DocumentKey>
        {
            new("partition", "row1"),
            new("partition", "row2")
        };

        context.List<UnitTests.PersonStub>().Returns(expectedDocumentKeys);

        // Act
        var result = await sut.ListAsync<UnitTests.PersonStub>();

        // Assert
        result.ShouldBe(expectedDocumentKeys);
    }

    [Fact]
    public async Task ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var documentKey = new DocumentKey("partition", "row");
        var expectedDocumentKeys = new List<DocumentKey>
        {
            new("partition", "row1"),
            new("partition", "row2")
        };

        context.List<UnitTests.PersonStub>(documentKey, DocumentKeyFilter.FullMatch).Returns(expectedDocumentKeys);

        // Act
        var result = await sut.ListAsync<UnitTests.PersonStub>(documentKey, DocumentKeyFilter.FullMatch);

        // Assert
        result.ShouldBe(expectedDocumentKeys);
    }

    [Fact]
    public async Task CountAsync_ReturnsNumberOfEntities()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var expectedEntities = new List<UnitTests.PersonStub>
        {
            new() { Nationality = "USA", FirstName = "John", LastName = "Doe", Age = 18 },
            new() { Nationality = "USA", FirstName = "Mary", LastName = "Jane", Age = 23 },
        };
        context.Find<UnitTests.PersonStub>().Returns(expectedEntities);

        // Act
        var result = await sut.CountAsync<UnitTests.PersonStub>();

        // Assert
        result.ShouldBe(expectedEntities.Count);
    }

    [Fact]
    public async Task UpsertAsync_CreatesOrUpdateEntity()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);
        var documentKey = new DocumentKey("partition", "row");
        var entity = new UnitTests.PersonStub { Nationality = "USA", FirstName = "John", LastName = "Doe", Age = 18 };

        // Act
        await sut.UpsertAsync(documentKey, entity);

        // Assert
        context.Received().AddOrUpdate(Arg.Any<UnitTests.PersonStub>(), documentKey);
    }

    [Fact]
    public async Task DeleteAsync_DeletesEntity()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var context = Substitute.For<InMemoryDocumentStoreContext>();
        var documentKey = new DocumentKey("partition", "row");
        var sut = new InMemoryDocumentStoreProvider(loggerFactory, context);

        // Act
        await sut.DeleteAsync<UnitTests.PersonStub>(documentKey);

        // Assert
        context.Received().Delete<UnitTests.PersonStub>(documentKey);
    }
}