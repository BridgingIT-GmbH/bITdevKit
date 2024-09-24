// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using FizzWare.NBuilder;

[IntegrationTest("Domain")]
public class InMemoryRepositoryTests
{
    private readonly IEnumerable<StubEntityString> entities;
    private readonly IEnumerable<StubEntityGuid> entitiesGuid;

    public InMemoryRepositoryTests()
    {
        this.entities = Builder<StubEntityString>
            .CreateListOfSize(20)
            .All()
            .With(x => x.Country, "USA")
            .Build();

        this.entitiesGuid = Builder<StubEntityGuid>
            .CreateListOfSize(20)
            .All()
            .With(x => x.Country, "USA")
            .Build();
    }

    [Fact]
    public async Task DeleteEntity_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var entity = this.entities.FirstOrDefault(e => e.FirstName ==
            this.entities.First()
                .FirstName);
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
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

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
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var result = await sut.FindAllAsync()
            .AnyContext();

        // Assert
        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstOrDefault()
                ?.FirstName);
    }

    [Fact]
    public async Task CountAsync_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act & Assert
        var result = await sut.CountAsync(new List<ISpecification<StubEntityString>>
            {
                new StubHasNameSpecification(this.entities.First()
                    .FirstName), // And
                new StubHasMinimumAgeSpecification(0)
            })
            .AnyContext();

        Assert.True(result > 0);
    }

    [Fact]
    public async Task FindAllEntitiesWithMultipleSpecifications_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act & Assert
        var result = await sut.FindAllAsync(new List<ISpecification<StubEntityString>>
            {
                new StubHasNameSpecification(this.entities.First()
                    .FirstName), // And
                new StubHasMinimumAgeSpecification(0)
            })
            .AnyContext();

        Assert.False(result.IsNullOrEmpty());
        Assert.Equal("FirstName1",
            result.FirstOrDefault()
                ?.FirstName);

        result = await sut.FindAllAsync(new List<ISpecification<StubEntityString>>
            {
                new StubHasNameSpecification(this.entities.First()
                    .FirstName), // And
                new StubHasNameSpecification("Unknown")
            })
            .AnyContext();

        Assert.True(result.IsNullOrEmpty());
    }

    [Fact]
    public async Task FindAllEntitiesWithSingleSpecification_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act & Assert
        var result = await sut.FindAllAsync(new StubHasNameSpecification(this.entities.First()
                .FirstName))
            .AnyContext();

        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstOrDefault()
                ?.FirstName);

        result = await sut.FindAllAsync(new StubHasMinimumAgeSpecification(0))
            .AnyContext();

        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(20, result.Count());

        result = await sut.FindAllAsync(new StubHasMinimumAgeSpecification(0),
                new FindOptions<StubEntityString>(take: 5, orderExpression: e => e.Country))
            .AnyContext();

        Assert.False(result.IsNullOrEmpty());
        Assert.Equal(5, result.Count());
    }

    [Fact]
    public async Task FindAllWithAndSpecification_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var findResults = await sut.FindAllAsync(new StubHasNameSpecification(this.entities.First()
                    .FirstName)
                .And(new StubHasMinimumAgeSpecification(0)))
            .AnyContext();

        // Assert
        Assert.False(findResults.IsNullOrEmpty());
        Assert.Equal(this.entities.First()
                .FirstName,
            findResults.FirstOrDefault()
                ?.FirstName);
    }

    [Fact]
    public async Task FindAllWithNotSpecification_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var findResults = await sut.FindAllAsync(new StubHasNameSpecification(this.entities.First()
                .FirstName).Not())
            .AnyContext();

        // Assert
        Assert.False(findResults.IsNullOrEmpty());
        Assert.DoesNotContain(findResults,
            f => f.FirstName ==
                this.entities.First()
                    .FirstName);
    }

    [Fact]
    public async Task FindAllWithOrSpecification_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var findResults = await sut.FindAllAsync(new StubHasNameSpecification(this.entities.First()
                    .FirstName)
                .Or(new StubHasNameSpecification(this.entities.Last()
                    .FirstName)))
            .AnyContext();

        // Assert
        Assert.False(findResults.IsNullOrEmpty());
        Assert.Equal(2, findResults.Count());
        Assert.Contains(findResults,
            f => f.FirstName ==
                this.entities.First()
                    .FirstName);
        Assert.Contains(findResults,
            f => f.FirstName ==
                this.entities.Last()
                    .FirstName);
    }

    [Fact]
    public async Task FindOneEntityByGuidId_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityGuid>(o => o
            .Context(new InMemoryContext<StubEntityGuid>(this.entitiesGuid)));

        // Act & Assert
        var result = await sut.FindOneAsync(this.entitiesGuid.First()
                .Id)
            .AnyContext();

        Assert.NotNull(result);
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstName);

        result = await sut.FindOneAsync(this.entitiesGuid.First()
                .Id)
            .AnyContext();

        Assert.NotNull(result);
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstName);
    }

    [Fact]
    public async Task FindOneEntityByStringId_Test()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act & Assert
        var id = this.entities.First()
            .Id;
        var result = await sut.FindOneAsync(id)
            .AnyContext();

        Assert.NotNull(result);
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstName);

        result = await sut.FindOneAsync(id)
            .AnyContext();

        Assert.NotNull(result);
        Assert.Equal(this.entities.First()
                .FirstName,
            result.FirstName);
    }

    [Fact]
    public async Task InsertNewEntityWithId_EntityIsAdded()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var result = await sut.UpsertAsync(new StubEntityString { FirstName = "FirstName99", Id = "Id99" })
            .AnyContext();

        var findResult = await sut.FindOneAsync("Id99")
            .AnyContext();

        // Assert
        Assert.Equal(RepositoryActionResult.Inserted, result.action);
        Assert.False(result.entity.Id.IsNullOrEmpty());
        Assert.False(result.entity.Id == default);
        Assert.Equal("Id99", result.entity.Id);
        Assert.NotNull(findResult);
        Assert.Equal("FirstName99", findResult.FirstName);
    }

    [Fact]
    public async Task InsertNewEntityWithoutId_EntityIsAddedWithGeneratedId()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var result = await sut.UpsertAsync(new StubEntityString { FirstName = "FirstName88" })
            .AnyContext();

        var findResult = await sut.FindOneAsync(result.entity.Id)
            .AnyContext();

        // Assert
        Assert.Equal(RepositoryActionResult.Inserted, result.action);
        Assert.NotNull(result.entity);
        Assert.False(result.entity.Id.IsNullOrEmpty());
        Assert.False(result.entity.Id == default);
        Assert.NotNull(findResult);
        Assert.Equal(findResult.Id, result.entity.Id);
        Assert.Equal("FirstName88", findResult.FirstName);
    }

    [Fact]
    public async Task UpsertExistingEntityWithId_EntityIsUpdated()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var result = await sut.UpsertAsync(new StubEntityString { Id = "Id1", FirstName = "FirstName77", LastName = "LastName77" })
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

    private class StubEntityString : AggregateRoot<string>
    {
        public string Country { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }
    }

    private class StubEntityGuid : AggregateRoot<Guid>
    {
        public string Country { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Age { get; set; }
    }

    private class StubHasNameSpecification : Specification<StubEntityString>
    {
        private readonly string firstName;

        public StubHasNameSpecification(string firstName)
        {
            EnsureArg.IsNotNull(firstName);

            this.firstName = firstName;
        }

        public override Expression<Func<StubEntityString, bool>> ToExpression()
        {
            return p => p.FirstName == this.firstName;
        }
    }

    private class StubHasMinimumAgeSpecification(int age) : Specification<StubEntityString>
    {
        private readonly int age = age;

        public override Expression<Func<StubEntityString, bool>> ToExpression()
        {
            return p => p.Age >= this.age;
        }
    }
}