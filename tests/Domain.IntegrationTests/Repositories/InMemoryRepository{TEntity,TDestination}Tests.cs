// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Infrastructure.Mapping;
using FizzWare.NBuilder;

[IntegrationTest("Domain")]
public class InMemoryRepositoryDbTests
{
    private readonly IEnumerable<StubEntity> entities;

    public InMemoryRepositoryDbTests()
    {
        this.entities = Builder<StubEntity>
            .CreateListOfSize(20)
            .All()
            .With(x => x.FirstName, "John")
            .With(x => x.LastName, this.GenerateRandomString(5))
            .With(x => x.Country, "USA")
            .Build()
            .Concat(new[]
            {
                new StubEntity
                {
                    Id = "Id99",
                    FirstName = "John",
                    LastName = "Doe",
                    Age = 38,
                    Country = "USA"
                }
            });
    }

    public string GenerateRandomString(int length = 5)
    {
        var chars = Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 8);

        return new string(chars.SelectMany(str => str)
            .OrderBy(c => Guid.NewGuid())
            .Take(length)
            .ToArray());
    }

    [Fact]
    public async Task DeleteEntity_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var entity = (await sut.FindAllAsync()
            .AnyContext()).FirstOrDefault();
        await sut.DeleteAsync(entity)
            .AnyContext();
        entity = await sut.FindOneAsync(entity.Id)
            .AnyContext();

        // Assert
        Assert.Null(entity);
    }

    [Fact]
    public async Task DeleteEntityById_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var id = this.entities.First()
            .Id;
        await sut.DeleteAsync(id)
            .AnyContext();
        var entity = await sut.FindOneAsync(id)
            .AnyContext();

        // Assert
        Assert.Null(entity);
    }

    [Fact]
    public async Task FindAllEntities_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var result = await sut.FindAllAsync()
            .AnyContext();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.All(e =>
            !e.Id.IsNullOrEmpty() && !e.FirstName.IsNullOrEmpty() && !e.LastName.IsNullOrEmpty()));
        Assert.NotNull(result.FirstOrDefault(e => e.FirstName == "John" && e.LastName == "Doe"));
    }

    [Fact]
    public async Task FindAllTenantEntities_Test() // TODO: move to own test class + mocks
    {
        // Arrange
        var sut = new RepositorySpecificationBehavior<StubEntity>(new Specification<StubEntity>(t => t.Country == "USA"), // add a default specification
            new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                    .Context(new InMemoryContext<StubEntity>(this.entities))
                    .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
                e => e.Identifier));

        // Act
        var result = await sut.FindAllAsync()
            .AnyContext();

        // Assert
        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(21, result.Count());
        Assert.NotNull(result.FirstOrDefault(e => e.Id == "Id99"));
    }

    [Fact]
    public async Task FindAllTenantEntities2_Test() // TODO: move to own test class + mocks
    {
        // Arrange
        var sut = new RepositorySpecificationBehavior<StubEntity>(new Specification<StubEntity>(t => t.Country == "USA"), // add a default specification
            new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                    .Context(new InMemoryContext<StubEntity>(this.entities))
                    .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
                e => e.Identifier));

        // Act
        var result = await sut.FindAllAsync()
            .AnyContext();

        // Assert
        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(21, result.Count());
    }

    [Fact]
    public async Task FindMappedEntitiesWithSpecification_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var result = await sut.FindAllAsync(new StubHasNameSpecification("John", "Doe"),
                new FindOptions<StubEntity>(orderExpression: e => e.Country))
            .AnyContext(); // domain layer
        //var result = await sut.FindAllAsync(
        //    new StubHasIdSpecification("Id99")).AnyContext(); // domain layer

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result.FirstOrDefault()
            ?.Id);
        Assert.NotNull(result.FirstOrDefault(e => !e.FirstName.IsNullOrEmpty() && !e.LastName.IsNullOrEmpty()));
    }

    [Fact]
    public async Task FindMappedEntityOne_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var result = await sut.FindOneAsync("Id99")
            .AnyContext();

        // Assert
        Assert.NotNull(result);
        Assert.True(!result.Id.IsNullOrEmpty() &&
            !result.FirstName.IsNullOrEmpty() &&
            !result.LastName.IsNullOrEmpty());
        Assert.True(result.Id == "Id99" && result.FirstName == "John" && result.LastName == "Doe");
    }

    [Fact]
    public async Task FindOneEntity_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var result = await sut.FindOneAsync("Id99")
            .AnyContext();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id == "Id99");
    }

    [Fact]
    public async Task FindOneTenantEntity_Test()
    {
        // Arrange
        var sut = new RepositorySpecificationBehavior<StubEntity>(new Specification<StubEntity>(t => t.Country == "USA"), // add a default specification
            new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                    .Context(new InMemoryContext<StubEntity>(this.entities))
                    .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
                e => e.Identifier));

        // Act
        var result = await sut.FindOneAsync("Id99")
            .AnyContext();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id == "Id99");
    }

    [Fact]
    public async Task UpsertExistingEntityWithId_EntityIsUpdated()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(o => o
                .Context(new InMemoryContext<StubEntity>(this.entities))
                .Mapper(new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create())),
            e => e.Identifier);

        // Act
        var result = await sut.UpsertAsync(new StubEntity { Id = "Id1", FirstName = "FirstName77", LastName = "LastName77" })
            .AnyContext();

        var findResult = await sut.FindOneAsync("Id1")
            .AnyContext();

        // Assert
        Assert.Equal(RepositoryActionResult.Updated, result.action);
        Assert.NotNull(result.entity);
        Assert.False(result.entity.Id.IsNullOrEmpty());
        Assert.False(result.entity.Id == default);
        Assert.Equal("Id1", result.entity.Id);
        Assert.NotNull(findResult);
        Assert.Equal(findResult.Id, result.entity.Id);
        Assert.Equal("FirstName77", findResult.FirstName);
    }
}